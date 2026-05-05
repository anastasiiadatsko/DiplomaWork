using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class OnboardingController : Controller
    {
        private readonly IUserService userService;

        public OnboardingController(IUserService userService)
        {
            this.userService = userService;
        }

        private Guid? CurrentUserId
        {
            get
            {
                var id = this.HttpContext.Session.GetString("UserId");
                return id == null ? null : Guid.Parse(id);
            }
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(OnboardingDto dto)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            await this.userService.SaveOnboardingAsync(this.CurrentUserId.Value, dto);
            return this.RedirectToAction("BalanceWheel");
        }

        [HttpGet]
        public IActionResult BalanceWheel()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (userId == null)
            {
                return this.RedirectToAction("Login", "Account");
            }

            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> BalanceWheel(BalanceWheelDto dto)
        {
            if (!ModelState.IsValid)
            {
                return this.View(dto);
            }

            var userId = HttpContext.Session.GetString("UserId");
            if (userId == null)
            {
                return this.RedirectToAction("Login", "Account");
            }

            await this.userService.SaveBalanceWheelAsync(Guid.Parse(userId), dto);

            return this.RedirectToAction("Index", "Dashboard");
        }
    }
}