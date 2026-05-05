using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IHabitService habitService;

        public DashboardController(IHabitService habitService)
        {
            this.habitService = habitService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = this.HttpContext.Session.GetString("UserId");
            if (userId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var userName = this.HttpContext.Session.GetString("UserName") ?? "Користувач";
            var model = await this.habitService.GetDashboardAsync(
                Guid.Parse(userId), userName);

            return this.View(model);
        }
    }
}