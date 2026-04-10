using System.ComponentModel.DataAnnotations;

namespace HabitFlow.BLL.DTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Введіть email")]
        [EmailAddress(ErrorMessage = "Невірний формат email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть пароль")]
        public string Password { get; set; } = string.Empty;
    }
}