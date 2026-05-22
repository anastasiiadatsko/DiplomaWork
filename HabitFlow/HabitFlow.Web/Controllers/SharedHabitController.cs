using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class SharedHabitController : Controller
    {
        private readonly ISharedHabitService sharedHabitService;

        public SharedHabitController(ISharedHabitService sharedHabitService)
        {
            this.sharedHabitService = sharedHabitService;
        }

        [HttpGet]
        public async Task<IActionResult> Accept(string token)
        {
            var result = await this.sharedHabitService.AcceptInvitationAsync(token);

            if (!result.Success)
            {
                this.ViewBag.Message = result.Error;
                this.ViewBag.IsSuccess = false;
                return this.View("InvitationResult");
            }

            this.ViewBag.Message = "Запрошення прийнято! Тепер ця звичка доступна як спільна.";
            this.ViewBag.IsSuccess = true;
            return this.View("InvitationResult");
        }

        [HttpGet]
        public async Task<IActionResult> Decline(string token)
        {
            var result = await this.sharedHabitService.DeclineInvitationAsync(token);

            if (!result.Success)
            {
                this.ViewBag.Message = result.Error;
                this.ViewBag.IsSuccess = false;
                return this.View("InvitationResult");
            }

            this.ViewBag.Message = "Запрошення відхилено.";
            this.ViewBag.IsSuccess = true;
            return this.View("InvitationResult");
        }

        [HttpGet]
        public async Task<IActionResult> Progress(Guid habitId)
        {
            var userIdString = this.HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdString))
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var userId = Guid.Parse(userIdString);

            var model = await this.sharedHabitService.GetParticipantsProgressAsync(
                habitId,
                userId);

            if (model == null || !model.Any())
            {
                return this.NotFound();
            }

            this.ViewBag.HabitId = habitId;

            return this.View(model);
        }
    }
}