using MiniBankDTOs;
using MiniBankWebClient.Models;
using MiniBankWebClient.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MiniBankWebClient.Controllers
{
    public class AccountController : Controller
    {
        private readonly BankApiClient api;

        public AccountController(BankApiClient api) => this.api = api;

        [HttpGet]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var dto = new RegisterDto
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password,
                    PhoneNumber = model.PhoneNumber,
                    DateOfBirth = model.DateOfBirth,
                    IdentityNumber = model.IdentityNumber,
                    Address = model.Address,
                    FaceImageBase64 = model.FaceImageBase64
                };

                var response = await api.PostAnonymousAsync("/auth/register", dto);
                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = await response.Content.ReadAsStringAsync();
                    return View(model);
                }

                var result = await response.Content.ReadFromJsonAsync<RegisterResultDto>();
                TempData["Message"] = $"Account created. Your account number is {result!.AccountNumber}, " +
                                      "please keep it to login.";
                return RedirectToAction("Login");
            }
            catch (HttpRequestException)
            {
                ViewBag.Error = "Cannot connect to the API, please make sure MiniBankWebApi is running";
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Login() => View(new LoginDto());

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var response = await api.PostAnonymousAsync("/auth/login", model);
                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = await response.Content.ReadAsStringAsync();
                    return View(model);
                }

                var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
                api.SaveSession(result!);
                return RedirectToAction("Index", "Transfer");
            }
            catch (HttpRequestException)
            {
                ViewBag.Error = "Cannot connect to the API, please make sure MiniBankWebApi is running";
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult FaceLogin() => View(new FaceLoginViewModel());

        [HttpPost]
        public async Task<IActionResult> FaceLogin(FaceLoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (string.IsNullOrWhiteSpace(model.ImageBase64))
            {
                ViewBag.Error = "Please scan your face before logging in";
                return View(model);
            }

            try
            {
                var dto = new FaceLoginDto { ImageBase64 = model.ImageBase64 };
                var response = await api.PostAnonymousAsync("/auth/face-login", dto);
                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = await response.Content.ReadAsStringAsync();
                    return View(model);
                }

                var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
                api.SaveSession(result!);
                return RedirectToAction("Index", "Transfer");
            }
            catch (HttpRequestException)
            {
                ViewBag.Error = "Cannot connect to the API, please make sure MiniBankWebApi is running";
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult RegisterFace()
        {
            if (!api.IsLoggedIn) return RedirectToAction("Login");

            ViewBag.AccountNumber = HttpContext.Session.GetString("AccountNumber");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegisterFace(string imageBase64)
        {
            if (!api.IsLoggedIn) return RedirectToAction("Login");

            ViewBag.AccountNumber = HttpContext.Session.GetString("AccountNumber");

            if (string.IsNullOrWhiteSpace(imageBase64))
            {
                ViewBag.Error = "Please scan your face before saving";
                return View();
            }

            try
            {
                var response = await api.PostAsync("/auth/face-register", new FaceRegisterDto { ImageBase64 = imageBase64 });
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    api.Clear();
                    TempData["Message"] = "Your session has expired, please login again";
                    return RedirectToAction("Login");
                }

                if (!response.IsSuccessStatusCode)
                {
                    ViewBag.Error = await response.Content.ReadAsStringAsync();
                    return View();
                }

                api.SaveFaceRegistered();
                TempData["Message"] = "Face registered successfully, you can now login by face";
                return RedirectToAction("Index", "Transfer");
            }
            catch (HttpRequestException)
            {
                ViewBag.Error = "Cannot connect to the API, please make sure MiniBankWebApi is running";
                return View();
            }
        }

        public IActionResult Logout()
        {
            api.Clear();
            return RedirectToAction("Login");
        }
    }
}
