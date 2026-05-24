using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class BalanceConstellationController : Controller
    {
        private readonly IBalanceConstellationService balanceConstellationService;

        public BalanceConstellationController(
            IBalanceConstellationService balanceConstellationService)
        {
            this.balanceConstellationService = balanceConstellationService;
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
        public async Task<IActionResult> Index()
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var model = await this.balanceConstellationService
                .GetConstellationAsync(this.CurrentUserId.Value);

            return this.View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetData()
        {
            if (this.CurrentUserId == null)
            {
                return this.Json(new { success = false });
            }

            var model = await this.balanceConstellationService
                .GetConstellationAsync(this.CurrentUserId.Value);

            return this.Json(new
            {
                success = true,
                data = model
            });
        }
    }
}