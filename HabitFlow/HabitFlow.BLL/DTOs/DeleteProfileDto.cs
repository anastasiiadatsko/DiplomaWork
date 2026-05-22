using System.ComponentModel.DataAnnotations;

namespace HabitFlow.BLL.DTOs
{
    public class DeleteProfileDto
    {
        [Required(ErrorMessage = "Введіть пароль")]
        public string Password { get; set; } = string.Empty;
    }
}