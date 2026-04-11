using System.ComponentModel.DataAnnotations;

namespace HabitFlow.BLL.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Введіть ім'я")]
        [StringLength(100, ErrorMessage = "Максимум 100 символів")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть email")]
        [EmailAddress(ErrorMessage = "Невірний формат email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть пароль")]
        [MinLength(6, ErrorMessage = "Мінімум 6 символів")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Підтвердіть пароль")]
        [Compare("Password", ErrorMessage = "Паролі не співпадають")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}