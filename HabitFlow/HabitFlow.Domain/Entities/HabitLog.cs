using HabitFlow.Domain.Enums;

namespace HabitFlow.Domain.Entities
{
    public class HabitLog
    {
        public Guid Id { get; set; }

        public Guid HabitId { get; set; }

        public Guid UserId { get; set; }

        public DateTime ScheduledDate { get; set; }

        public DateTime? CompletedAt { get; set; }

        public LogStatus Status { get; set; } = LogStatus.Pending;

        public string? Note { get; set; }

        public Habit Habit { get; set; } = null!;
    }
}