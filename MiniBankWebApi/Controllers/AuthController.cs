using MiniBankDTOs;
using MiniBankWebApi.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MiniBankWebApi.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService service;
        private readonly ILogger<AuthController> logger;

        public AuthController(IAuthService service, ILogger<AuthController> logger)
        {
            this.service = service;
            this.logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            try
            {
                return StatusCode(201, await service.RegisterAsync(dto));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(409, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Register failed for email {Email}", dto.Email);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            try
            {
                var result = await service.LoginAsync(dto);
                return result != null ? Ok(result) : Unauthorized("Invalid account number or password");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login failed for account {AccountNumber}", dto.AccountNumber);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshDto dto)
        {
            try
            {
                var result = await service.RefreshAsync(dto.RefreshToken);
                return result != null ? Ok(result) : Unauthorized("Invalid or expired refresh token");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refresh token failed");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("face-login")]
        public async Task<IActionResult> FaceLogin(FaceLoginDto dto)
        {
            try
            {
                var result = await service.FaceLoginAsync(dto);
                return result != null ? Ok(result) : Unauthorized("Face does not match any account");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Face login failed");
                return StatusCode(500, "Internal server error");
            }
        }

        [Authorize]
        [HttpPost("face-register")]
        public async Task<IActionResult> FaceRegister(FaceRegisterDto dto)
        {
            var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            try
            {
                var success = await service.RegisterFaceAsync(accountId, dto.ImageBase64);
                return success ? Ok("Face registered successfully") : NotFound("Account not found");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Face register failed for account {AccountId}", accountId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
