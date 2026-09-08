using MiniBankDTOs;
using MiniBankWebClient.Models;
using MiniBankWebClient.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MiniBankWebClient.Controllers
{
    public class TransferController : Controller
    {
        private readonly BankApiClient api;

        public TransferController(BankApiClient api) => this.api = api;

        [HttpGet]
        public IActionResult Index()
        {
            if (!api.IsLoggedIn) return RedirectToAction("Login", "Account");

            LoadAccountInfo();
            return View(new TransferRequestDto());
        }

        [HttpPost]
        public async Task<IActionResult> CreateRequest(TransferRequestDto model)
        {
            if (!api.IsLoggedIn) return RedirectToAction("Login", "Account");

            LoadAccountInfo();
            if (!ModelState.IsValid) return View("Index", model);

            try
            {
                var response = await api.PostAsync("/transfers/request", model);
                if (response.StatusCode == HttpStatusCode.Unauthorized) return LogoutExpired();

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = await BankApiClient.ReadErrorAsync(response);
                    return View("Index", model);
                }

                var otp = await response.Content.ReadFromJsonAsync<OtpSentDto>();
                return View("Confirm", new ConfirmOtpViewModel
                {
                    RequestId = otp!.RequestId,
                    MaskedEmail = otp.MaskedEmail,
                    RequiresFace = otp.RequiresFace,
                    SuspiciousReason = otp.SuspiciousReason,
                    Summary = $"{model.Amount:N0} to account {model.ToAccountNumber}"
                });
            }
            catch (HttpRequestException)
            {
                ViewBag.Error = "Cannot connect to the API, please make sure MiniBankWebApi is running";
                return View("Index", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Confirm(ConfirmOtpViewModel model)
        {
            if (!api.IsLoggedIn) return RedirectToAction("Login", "Account");
            if (!ModelState.IsValid) return View(model);

            try
            {
                var dto = new ConfirmOtpDto
                {
                    RequestId = model.RequestId,
                    OtpCode = model.OtpCode,
                    FaceImageBase64 = model.RequiresFace ? model.FaceImageBase64 : null
                };

                if (model.RequiresFace && string.IsNullOrWhiteSpace(dto.FaceImageBase64))
                {
                    ViewBag.Error = "This transfer is unusual, please scan your face before confirming";
                    return View(model);
                }

                var response = await api.PostAsync("/transfers/confirm", dto);
                if (response.StatusCode == HttpStatusCode.Unauthorized) return LogoutExpired();

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = await BankApiClient.ReadErrorAsync(response);
                    return View(model);
                }

                var result = await response.Content.ReadFromJsonAsync<TransferResultDto>();
                api.SaveBalance(result!.NewBalance);
                TempData["Message"] = $"Transferred {result.Amount:N0} to account {result.ToAccountNumber}";
                return RedirectToAction("History");
            }
            catch (HttpRequestException)
            {
                ViewBag.Error = "Cannot connect to the API, please make sure MiniBankWebApi is running";
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            if (!api.IsLoggedIn) return RedirectToAction("Login", "Account");

            LoadAccountInfo();

            try
            {
                var response = await api.GetAsync("/transfers");
                if (response.StatusCode == HttpStatusCode.Unauthorized) return LogoutExpired();

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = await BankApiClient.ReadErrorAsync(response);
                    return View(new List<TransferHistoryDto>());
                }

                var history = await response.Content.ReadFromJsonAsync<List<TransferHistoryDto>>();
                return View(history);
            }
            catch (HttpRequestException)
            {
                ViewBag.Error = "Cannot connect to the API, please make sure MiniBankWebApi is running";
                return View(new List<TransferHistoryDto>());
            }
        }

        private void LoadAccountInfo()
        {
            ViewBag.AccountNumber = HttpContext.Session.GetString("AccountNumber");
            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            ViewBag.Balance = HttpContext.Session.GetString("Balance");
            ViewBag.HasFace = api.HasFace;
        }

        private IActionResult LogoutExpired()
        {
            api.Clear();
            TempData["Message"] = "Your session has expired, please login again";
            return RedirectToAction("Login", "Account");
        }
    }
}
