using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class HabitController : Controller
    {
        private readonly IHabitService habitService;
        private readonly ISharedHabitService sharedHabitService;

        public HabitController(
            IHabitService habitService,
            ISharedHabitService sharedHabitService)
        {
            this.habitService = habitService;
            this.sharedHabitService = sharedHabitService;
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
                return this.RedirectToAction("Login", "Auth");

            var habits = await this.habitService.GetAllHabitsAsync(this.CurrentUserId.Value);
            return this.View(habits);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            return this.View(new CreateHabitDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateHabitDto dto, string[] targetDays)
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            dto.TargetDays = targetDays
                .Select(d => Enum.Parse<DayOfWeek>(d))
                .ToList();

            if (!this.ModelState.IsValid)
                return this.View(dto);

            var habitId = await this.habitService.CreateHabitAsync(this.CurrentUserId.Value, dto);

            if (!string.IsNullOrWhiteSpace(dto.FriendEmail))
            {
                var acceptLink = this.Url.Action("Accept", "SharedHabit", null, this.Request.Scheme)!;
                var declineLink = this.Url.Action("Decline", "SharedHabit", null, this.Request.Scheme)!;

                var inviteResult = await this.sharedHabitService.InviteFriendAsync(
                    habitId,
                    this.CurrentUserId.Value,
                    dto.FriendEmail,
                    acceptLink,
                    declineLink);

                if (!inviteResult.Success)
                {
                    this.TempData["Error"] = inviteResult.Error;
                    return this.RedirectToAction("Index");
                }

                this.TempData["Success"] = "Звичку створено, запрошення другу надіслано!";
                return this.RedirectToAction("Index");
            }

            this.TempData["Success"] = "Звичку створено!";
            return this.RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            var habit = await this.habitService.GetByIdAsync(id, this.CurrentUserId.Value);
            if (habit == null)
                return this.NotFound();

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
                return this.RedirectToAction("Login", "Auth");

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
                return this.RedirectToAction("Login", "Auth");

            await this.habitService.DeleteHabitAsync(id, this.CurrentUserId.Value);
            this.TempData["Success"] = "Звичку видалено!";
            return this.RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Pause(Guid id)
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            await this.habitService.PauseHabitAsync(id, this.CurrentUserId.Value);
            return this.RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> ManualLog(Guid habitId, Guid? id = null)
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            if (habitId == Guid.Empty && id.HasValue)
                habitId = id.Value;

            if (habitId == Guid.Empty)
                return this.RedirectToAction("Index");

            var habit = await this.habitService.GetByIdAsync(habitId, this.CurrentUserId.Value);
            if (habit == null)
                return this.NotFound();

            this.ViewBag.HabitId = habitId;
            this.ViewBag.HabitName = habit.Name;
            this.ViewBag.HabitColor = habit.Color;
            this.ViewBag.HabitStartDate = habit.StartDate.Date.ToString("yyyy-MM-dd");

            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> ManualLog(ManualLogDto dto)
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            if (dto.HabitId == Guid.Empty)
                return this.RedirectToAction("Index");

            var habit = await this.habitService.GetByIdAsync(dto.HabitId, this.CurrentUserId.Value);
            if (habit == null)
                return this.NotFound();

            if (dto.Date.Date > DateTime.Today)
            {
                this.TempData["Error"] = "Не можна додавати виконання на майбутню дату.";
                return this.RedirectToAction("ManualLog", new { habitId = dto.HabitId });
            }

            if (dto.Date.Date < habit.StartDate.Date)
            {
                this.TempData["Error"] = "Не можна додавати виконання раніше дати створення звички.";
                return this.RedirectToAction("ManualLog", new { habitId = dto.HabitId });
            }

            await this.habitService.ManualLogAsync(this.CurrentUserId.Value, dto);

            this.TempData["Success"] = $"Виконання за {dto.Date:dd.MM.yyyy} додано!";
            return this.RedirectToAction("ManualLog", new { habitId = dto.HabitId });
        }

        [HttpPost]
        public async Task<IActionResult> ManualLogRange(
            Guid habitId, DateTime fromDate, DateTime toDate)
        {
            if (this.CurrentUserId == null)
                return this.RedirectToAction("Login", "Auth");

            if (habitId == Guid.Empty)
                return this.RedirectToAction("Index");

            var habit = await this.habitService.GetByIdAsync(habitId, this.CurrentUserId.Value);
            if (habit == null)
                return this.NotFound();

            if (fromDate.Date > toDate.Date)
            {
                this.TempData["Error"] = "Дата «Від» не може бути пізніше дати «До».";
                return this.RedirectToAction("ManualLog", new { habitId });
            }

            if (toDate.Date > DateTime.Today)
                toDate = DateTime.Today;

            if (fromDate.Date < habit.StartDate.Date)
                fromDate = habit.StartDate.Date;

            if (fromDate.Date > toDate.Date)
            {
                this.TempData["Error"] = "Обраний діапазон не входить у період існування звички.";
                return this.RedirectToAction("ManualLog", new { habitId });
            }

            await this.habitService.ManualLogRangeAsync(
                this.CurrentUserId.Value, habitId, fromDate, toDate);

            var days = (toDate.Date - fromDate.Date).Days + 1;
            this.TempData["Success"] = $"Додано {days} днів виконання!";

            return this.RedirectToAction("ManualLog", new { habitId });
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(Guid id)
        {
            if (this.CurrentUserId == null)
                return this.Json(new { success = false });

            var completed = await this.habitService
                .ToggleCompletionAsync(id, this.CurrentUserId.Value);

            return this.Json(new { success = true, completed });
        }
    }
}