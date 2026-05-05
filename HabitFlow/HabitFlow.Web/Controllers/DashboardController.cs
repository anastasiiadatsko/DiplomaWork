using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IHabitService habitService;
        private readonly IRecommendationService recommendationService;

        public DashboardController(IHabitService habitService,
            IRecommendationService recommendationService)
        {
            this.habitService = habitService;
            this.recommendationService = recommendationService;
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
            ViewBag.Recommendations = await this.recommendationService
                .GetRecommendationsAsync(Guid.Parse(userId));

            return this.View(model);
        }
    }
}