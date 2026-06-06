using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class GoogleCalendarController : Controller
    {
        private readonly IGoogleCalendarService googleCalendarService;

        public GoogleCalendarController(IGoogleCalendarService googleCalendarService)
        {
            this.googleCalendarService = googleCalendarService;
        }

        [HttpGet]
        public IActionResult Connect()
        {
            var userIdText = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrWhiteSpace(userIdText))
            {
                return RedirectToAction("Login", "Auth");
            }

            var userId = Guid.Parse(userIdText);
            var authorizationUrl = this.googleCalendarService.BuildAuthorizationUrl(userId);

            return Redirect(authorizationUrl);
        }

        [HttpGet]
        public async Task<IActionResult> Callback(string code, string state, string? error = null)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                TempData["Error"] = "Google Calendar не підключено.";
                return RedirectToAction("Index", "Profile");
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                TempData["Error"] = "Некоректна відповідь від Google.";
                return RedirectToAction("Index", "Profile");
            }

            var userId = Guid.Parse(state);
            var success = await this.googleCalendarService.HandleCallbackAsync(userId, code);

            TempData[success ? "Success" : "Error"] = success
                ? "Google Calendar успішно підключено."
                : "Не вдалося підключити Google Calendar.";

            return RedirectToAction("Index", "Profile");
        }

        [HttpPost]
        public async Task<IActionResult> Disconnect()
        {
            var userIdText = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrWhiteSpace(userIdText))
            {
                return RedirectToAction("Login", "Auth");
            }

            var userId = Guid.Parse(userIdText);
            var success = await this.googleCalendarService.DisconnectAsync(userId);

            TempData[success ? "Success" : "Error"] = success
                ? "Google Calendar відключено."
                : "Не вдалося відключити Google Calendar.";

            return RedirectToAction("Index", "Profile");
        }
    }
}