using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class HabitController : Controller
    {
        private readonly IHabitService habitService;

        public HabitController(IHabitService habitService)
        {
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

        public async Task<IActionResult> Index()
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var habits = await this.habitService.GetAllHabitsAsync(this.CurrentUserId.Value);
            return this.View(habits);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            return this.View(new CreateHabitDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateHabitDto dto, string[] targetDays)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            dto.TargetDays = targetDays
                .Select(d => Enum.Parse<DayOfWeek>(d))
                .ToList();

            if (!this.ModelState.IsValid)
            {
                return this.View(dto);
            }

            await this.habitService.CreateHabitAsync(this.CurrentUserId.Value, dto);
            this.TempData["Success"] = "Звичку створено!";
            return this.RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var habit = await this.habitService.GetByIdAsync(id, this.CurrentUserId.Value);
            if (habit == null)
            {
                return this.NotFound();
            }

            var dto = new CreateHabitDto
            {
                Name = habit.Name,
                Description = habit.Description,
                Category = habit.Category,
                FrequencyType = habit.FrequencyType,
                TargetDays = habit.TargetDays,
                Color = habit.Color,
            };

            this.ViewBag.HabitId = id;
            return this.View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Guid id, CreateHabitDto dto, string[] targetDays)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            dto.TargetDays = targetDays
                .Select(d => Enum.Parse<DayOfWeek>(d))
                .ToList();

            await this.habitService.UpdateHabitAsync(id, this.CurrentUserId.Value, dto);
            this.TempData["Success"] = "Звичку оновлено!";
            return this.RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            await this.habitService.DeleteHabitAsync(id, this.CurrentUserId.Value);
            this.TempData["Success"] = "Звичку видалено!";
            return this.RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Pause(Guid id)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            await this.habitService.PauseHabitAsync(id, this.CurrentUserId.Value);
            return this.RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> ManualLog(Guid id)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var habit = await this.habitService.GetByIdAsync(id, this.CurrentUserId.Value);
            if (habit == null)
            {
                return this.NotFound();
            }

            this.ViewBag.HabitId = id;
            this.ViewBag.HabitName = habit.Name;
            this.ViewBag.HabitColor = habit.Color;
            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> ManualLog(ManualLogDto dto)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            await this.habitService.ManualLogAsync(this.CurrentUserId.Value, dto);
            this.TempData["Success"] = $"Виконання за {dto.Date:dd.MM.yyyy} додано!";
            return this.RedirectToAction("ManualLog", new { id = dto.HabitId });
        }

        [HttpPost]
        public async Task<IActionResult> ManualLogRange(
            Guid habitId, DateTime fromDate, DateTime toDate)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            await this.habitService.ManualLogRangeAsync(
                this.CurrentUserId.Value, habitId, fromDate, toDate);

            var days = (toDate.Date - fromDate.Date).Days + 1;
            this.TempData["Success"] = $"Додано {days} днів виконання!";
            return this.RedirectToAction("ManualLog", new { id = habitId });
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(Guid id)
        {
            if (this.CurrentUserId == null)
            {
                return this.Json(new { success = false });
            }

            var completed = await this.habitService
                .ToggleCompletionAsync(id, this.CurrentUserId.Value);

            return this.Json(new { success = true, completed });
        }
    }
}