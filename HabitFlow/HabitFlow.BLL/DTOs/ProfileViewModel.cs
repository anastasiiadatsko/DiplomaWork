using HabitFlow.Domain.Entities;

namespace HabitFlow.BLL.DTOs
{
    public class ProfileViewModel
    {
        public User User { get; set; } = null!;

        public int TotalCompletedHabits { get; set; }

        public int TotalHabits { get; set; }

        public int StarLevel { get; set; }

        public string StarLevelName { get; set; } = string.Empty;

        public int NextLevelAt { get; set; }

        public int ProgressPercent { get; set; }

        public List<Achievement> Achievements { get; set; } = new();

        public BalanceWheelViewModel? BalanceWheel { get; set; }

        public string? StrongestBalanceArea { get; set; }

        public string? WeakestBalanceArea { get; set; }

        public double BalanceAverage { get; set; }
    }

    public class Achievement
    {
        public string Icon { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsUnlocked { get; set; }
    }

    public class BalanceWheelViewModel
    {
        public int Health { get; set; }

        public int Career { get; set; }

        public int Finance { get; set; }

        public int Relationships { get; set; }

        public int SelfDevelopment { get; set; }

        public int Rest { get; set; }

        public int EmotionalState { get; set; }

        public int Environment { get; set; }
    }
}