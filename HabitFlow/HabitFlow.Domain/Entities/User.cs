namespace HabitFlow.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string TimeZone { get; set; } = "UTC";

        public string Role { get; set; } = "User";

        public bool IsEmailConfirmed { get; set; } = false;

        public string? EmailConfirmationToken { get; set; }

        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetTokenExpiry { get; set; }

        public bool NotificationsEnabled { get; set; } = true;

        public int AvatarId { get; set; } = 1;

        public bool IsOnboardingCompleted { get; set; } = false;

        public string? OnboardingGoal { get; set; }

        public string? OnboardingDescription { get; set; }

        public string? OnboardingTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Habit> Habits { get; set; } = new List<Habit>();

        public ICollection<HabitParticipant> HabitParticipants { get; set; } = new List<HabitParticipant>();

        public ICollection<HabitInvitation> SentHabitInvitations { get; set; } = new List<HabitInvitation>();

        public ICollection<HabitInvitation> ReceivedHabitInvitations { get; set; } = new List<HabitInvitation>();
    }
}