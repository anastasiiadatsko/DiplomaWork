namespace HabitFlow.Domain.Entities
{
    public class HabitParticipant
    {
        public Guid Id { get; set; }

        public Guid HabitId { get; set; }

        public Guid UserId { get; set; }

        public bool IsOwner { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public Habit Habit { get; set; } = null!;

        public User User { get; set; } = null!;
    }
}
