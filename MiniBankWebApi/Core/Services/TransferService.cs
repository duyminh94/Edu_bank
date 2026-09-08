using MiniBankDTOs;
using MiniBankWebApi.Core.Entities;
using MiniBankWebApi.Core.Models;
using MiniBankWebApi.Helper;
using MiniBankWebApi.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace MiniBankWebApi.Core.Services
{
    public class TransferService : ITransferService
    {
        private readonly BankDbContext db;
        private readonly IMemoryCache cache;
        private readonly IMailService mailService;
        private readonly IFraudCheckService fraudCheckService;
        private readonly IFaceApiService faceApi;
        private readonly IConfiguration config;

        public TransferService(BankDbContext db, IMemoryCache cache, IMailService mailService,
                               IFraudCheckService fraudCheckService, IFaceApiService faceApi, IConfiguration config)
        {
            this.db = db;
            this.cache = cache;
            this.mailService = mailService;
            this.fraudCheckService = fraudCheckService;
            this.faceApi = faceApi;
            this.config = config;
        }

        public async Task<OtpSentDto> RequestAsync(int fromAccountId, TransferRequestDto dto)
        {
            if (dto.Amount <= 0) throw new ArgumentException("Amount must be greater than 0");

            var from = await db.Accounts.FindAsync(fromAccountId);
            if (from == null) throw new KeyNotFoundException("Sender account not found");

            var to = await db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == dto.ToAccountNumber);
            if (to == null) throw new KeyNotFoundException("Receiver account not found");

            if (from.Id == to.Id) throw new ArgumentException("Cannot transfer to your own account");
            if (from.Balance < dto.Amount) throw new InvalidOperationException("Insufficient balance");

            var (isSuspicious, reason) = await fraudCheckService.CheckAsync(from.Id, dto.Amount);
            if (isSuspicious && string.IsNullOrWhiteSpace(from.FaceDescriptor))
            {
                throw new InvalidOperationException(
                    $"This transfer looks unusual ({reason}) so it needs face verification, " +
                    "but your account has not registered a face yet");
            }

            var expireMinutes = config.GetValue<int>("Otp:ExpireMinutes");
            var pending = new PendingTransfer
            {
                RequestId = Guid.NewGuid().ToString(),
                FromAccountId = from.Id,
                ToAccountId = to.Id,
                ToAccountNumber = to.AccountNumber,
                Amount = dto.Amount,
                OtpCode = CreateOtpCode(),
                AttemptCount = 0,
                RequiresFace = isSuspicious,
                SuspiciousReason = reason
            };

            await mailService.SendOtpAsync(from.Email, pending.OtpCode, expireMinutes);

            cache.Set(CacheKey(pending.RequestId), pending, TimeSpan.FromMinutes(expireMinutes));

            return new OtpSentDto
            {
                RequestId = pending.RequestId,
                MaskedEmail = MaskEmail(from.Email),
                ExpireMinutes = expireMinutes,
                RequiresFace = pending.RequiresFace,
                SuspiciousReason = pending.SuspiciousReason,
                Message = isSuspicious
                    ? "This transfer looks unusual, please enter the OTP and scan your face"
                    : "OTP has been sent, please confirm to finish the transfer"
            };
        }

        public async Task<TransferResultDto> ConfirmAsync(int fromAccountId, ConfirmOtpDto dto)
        {
            if (!cache.TryGetValue(CacheKey(dto.RequestId), out PendingTransfer? pending) || pending == null)
            {
                throw new KeyNotFoundException("Transfer request not found or OTP expired");
            }

            if (pending.FromAccountId != fromAccountId)
            {
                throw new UnauthorizedAccessException("This transfer request does not belong to you");
            }

            if (pending.RequiresFace) await EnsureFaceMatchesAsync(pending.FromAccountId, dto.FaceImageBase64);

            var maxAttempt = config.GetValue<int>("Otp:MaxAttempt");
            if (pending.OtpCode != dto.OtpCode)
            {
                pending.AttemptCount++;
                if (pending.AttemptCount >= maxAttempt)
                {
                    cache.Remove(CacheKey(dto.RequestId));
                    throw new InvalidOperationException("Wrong OTP too many times, please create a new transfer");
                }

                var remain = maxAttempt - pending.AttemptCount;
                throw new ArgumentException($"Wrong OTP code, you have {remain} attempt(s) left");
            }

            cache.Remove(CacheKey(dto.RequestId));
            return await ExecuteTransferAsync(pending);
        }

        public async Task<List<TransferHistoryDto>> HistoryAsync(int accountId)
        {
            return await db.Transfers
                .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
                .OrderByDescending(t => t.TransferDate)
                .Select(t => new TransferHistoryDto
                {
                    Id = t.Id,
                    FromAccountNumber = t.FromAccount.AccountNumber,
                    ToAccountNumber = t.ToAccount.AccountNumber,
                    Amount = t.Amount,
                    TransferDate = t.TransferDate
                })
                .ToListAsync();
        }

        private async Task<TransferResultDto> ExecuteTransferAsync(PendingTransfer pending)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var from = await db.Accounts.FindAsync(pending.FromAccountId);
                var to = await db.Accounts.FindAsync(pending.ToAccountId);
                if (from == null || to == null) throw new KeyNotFoundException("Account not found");
                if (from.Balance < pending.Amount) throw new InvalidOperationException("Insufficient balance");

                from.Balance -= pending.Amount;
                to.Balance += pending.Amount;

                if (from.Balance < 0 || to.Balance < 0)
                {
                    throw new InvalidOperationException("Balance can never go negative, transfer rejected");
                }

                var transfer = new Transfer
                {
                    FromAccountId = from.Id,
                    ToAccountId = to.Id,
                    Amount = pending.Amount,
                    TransferDate = DateTime.Now
                };
                db.Transfers.Add(transfer);

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                return new TransferResultDto
                {
                    TransferId = transfer.Id,
                    Message = "Transfer successful",
                    FromAccountNumber = from.AccountNumber,
                    ToAccountNumber = to.AccountNumber,
                    Amount = pending.Amount,
                    NewBalance = from.Balance,
                    Links = new List<LinkDto>
                    {
                        new LinkDto("self", $"/api/transfers/{transfer.Id}"),
                        new LinkDto("history", "/api/transfers")
                    }
                };
            }
            catch (Exception)
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private async Task EnsureFaceMatchesAsync(int accountId, string? faceImageBase64)
        {
            if (string.IsNullOrWhiteSpace(faceImageBase64))
            {
                throw new ArgumentException("This transfer is unusual, please scan your face to confirm");
            }

            var account = await db.Accounts.FindAsync(accountId);
            var savedDescriptor = FaceHelper.FromJson(account?.FaceDescriptor);
            if (savedDescriptor == null)
            {
                throw new InvalidOperationException("Your account has not registered a face yet");
            }

            if (savedDescriptor.Length != FaceHelper.DescriptorLength)
            {
                throw new InvalidOperationException("Your saved face is from the old version, please register your face again");
            }

            var descriptor = await faceApi.GetEmbeddingAsync(faceImageBase64);

            var maxDistance = config.GetValue<double>("Face:MaxDistance");
            if (FaceHelper.Distance(descriptor, savedDescriptor) > maxDistance)
            {
                throw new ArgumentException("Face does not match, please scan again");
            }
        }

        private string CreateOtpCode()
        {
            var length = config.GetValue<int>("Otp:Length");
            var maxValue = (int)Math.Pow(10, length);
            return RandomNumberGenerator.GetInt32(maxValue).ToString().PadLeft(length, '0');
        }

        private static string CacheKey(string requestId) => $"transfer_{requestId}";

        private static string MaskEmail(string email)
        {
            var atIndex = email.IndexOf('@');
            if (atIndex <= 2) return email;
            return $"{email[..2]}***{email[atIndex..]}";
        }
    }
}
