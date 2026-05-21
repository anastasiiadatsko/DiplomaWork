using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    [Route("Coach")]
    public class CoachController : Controller
    {
        private readonly ICoachService coachService;
        private readonly IHabitService habitService;

        public CoachController(
            ICoachService coachService,
            IHabitService habitService)
        {
            this.coachService = coachService;
            this.habitService = habitService;
        }


        [HttpGet("Session/{habitId:guid}")]
        public async Task<IActionResult> Session(Guid habitId)
        {
            var userId = this.GetUserId();

            if (userId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var habit = await this.habitService.GetByIdAsync(habitId, userId.Value);

            if (habit == null)
            {
                return this.NotFound();
            }

            var sessionType = await this.coachService
                .DetectSessionTypeAsync(habitId, userId.Value);

            var session = await this.coachService
                .GetSessionQuestionsAsync(habitId, userId.Value, sessionType);

            this.ViewBag.HabitId = habitId;
            this.ViewBag.HabitName = habit.Name;

            return this.View("Session", session);
        }

        [HttpGet("Session/{habitId:guid}/{sessionType}")]
        public async Task<IActionResult> SessionByType(
    Guid habitId, CoachSessionType sessionType)
        {
            var userId = this.GetUserId();

            if (userId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var habit = await this.habitService.GetByIdAsync(habitId, userId.Value);

            if (habit == null)
            {
                return this.NotFound();
            }

            var session = await this.coachService
                .GetSessionQuestionsAsync(habitId, userId.Value, sessionType);

            this.ViewBag.HabitId = habitId;
            this.ViewBag.HabitName = habit.Name;

            return this.View("Session", session);
        }

        [HttpPost("Advice")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Advice([FromBody] CoachAdviceRequest request)
        {
            var userId = this.GetUserId();
            if (userId == null)
                return this.Json(new { error = "Не авторизовано" });

            if (request?.HabitId == Guid.Empty)
                return this.BadRequest("HabitId is required");

            var result = await this.coachService
                .GetAdviceAsync(userId.Value, request!);
            return this.Json(result);
        }

        [HttpPost("Chat")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Chat([FromBody] CoachAdviceRequest request)
        {
            var userId = this.GetUserId();
            if (userId == null)
                return this.Json(new { error = "Не авторизовано" });

            request.SessionType = CoachSessionType.FreeChat;
            var result = await this.coachService
                .GetAdviceAsync(userId.Value, request);
            return this.Json(result);
        }

        private Guid? GetUserId()
        {
            var value = this.HttpContext.Session.GetString("UserId");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}