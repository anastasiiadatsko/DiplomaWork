using System.ComponentModel.DataAnnotations;

namespace HabitFlow.BLL.DTOs
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Введіть поточний пароль")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть новий пароль")]
        [MinLength(6, ErrorMessage = "Мінімум 6 символів")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Підтвердіть пароль")]
        [Compare("NewPassword", ErrorMessage = "Паролі не співпадають")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}