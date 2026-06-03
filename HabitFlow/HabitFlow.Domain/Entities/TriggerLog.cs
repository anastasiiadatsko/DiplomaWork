using HabitFlow.Domain.Enums;

namespace HabitFlow.Domain.Entities
{
    public class TriggerLog
    {
        public Guid Id { get; set; }

        public Guid HabitId { get; set; }

        public Guid UserId { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public string? TimeOfDay { get; set; }

        public string? Location { get; set; }

        public string? EmotionalState { get; set; }

        public int CravingLevel { get; set; }

        public TriggerType TriggerType { get; set; }

        public bool DidRelapse { get; set; }

        public bool Resisted { get; set; }

        public string? Note { get; set; }

        public Habit Habit { get; set; } = null!;

        public User User { get; set; } = null!;
    }
}