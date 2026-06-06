using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService authService;

        public AuthController(IAuthService authService)
        {
            this.authService = authService;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(dto);
            }

            var confirmationLink = this.Url.Action(
                "ConfirmEmail", "Auth", null, this.Request.Scheme)!;

            var (success, error) = await this.authService.RegisterAsync(dto, confirmationLink);

            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(dto);
            }

            return this.RedirectToAction("RegisterSuccess");
        }

        [HttpGet]
        public IActionResult RegisterSuccess()
        {
            return this.View();
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string token, string email)
        {
            var (success, error) = await this.authService.ConfirmEmailAsync(email, token);

            if (!success)
            {
                this.ViewBag.Error = error;
                return this.View("ConfirmEmailError");
            }

            return this.View("ConfirmEmailSuccess");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(dto);
            }

            var (success, error, user) = await this.authService.LoginAsync(dto);

            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(dto);
            }

            this.HttpContext.Session.SetString("UserId", user!.Id.ToString());
            this.HttpContext.Session.SetString("UserName", user.Name);
            this.HttpContext.Session.SetString("UserEmail", user.Email);

            // Якщо онбординг не пройдений — направляємо туди
            if (!user.IsOnboardingCompleted)
            {
                return this.RedirectToAction("Index", "Onboarding");
            }

            return this.RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(dto);
            }

            var resetLink = this.Url.Action(
                "ResetPassword", "Auth", null, this.Request.Scheme)!;

            await this.authService.ForgotPasswordAsync(dto.Email, resetLink);

            return this.RedirectToAction("ForgotPasswordSuccess");
        }

        [HttpGet]
        public IActionResult ForgotPasswordSuccess()
        {
            return this.View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            var dto = new ResetPasswordDto { Token = token, Email = email };
            return this.View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(dto);
            }

            var (success, error) = await this.authService.ResetPasswordAsync(dto);

            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(dto);
            }

            return this.RedirectToAction("ResetPasswordSuccess");
        }

        [HttpGet]
        public IActionResult ResetPasswordSuccess()
        {
            return this.View();
        }

        public IActionResult Logout()
        {
            this.HttpContext.Session.Clear();
            return this.RedirectToAction("Login");
        }
    }
}