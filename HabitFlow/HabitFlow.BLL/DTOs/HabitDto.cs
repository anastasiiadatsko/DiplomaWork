using HabitFlow.Domain.Enums;

namespace HabitFlow.BLL.DTOs
{
    public class HabitDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Category { get; set; } = string.Empty;

        public FrequencyType FrequencyType { get; set; }

        public List<DayOfWeek> TargetDays { get; set; } = new();

        public string Color { get; set; } = "#16a34a";

        public bool IsCompletedToday { get; set; }

        public int CurrentStreak { get; set; }

        public double ConsistencyRate { get; set; }

        public DateTime StartDate { get; set; }

        public bool IsActive { get; set; }
    }
}