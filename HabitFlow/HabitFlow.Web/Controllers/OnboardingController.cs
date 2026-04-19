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
            return this.RedirectToAction("Index", "Dashboard");
        }
    }
}