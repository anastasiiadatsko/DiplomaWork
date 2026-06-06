using System.ComponentModel.DataAnnotations;

namespace HabitFlow.BLL.DTOs
{
    public class ResetPasswordDto
    {
        public string Token { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть новий пароль")]
        [MinLength(6, ErrorMessage = "Мінімум 6 символів")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Підтвердіть пароль")]
        [Compare("NewPassword", ErrorMessage = "Паролі не співпадають")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}