namespace HabitFlow.BLL.DTOs
{
    public class QuitAnalyticsViewModel
    {
        public int CleanDays { get; set; }

        public int RelapseCount { get; set; }

        public int WonCravingsCount { get; set; }

        public int TotalCravingsCount { get; set; }

        public double AverageCravingIntensity { get; set; }

        public string MostDangerousTime { get; set; } = "Недостатньо даних";

        public List<QuitTriggerStatsDto> MostDangerousTriggers { get; set; } = new();

        public double RelapseRisk { get; set; }

        public string MainInsight { get; set; } = string.Empty;

        public string ActionTip { get; set; } = string.Empty;
    }
}