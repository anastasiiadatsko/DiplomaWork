namespace HabitFlow.BLL.DTOs
{
    public class BalanceConstellationViewModel
    {
        public List<BalanceConstellationAreaDto> Areas { get; set; } = new();

        public string WeakestAreaName { get; set; } = string.Empty;

        public string MostActiveAreaName { get; set; } = string.Empty;

        public double AverageSelfScore { get; set; }

        public double AverageActivityScore { get; set; }

        public string MainInsight { get; set; } = string.Empty;

        public string RecommendationText { get; set; } = string.Empty;
    }

    public class BalanceConstellationAreaDto
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Emoji { get; set; } = string.Empty;

        public int SelfScore { get; set; }

        public double ActivityScore { get; set; }

        public int HabitsCount { get; set; }

        public int CompletedCount { get; set; }

        public double ConsistencyRate { get; set; }

        public bool IsWeakest { get; set; }

        public bool IsMostActive { get; set; }

        public string StatusText { get; set; } = string.Empty;
    }
}