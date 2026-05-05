namespace HabitFlow.BLL.DTOs
{
    public class AnalyticsViewModel
    {
        // Базові метрики
        public string HabitName { get; set; } = string.Empty;
        public string HabitColor { get; set; } = string.Empty;
        public int DaysSinceStart { get; set; }
        public int TotalCompleted { get; set; }
        public int CurrentStreak { get; set; }
        public int MaxStreak { get; set; }
        public double ConsistencyRate { get; set; }

        // МНК — апроксимація
        public List<MnkDataPoint> MnkPoints { get; set; } = new();
        public List<MnkDataPoint> MnkTrendLine { get; set; } = new();
        public double MnkA0 { get; set; }
        public double MnkA1 { get; set; }
        public double MnkA2 { get; set; }
        public int PredictedDaysToForm { get; set; }
        public DateTime PredictedFormationDate { get; set; }

        // Марківські ланцюги
        public double MarkovProbCompleted { get; set; }
        public double MarkovProbSkipped { get; set; }
        public double[][] TransitionMatrix { get; set; } = Array.Empty<double[]>();
        public List<double> Next7DaysProbabilities { get; set; } = new();
        public double BreakRisk { get; set; }

        // HSS з градієнтним спуском
        public double HabitStrengthScore { get; set; }
        public double AlphaWeight { get; set; }
        public double BetaWeight { get; set; }
        public double GammaWeight { get; set; }

        // Теорія ігор — мінімакс
        public List<WeekdayRisk> WeekdayRisks { get; set; } = new();
        public string OptimalDayToAct { get; set; } = string.Empty;
        public string MostRiskyDay { get; set; } = string.Empty;

        // Дані для графіків
        public List<DailyLogPoint> DailyLogs { get; set; } = new();
        public List<WeekdayStats> WeekdayStats { get; set; } = new();
    }

    public class MnkDataPoint
    {
        public int Day { get; set; }
        public double Value { get; set; }
    }

    public class WeekdayRisk
    {
        public string DayName { get; set; } = string.Empty;
        public double CompletionRate { get; set; }
        public double RiskScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
    }

    public class DailyLogPoint
    {
        public DateTime Date { get; set; }
        public bool Completed { get; set; }
        public int CumulativeStreak { get; set; }
    }

    public class WeekdayStats
    {
        public string Day { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Completed { get; set; }
        public double Rate { get; set; }
    }
}