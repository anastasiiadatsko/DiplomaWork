using System.ComponentModel.DataAnnotations;

namespace HabitFlow.BLL.DTOs
{
    public class ForgotPasswordDto
    {
        [Required(ErrorMessage = "Введіть email")]
        [EmailAddress(ErrorMessage = "Невірний формат email")]
        public string Email { get; set; } = string.Empty;
    }
}