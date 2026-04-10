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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Habit> Habits { get; set; } = new List<Habit>();
    }
}