using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly IAnalyticsService analyticsService;
        private readonly IHabitService habitService;

        public AnalyticsController(
            IAnalyticsService analyticsService,
            IHabitService habitService)
        {
            this.analyticsService = analyticsService;
            this.habitService = habitService;
        }

        private Guid? CurrentUserId
        {
            get
            {
                var id = this.HttpContext.Session.GetString("UserId");
                return id == null ? null : Guid.Parse(id);
            }
        }

        public async Task<IActionResult> Index(Guid habitId)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var model = await this.analyticsService
                .GetHabitAnalyticsAsync(habitId, this.CurrentUserId.Value);

            this.ViewBag.HabitId = habitId;
            return this.View(model);
        }
    }
}