namespace HabitFlow.BLL.DTOs
{
    public class DashboardViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public List<HabitDto> TodayHabits { get; set; } = new();
        public int TotalHabits { get; set; }
        public int CompletedToday { get; set; }
        public int TotalCompleted { get; set; }
        public int LongestStreak { get; set; }
        public double OverallConsistencyRate { get; set; }
        public List<HeatmapDay> HeatmapData { get; set; } = new();
        public List<HeatmapDay> QuitHeatmapData { get; set; } = new();
    }

    public class HeatmapDay
    {
        public DateTime Date { get; set; }
        public int CompletedCount { get; set; }
        public int Level { get; set; }
    }
}