namespace HabitFlow.BLL.DTOs
{
    public class SharedHabitParticipantProgressDto
    {
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string UserEmail { get; set; } = string.Empty;

        public bool IsOwner { get; set; }

        public bool IsCompletedToday { get; set; }

        public int CurrentStreak { get; set; }

        public int TotalCompleted { get; set; }

        public double ConsistencyRate { get; set; }
    }
}