using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class ProfileController : Controller
    {
        private readonly IUserService userService;

        public ProfileController(IUserService userService)
        {
            this.userService = userService;
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

            var model = await this.userService.GetProfileAsync(this.CurrentUserId.Value);
            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(EditProfileDto dto)
        {
            if (!this.ModelState.IsValid)
            {
                return this.RedirectToAction("Index");
            }

            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var (success, error) = await this.userService
                .EditProfileAsync(this.CurrentUserId.Value, dto);

            if (success)
            {
                this.HttpContext.Session.SetString("UserName", dto.Name);
                this.TempData["Success"] = "Профіль оновлено!";
            }
            else
            {
                this.TempData["Error"] = error;
            }

            return this.RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            if (!this.ModelState.IsValid)
            {
                return this.RedirectToAction("Index");
            }

            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var (success, error) = await this.userService
                .ChangePasswordAsync(this.CurrentUserId.Value, dto);

            if (success)
            {
                this.TempData["Success"] = "Пароль змінено!";
            }
            else
            {
                this.TempData["Error"] = error;
            }

            return this.RedirectToAction("Index");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGoal(OnboardingDto dto)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(dto.Goal))
            {
                this.TempData["Error"] = "Введи ціль.";
                return this.RedirectToAction("Index");
            }

            await this.userService.SaveOnboardingAsync(this.CurrentUserId.Value, dto);

            this.TempData["Success"] = "Ціль оновлено!";
            return this.RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProfile(DeleteProfileDto dto)
        {
            if (this.CurrentUserId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            if (!this.ModelState.IsValid)
            {
                this.TempData["Error"] = "Введи пароль для видалення профілю.";
                return this.RedirectToAction("Index");
            }

            var (success, error) = await this.userService.DeleteProfileAsync(
                this.CurrentUserId.Value,
                dto);

            if (!success)
            {
                this.TempData["Error"] = error;
                return this.RedirectToAction("Index");
            }

            this.HttpContext.Session.Clear();
            this.TempData["Success"] = "Профіль видалено.";

            return this.RedirectToAction("Login", "Auth");
        }
    }
}