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
    }

    public class Achievement
    {
        public string Icon { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsUnlocked { get; set; }
    }
}