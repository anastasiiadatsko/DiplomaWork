using HabitFlow.Domain.Enums;

namespace HabitFlow.Domain.Entities
{
    public class Habit
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Category { get; set; } = string.Empty;

        public FrequencyType FrequencyType { get; set; }

        public string TargetDaysJson { get; set; } = "[]";

        public DateTime StartDate { get; set; }

        public string Color { get; set; } = "#7fff6e";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;

        public ICollection<HabitLog> Logs { get; set; } = new List<HabitLog>();

        public HabitAnalytics? Analytics { get; set; }
    }
}