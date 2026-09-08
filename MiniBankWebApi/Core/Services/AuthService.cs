using MiniBankDTOs;
using MiniBankWebApi.Core.Entities;
using MiniBankWebApi.Helper;
using MiniBankWebApi.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MiniBankWebApi.Core.Services
{
    public class AuthService : IAuthService
    {
        private readonly BankDbContext db;
        private readonly JwtHelper jwt;
        private readonly IFaceApiService faceApi;
        private readonly IConfiguration config;
        private readonly ILogger<AuthService> logger;
        private readonly PasswordHasher<Account> hasher = new();

        public AuthService(BankDbContext db, JwtHelper jwt, IFaceApiService faceApi,
                           IConfiguration config, ILogger<AuthService> logger)
        {
            this.db = db;
            this.jwt = jwt;
            this.faceApi = faceApi;
            this.config = config;
            this.logger = logger;
        }

        public async Task<RegisterResultDto> RegisterAsync(RegisterDto dto)
        {
            var email = dto.Email.Trim().ToLower();
            var identityNumber = dto.IdentityNumber.Trim();

            EnsureOldEnough(dto.DateOfBirth);
            await EnsureEmailAndIdentityAreFreeAsync(email, identityNumber);

            var faceDescriptor = string.IsNullOrWhiteSpace(dto.FaceImageBase64)
                ? null
                : FaceHelper.ToJson(await faceApi.GetEmbeddingAsync(dto.FaceImageBase64));

            var account = new Account
            {
                AccountNumber = await CreateAccountNumberAsync(),
                FullName = dto.FullName.Trim(),
                Email = email,
                PhoneNumber = dto.PhoneNumber.Trim(),
                DateOfBirth = dto.DateOfBirth.Date,
                IdentityNumber = identityNumber,
                Address = dto.Address.Trim(),
                Balance = 0,
                FaceDescriptor = faceDescriptor
            };
            account.PasswordHash = hasher.HashPassword(account, dto.Password);

            db.Accounts.Add(account);
            await db.SaveChangesAsync();

            logger.LogInformation("New account {AccountNumber} created for {Email}", account.AccountNumber, email);

            return new RegisterResultDto
            {
                AccountId = account.Id,
                AccountNumber = account.AccountNumber,
                FullName = account.FullName,
                Email = account.Email,
                Balance = account.Balance,
                HasFace = account.FaceDescriptor != null
            };
        }

        private const int MinimumAge = 18;

        private static void EnsureOldEnough(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            if (dateOfBirth.Date > today) throw new ArgumentException("Date of birth cannot be in the future");

            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;

            if (age < MinimumAge)
            {
                throw new ArgumentException($"You must be at least {MinimumAge} years old to open an account");
            }
        }

        private async Task EnsureEmailAndIdentityAreFreeAsync(string email, string identityNumber)
        {
            var duplicated = await db.Accounts
                .Where(a => a.Email.ToLower() == email || a.IdentityNumber == identityNumber)
                .Select(a => new { a.Email })
                .FirstOrDefaultAsync();

            if (duplicated == null) return;

            throw new InvalidOperationException(duplicated.Email.ToLower() == email
                ? "This email is already used by another account"
                : "This identity number is already used by another account");
        }

        private async Task<string> CreateAccountNumberAsync()
        {
            var accountNumbers = await db.Accounts.Select(a => a.AccountNumber).ToListAsync();
            var largestNumber = accountNumbers
                .Select(number => int.TryParse(number, out var value) ? value : 0)
                .DefaultIfEmpty(1000)
                .Max();

            return (largestNumber + 1).ToString();
        }

        public async Task<TokenResponse?> LoginAsync(LoginDto dto)
        {
            var account = await db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == dto.AccountNumber);
            if (account == null) return null;

            var verify = hasher.VerifyHashedPassword(account, account.PasswordHash, dto.Password);
            if (verify == PasswordVerificationResult.Failed) return null;

            return await IssueTokenAsync(account);
        }

        public async Task<TokenResponse?> RefreshAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return null;

            var saved = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
            if (saved == null || saved.IsRevoked || saved.ExpiresAt <= DateTime.UtcNow) return null;

            var account = await db.Accounts.FindAsync(saved.AccountId);
            if (account == null) return null;

            saved.IsRevoked = true;
            return await IssueTokenAsync(account);
        }

        public async Task<bool> RegisterFaceAsync(int accountId, string imageBase64)
        {
            var account = await db.Accounts.FindAsync(accountId);
            if (account == null) return false;

            var descriptor = await faceApi.GetEmbeddingAsync(imageBase64);

            account.FaceDescriptor = FaceHelper.ToJson(descriptor);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<TokenResponse?> FaceLoginAsync(FaceLoginDto dto)
        {
            var accounts = await db.Accounts.Where(a => a.FaceDescriptor != null).ToListAsync();
            if (accounts.Count == 0)
            {
                throw new InvalidOperationException("No account has registered a face yet");
            }

            var descriptor = await faceApi.GetEmbeddingAsync(dto.ImageBase64);

            Account? bestAccount = null;
            var bestDistance = double.MaxValue;

            foreach (var account in accounts)
            {
                var savedDescriptor = FaceHelper.FromJson(account.FaceDescriptor);
                if (savedDescriptor == null) continue;

                if (savedDescriptor.Length != FaceHelper.DescriptorLength)
                {
                    logger.LogWarning("Account {AccountNumber} still keeps an old face of {Length} numbers, it is skipped",
                        account.AccountNumber, savedDescriptor.Length);
                    continue;
                }

                var distance = FaceHelper.Distance(descriptor, savedDescriptor);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestAccount = account;
            }

            var maxDistance = config.GetValue<double>("Face:MaxDistance");
            logger.LogInformation("Face login searched {Count} accounts, best match {AccountNumber} at distance {Distance}, max allowed {MaxDistance}",
                accounts.Count, bestAccount?.AccountNumber ?? "none", bestDistance, maxDistance);

            if (bestAccount == null || bestDistance > maxDistance) return null;

            return await IssueTokenAsync(bestAccount);
        }

        private async Task<TokenResponse> IssueTokenAsync(Account account)
        {
            var refreshDays = config.GetValue<double>("Jwt:RefreshTokenDays");
            var refreshToken = new RefreshToken
            {
                AccountId = account.Id,
                Token = jwt.CreateRefreshToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            db.RefreshTokens.Add(refreshToken);
            await db.SaveChangesAsync();

            return new TokenResponse
            {
                AccessToken = jwt.CreateAccessToken(account),
                RefreshToken = refreshToken.Token,
                AccountId = account.Id,
                AccountNumber = account.AccountNumber,
                FullName = account.FullName,
                Balance = account.Balance,
                HasFace = !string.IsNullOrWhiteSpace(account.FaceDescriptor)
            };
        }
    }
}
