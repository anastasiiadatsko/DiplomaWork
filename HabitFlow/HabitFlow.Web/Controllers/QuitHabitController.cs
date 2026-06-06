using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class QuitHabitController : Controller
    {
        private readonly IQuitHabitService quitHabitService;

        public QuitHabitController(IQuitHabitService quitHabitService)
        {
            this.quitHabitService = quitHabitService;
        }

        private Guid? CurrentUserId
        {
            get
            {
                var id = this.HttpContext.Session.GetString("UserId");
                return id == null ? null : Guid.Parse(id);
            }
        }

        [HttpPost]
        public async Task<IActionResult> LogCleanDay(Guid habitId)
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            await this.quitHabitService.LogCleanDayAsync(habitId, this.CurrentUserId.Value);
            this.TempData["Success"] = "Чистий день зафіксовано!";
            return this.RedirectToAction("Index", "Habit");
        }

        [HttpPost]
        public async Task<IActionResult> LogCraving(Guid habitId, LogCravingDto dto)
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            await this.quitHabitService.LogCravingAsync(habitId, this.CurrentUserId.Value, dto);
            this.TempData["Success"] = "Потяг зафіксовано. Ти впоралась!";
            return this.RedirectToAction("Index", "Habit");
        }

        [HttpPost]
        public async Task<IActionResult> LogRelapse(Guid habitId)
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            await this.quitHabitService.LogRelapseAsync(habitId, this.CurrentUserId.Value, new LogRelapseDto
            {
                CravingLevel = 10,
                TriggerType = TriggerType.Other,
            });
            this.TempData["Success"] = "Зрив зафіксовано. Продовжуємо далі.";
            return this.RedirectToAction("Index", "Dashboard");
        }
    }
}