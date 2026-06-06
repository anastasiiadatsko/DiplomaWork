using System.ComponentModel.DataAnnotations;
using HabitFlow.Domain.Enums;

namespace HabitFlow.BLL.DTOs
{
    public class CreateHabitDto
    {
        [Required(ErrorMessage = "Введіть назву")]
        [StringLength(200, ErrorMessage = "Максимум 200 символів")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Оберіть категорію")]
        public string Category { get; set; } = string.Empty;

        public HabitMode Mode { get; set; } = HabitMode.Form;

        public QuitCategory? QuitCategory { get; set; }

        public FrequencyType FrequencyType { get; set; } = FrequencyType.Daily;

        public List<DayOfWeek> TargetDays { get; set; } = new();

        public string Color { get; set; } = "#16a34a";

        public TimeOnly? ReminderTime { get; set; }

        public string? FriendEmail { get; set; }

        public bool AddToGoogleCalendar { get; set; }
    }
}