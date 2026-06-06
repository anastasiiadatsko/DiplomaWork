using HabitFlow.Domain.Enums;

namespace HabitFlow.BLL.DTOs
{
    public class QuitTriggerStatsDto
    {
        public TriggerType TriggerType { get; set; }
        public string TriggerName { get; set; } = string.Empty;
        public int Count { get; set; }
        public int RelapseCount { get; set; }
        public double AverageIntensity { get; set; }
        public double RiskPercent { get; set; }
    }
}