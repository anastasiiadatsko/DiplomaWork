using System.ComponentModel.DataAnnotations;

namespace HabitFlow.BLL.DTOs
{
    public class EditProfileDto
    {
        [Required(ErrorMessage = "Введіть ім'я")]
        [StringLength(100, ErrorMessage = "Максимум 100 символів")]
        public string Name { get; set; } = string.Empty;

        public string TimeZone { get; set; } = "UTC";

        public bool NotificationsEnabled { get; set; }
    }
}