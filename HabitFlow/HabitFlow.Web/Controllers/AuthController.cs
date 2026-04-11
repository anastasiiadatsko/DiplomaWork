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
                "ConfirmEmail",
                "Auth",
                null,
                this.Request.Scheme)!;

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

            return this.RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Logout()
        {
            this.HttpContext.Session.Clear();
            return this.RedirectToAction("Login");
        }
    }
}