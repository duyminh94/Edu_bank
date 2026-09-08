using MiniBankDTOs;
using MiniBankWebApi.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MiniBankWebApi.Controllers
{
    [Authorize]
    [Route("api/transfers")]
    [ApiController]
    public class TransfersController : ControllerBase
    {
        private readonly ITransferService service;
        private readonly ILogger<TransfersController> logger;

        public TransfersController(ITransferService service, ILogger<TransfersController> logger)
        {
            this.service = service;
            this.logger = logger;
        }

        private int CurrentAccountId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("request")]
        public async Task<IActionResult> CreateRequest(TransferRequestDto dto)
        {
            try
            {
                return Ok(await service.RequestAsync(CurrentAccountId, dto));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transfer request failed for account {AccountId}", CurrentAccountId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm(ConfirmOtpDto dto)
        {
            try
            {
                return Ok(await service.ConfirmAsync(CurrentAccountId, dto));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(409, new { error = "Your balance was changed by another transfer, please try again" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transfer confirm failed for account {AccountId}", CurrentAccountId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            try
            {
                return Ok(await service.HistoryAsync(CurrentAccountId));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Load history failed for account {AccountId}", CurrentAccountId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
