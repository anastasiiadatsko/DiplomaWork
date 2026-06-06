namespace HabitFlow.Domain.Entities
{
    public class HabitAnalytics
    {
        public Guid Id { get; set; }

        public Guid HabitId { get; set; }

        public int CurrentStreak { get; set; }

        public int MaxStreak { get; set; }

        public double ConsistencyRate { get; set; }

        public double HabitStrengthScore { get; set; }

        public double VolatilityIndex { get; set; }

        public int EstimatedDaysToForm { get; set; }

        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        public Habit Habit { get; set; } = null!;
    }
}