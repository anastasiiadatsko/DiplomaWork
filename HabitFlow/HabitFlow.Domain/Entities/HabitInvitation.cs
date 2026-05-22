using HabitFlow.Domain.Enums;

namespace HabitFlow.Domain.Entities
{
    public class HabitInvitation
    {
        public Guid Id { get; set; }

        public Guid HabitId { get; set; }

        public Guid InviterUserId { get; set; }

        public Guid InviteeUserId { get; set; }

        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

        public string Token { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(3);

        public Habit Habit { get; set; } = null!;

        public User InviterUser { get; set; } = null!;

        public User InviteeUser { get; set; } = null!;
    }
}