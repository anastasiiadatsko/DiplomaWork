using HabitFlow.Domain.Enums;

namespace HabitFlow.Domain.Entities
{
    public class TriggerLog
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public int Intensity { get; set; }

        public TriggerType TriggerType { get; set; }

        public bool DidRelapse { get; set; }

        public string? Note { get; set; }

        public string? Location { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}