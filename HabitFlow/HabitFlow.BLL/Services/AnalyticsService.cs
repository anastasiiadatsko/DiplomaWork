using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace HabitFlow.BLL.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IHabitRepository habitRepository;
        private readonly IHabitLogRepository habitLogRepository;
        private readonly ILogger<AnalyticsService> logger;

        public AnalyticsService(
            IHabitRepository habitRepository,
            IHabitLogRepository habitLogRepository,
            ILogger<AnalyticsService> logger)
        {
            this.habitRepository = habitRepository;
            this.habitLogRepository = habitLogRepository;
            this.logger = logger;
        }

        public async Task<AnalyticsViewModel> GetHabitAnalyticsAsync(
            Guid habitId, Guid userId)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null || habit.UserId != userId)
            {
                return new AnalyticsViewModel();
            }

            var logs = await this.habitLogRepository.GetByHabitIdAsync(habitId);
            var completedLogs = logs
                .Where(l => l.Status == LogStatus.Completed)
                .OrderBy(l => l.ScheduledDate)
                .ToList();

            var today = DateTime.Today;
            var daysSinceStart = Math.Max(1, (today - habit.StartDate.Date).Days + 1);

            // Базові метрики
            var currentStreak = this.CalculateCurrentStreak(completedLogs);
            var maxStreak = this.CalculateMaxStreak(completedLogs);
            var completedUniqueDays = completedLogs
    .Select(l => l.ScheduledDate.Date)
    .Distinct()
    .Count();

            var consistencyRate = Math.Round(
                Math.Min((double)completedUniqueDays / daysSinceStart * 100, 100), 2);

            // Щоденні логи для графіка
            var dailyLogs = this.BuildDailyLogs(completedLogs, habit.StartDate, today);

            // МНК апроксимація
            var mnkPoints = this.BuildMnkPoints(completedLogs, habit.StartDate);
            var (a0, a1, a2, trendLine) = this.CalculateMnk(mnkPoints, daysSinceStart);
            var (predictedDays, formationDate) = this.PredictFormation(
                a0, a1, a2, daysSinceStart);

            // Марківські ланцюги
            var (transMatrix, stationaryDist, next7, breakRisk) =
                this.CalculateMarkovChains(completedLogs, daysSinceStart);

            // HSS з градієнтним спуском
            var (hss, alpha, beta, gamma) = this.CalculateHssWithGradient(
                currentStreak, consistencyRate,
                this.CalculateVolatility(completedLogs));

            // Теорія ігор — мінімакс
            var (weekdayRisks, optimalDay, riskyDay) =
                this.CalculateMinimaxStrategy(completedLogs);

            // Статистика по днях тижня
            var weekdayStats = this.CalculateWeekdayStats(completedLogs, daysSinceStart);

            this.logger.LogInformation(
                "Аналітика розрахована для звички {HabitId}", habitId);

            return new AnalyticsViewModel
            {
                HabitName = habit.Name,
                HabitColor = habit.Color,
                DaysSinceStart = daysSinceStart,
                TotalCompleted = completedLogs.Count,
                CurrentStreak = currentStreak,
                MaxStreak = maxStreak,
                ConsistencyRate = consistencyRate,
                DailyLogs = dailyLogs,
                MnkPoints = mnkPoints,
                MnkTrendLine = trendLine,
                MnkA0 = Math.Round(a0, 4),
                MnkA1 = Math.Round(a1, 4),
                MnkA2 = Math.Round(a2, 4),
                PredictedDaysToForm = predictedDays,
                PredictedFormationDate = formationDate,
                TransitionMatrix = transMatrix,
                MarkovProbCompleted = Math.Round(stationaryDist[0] * 100, 1),
                MarkovProbSkipped = Math.Round(stationaryDist[1] * 100, 1),
                Next7DaysProbabilities = next7,
                BreakRisk = Math.Round(breakRisk * 100, 1),
                HabitStrengthScore = Math.Round(hss, 1),
                AlphaWeight = Math.Round(alpha, 3),
                BetaWeight = Math.Round(beta, 3),
                GammaWeight = Math.Round(gamma, 3),
                WeekdayRisks = weekdayRisks,
                OptimalDayToAct = optimalDay,
                MostRiskyDay = riskyDay,
                WeekdayStats = weekdayStats,
            };
        }

        // ============================================================
        // АЛГОРИТМ 1: МНК — Метод Найменших Квадратів
        // Апроксимуємо накопичений прогрес поліномом 2-го степеня
        // y(t) = a0 + a1*t + a2*t^2
        // Система: [A^T * A] * x = A^T * b
        // ============================================================
        private List<MnkDataPoint> BuildMnkPoints(
            List<HabitFlow.Domain.Entities.HabitLog> logs,
            DateTime startDate)
        {
            var points = new List<MnkDataPoint>();
            int cumulative = 0;

            var grouped = logs
                .GroupBy(l => (l.ScheduledDate.Date - startDate.Date).Days)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                cumulative += group.Count();
                points.Add(new MnkDataPoint
                {
                    Day = group.Key,
                    Value = cumulative,
                });
            }

            return points;
        }

        private (double a0, double a1, double a2,
                 List<MnkDataPoint> trendLine)
            CalculateMnk(List<MnkDataPoint> points, int totalDays)
        {
            if (points.Count < 3)
            {
                return (0, 0.5, 0, new List<MnkDataPoint>());
            }

            int n = points.Count;

            // Будуємо матрицю A розміром n x 3 (поліном 2-го ступеня)
            double[,] a = new double[n, 3];
            double[] b = new double[n];

            for (int i = 0; i < n; i++)
            {
                double t = points[i].Day;
                a[i, 0] = 1;
                a[i, 1] = t;
                a[i, 2] = t * t;
                b[i] = points[i].Value;
            }

            // A^T * A — матриця 3x3
            double[,] ata = new double[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < n; k++)
                    {
                        ata[i, j] += a[k, i] * a[k, j];
                    }
                }
            }

            // A^T * b — вектор 3
            double[] atb = new double[3];
            for (int i = 0; i < 3; i++)
            {
                for (int k = 0; k < n; k++)
                {
                    atb[i] += a[k, i] * b[k];
                }
            }

            // Розв'язуємо методом Гаусса
            var coefficients = this.GaussianElimination(ata, atb);
            double a0 = coefficients[0];
            double a1 = coefficients[1];
            double a2 = coefficients[2];

            // Будуємо лінію тренду
            var trendLine = new List<MnkDataPoint>();
            int forecastDays = totalDays + 30;
            for (int t = 0; t <= forecastDays; t += 2)
            {
                var val = a0 + a1 * t + a2 * t * t;
                trendLine.Add(new MnkDataPoint { Day = t, Value = Math.Max(0, val) });
            }

            return (a0, a1, a2, trendLine);
        }

        private double[] GaussianElimination(double[,] matrix, double[] vector)
        {
            int n = vector.Length;
            double[,] m = (double[,])matrix.Clone();
            double[] v = (double[])vector.Clone();

            // Пряма хід
            for (int col = 0; col < n; col++)
            {
                int maxRow = col;
                for (int row = col + 1; row < n; row++)
                {
                    if (Math.Abs(m[row, col]) > Math.Abs(m[maxRow, col]))
                    {
                        maxRow = row;
                    }
                }

                for (int k = col; k < n; k++)
                {
                    (m[col, k], m[maxRow, k]) = (m[maxRow, k], m[col, k]);
                }

                (v[col], v[maxRow]) = (v[maxRow], v[col]);

                for (int row = col + 1; row < n; row++)
                {
                    if (Math.Abs(m[col, col]) < 1e-10)
                    {
                        continue;
                    }

                    double factor = m[row, col] / m[col, col];
                    for (int k = col; k < n; k++)
                    {
                        m[row, k] -= factor * m[col, k];
                    }

                    v[row] -= factor * v[col];
                }
            }

            // Зворотня хід
            double[] result = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                result[i] = v[i];
                for (int j = i + 1; j < n; j++)
                {
                    result[i] -= m[i, j] * result[j];
                }

                if (Math.Abs(m[i, i]) > 1e-10)
                {
                    result[i] /= m[i, i];
                }
            }

            return result;
        }

        private (int days, DateTime date) PredictFormation(
            double a0, double a1, double a2, int currentDay)
        {
            // Звичка сформована коли HSS >= 80 або виконань >= 66
            const double targetCompletions = 66;

            if (a1 <= 0 && a2 <= 0)
            {
                return (0, DateTime.UtcNow.Date);
            }

            // Шукаємо день коли y(t) = 66
            for (int t = currentDay; t <= 365; t++)
            {
                var predicted = a0 + a1 * t + a2 * t * t;
                if (predicted >= targetCompletions)
                {
                    var daysLeft = t - currentDay;
                    return (daysLeft, DateTime.UtcNow.Date.AddDays(daysLeft));
                }
            }

            return (999, DateTime.UtcNow.Date.AddDays(999));
        }

        // ============================================================
        // АЛГОРИТМ 2: ЛАНЦЮГИ МАРКОВА
        // Стани: 0 = Виконано, 1 = Пропущено
        // Матриця переходів P[i,j] = P(перейти з стану i в стан j)
        // Стаціонарний розподіл: π = π * P
        // ============================================================
        private (double[][] matrix, double[] stationary,
                 List<double> next7, double breakRisk)
            CalculateMarkovChains(
                List<HabitFlow.Domain.Entities.HabitLog> completedLogs,
                int totalDays)
        {
            // Будуємо послідовність станів
            var sequence = new List<int>();
            var startDate = completedLogs.Any()
                ? completedLogs.First().ScheduledDate.Date
                : DateTime.UtcNow.Date.AddDays(-totalDays);

            var completedDates = completedLogs
                .Select(l => l.ScheduledDate.Date)
                .ToHashSet();

            for (int i = 0; i < totalDays; i++)
            {
                var date = startDate.AddDays(i);
                sequence.Add(completedDates.Contains(date) ? 0 : 1);
            }

            // Рахуємо переходи
            double[] transitions = new double[] { 0, 0, 0, 0 }; // [00, 01, 10, 11]

            for (int i = 0; i < sequence.Count - 1; i++)
            {
                int from = sequence[i];
                int to = sequence[i + 1];
                transitions[from * 2 + to]++;
            }

            // Будуємо матрицю переходів
            double row0Total = transitions[0] + transitions[1];
            double row1Total = transitions[2] + transitions[3];

            var matrix = new double[][]
            {
                new double[]
                {
                    row0Total > 0 ? transitions[0] / row0Total : 0.7,
                    row0Total > 0 ? transitions[1] / row0Total : 0.3,
                },
                new double[]
                {
                    row1Total > 0 ? transitions[2] / row1Total : 0.4,
                    row1Total > 0 ? transitions[3] / row1Total : 0.6,
                },
            };

            // Стаціонарний розподіл: π[0] = p10 / (p01 + p10)
            double p01 = matrix[0][1];
            double p10 = matrix[1][0];
            double denom = p01 + p10;

            double[] stationary = denom > 0
                ? new double[] { p10 / denom, p01 / denom }
                : new double[] { 0.5, 0.5 };

            // Прогноз на наступні 7 днів
            // Визначаємо поточний стан
            double[] currentDist = sequence.Any() && sequence.Last() == 0
                ? new double[] { 1.0, 0.0 }
                : new double[] { 0.0, 1.0 };

            var next7 = new List<double>();
            var dist = currentDist;

            for (int day = 1; day <= 7; day++)
            {
                double[] nextDist = new double[]
                {
                    dist[0] * matrix[0][0] + dist[1] * matrix[1][0],
                    dist[0] * matrix[0][1] + dist[1] * matrix[1][1],
                };
                next7.Add(Math.Round(nextDist[0] * 100, 1));
                dist = nextDist;
            }

            // Ризик зламу серії = ймовірність переходу з виконано в пропущено
            double breakRisk = p01;

            return (matrix, stationary, next7, breakRisk);
        }

        // ============================================================
        // АЛГОРИТМ 3: ГРАДІЄНТНИЙ СПУСК
        // Підбираємо оптимальні ваги для HSS:
        // HSS = α*streak + β*consistency + γ*(1-volatility)
        // Мінімізуємо: L(α,β,γ) = (HSS - target)^2
        // ∂L/∂α = 2*(HSS-target)*streak
        // ============================================================
        private (double hss, double alpha, double beta, double gamma)
            CalculateHssWithGradient(
                int streak, double consistency, double volatility)
        {
            // Нормалізуємо вхідні дані до [0, 1]
            double normStreak = Math.Min(streak / 66.0, 1.0);
            double normConsistency = consistency / 100.0;
            double normStability = Math.Max(0, 1 - volatility / 7.0);

            // Початкові ваги (рівні)
            double alpha = 0.333;
            double beta = 0.333;
            double gamma = 0.333;

            // Цільове значення HSS (ідеальний прогрес)
            double target = 0.75;
            double learningRate = 0.01;
            int iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                double hss = alpha * normStreak
                           + beta * normConsistency
                           + gamma * normStability;

                double error = hss - target;
                double loss = error * error;

                if (loss < 0.0001)
                {
                    break;
                }

                // Градієнти
                double dAlpha = 2 * error * normStreak;
                double dBeta = 2 * error * normConsistency;
                double dGamma = 2 * error * normStability;

                // Оновлення ваг
                alpha -= learningRate * dAlpha;
                beta -= learningRate * dBeta;
                gamma -= learningRate * dGamma;

                // Проекція на симплекс: α + β + γ = 1, кожна >= 0.1
                alpha = Math.Max(0.1, alpha);
                beta = Math.Max(0.1, beta);
                gamma = Math.Max(0.1, gamma);

                double sum = alpha + beta + gamma;
                alpha /= sum;
                beta /= sum;
                gamma /= sum;
            }

            double finalHss = (alpha * normStreak
                             + beta * normConsistency
                             + gamma * normStability) * 100;

            return (finalHss, alpha, beta, gamma);
        }

        // ============================================================
        // АЛГОРИТМ 4: ТЕОРІЯ ІГОР — МІНІМАКС
        // Гравець: користувач (вибирає день виконання)
        // Противник: "обставини" (вихідні, стрес, зайнятість)
        // Матриця виплат: U(день, ризик)
        // maximin стратегія: вибрати день з максимальним мінімумом
        // ============================================================
        private (List<WeekdayRisk> risks, string optimalDay, string riskyDay)
            CalculateMinimaxStrategy(
                List<HabitFlow.Domain.Entities.HabitLog> completedLogs)
        {
            var dayNames = new[]
            {
                "Понеділок", "Вівторок", "Середа",
                "Четвер", "П'ятниця", "Субота", "Неділя",
            };

            var risks = new List<WeekdayRisk>();

            for (int d = 0; d < 7; d++)
            {
                var dow = (DayOfWeek)((d + 1) % 7);
                var dayLogs = completedLogs
                    .Where(l => l.ScheduledDate.DayOfWeek == dow)
                    .ToList();

                // "Природній ризик" середовища для кожного дня
                // (вихідні мають вищий базовий ризик)
                double baseRisk = dow is DayOfWeek.Saturday or DayOfWeek.Sunday
                    ? 0.4
                    : dow is DayOfWeek.Monday or DayOfWeek.Friday
                        ? 0.25
                        : 0.15;

                double completionRate = dayLogs.Any()
                    ? dayLogs.Count(l => l.Status == LogStatus.Completed)
                      / (double)dayLogs.Count
                    : 0.5;

                // Матриця виплат: U(день, ризик) = completionRate - baseRisk
                double utilityMin = completionRate - 1.0;      // найгірший сценарій
                double utilityMax = completionRate - baseRisk;  // найкращий сценарій
                double minimaxValue = (utilityMin + utilityMax) / 2.0;

                // Ризик = 1 - мінімакс значення нормалізоване
                double riskScore = Math.Max(0, Math.Min(1, 1 - (minimaxValue + 1) / 2));

                string riskLevel = riskScore switch
                {
                    < 0.3 => "Низький",
                    < 0.6 => "Середній",
                    _ => "Високий",
                };

                risks.Add(new WeekdayRisk
                {
                    DayName = dayNames[d],
                    CompletionRate = Math.Round(completionRate * 100, 1),
                    RiskScore = Math.Round(riskScore * 100, 1),
                    RiskLevel = riskLevel,
                });
            }

            // Мінімакс: оптимальна стратегія — день з найнижчим ризиком
            var optimal = risks.OrderBy(r => r.RiskScore).First();
            var risky = risks.OrderByDescending(r => r.RiskScore).First();

            return (risks, optimal.DayName, risky.DayName);
        }

        // ============================================================
        // ДОПОМІЖНІ АЛГОРИТМИ
        // ============================================================


        private int CalculateCurrentStreak(
    List<HabitFlow.Domain.Entities.HabitLog> logs)
        {
            if (!logs.Any())
            {
                return 0;
            }

            var dates = logs
                .Select(l => l.ScheduledDate.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            bool doneToday = dates.Contains(DateTime.Today);
            bool doneYesterday = dates.Contains(DateTime.Today.AddDays(-1));

            DateTime startDate;

            if (doneToday)
            {
                startDate = DateTime.Today;
            }
            else if (doneYesterday)
            {
                startDate = DateTime.Today.AddDays(-1);
            }
            else
            {
                return 0;
            }

            int streak = 0;
            var current = startDate;

            foreach (var date in dates)
            {
                if (date == current)
                {
                    streak++;
                    current = current.AddDays(-1);
                }
                else if (date < current)
                {
                    break;
                }
            }

            return streak;
        }

        private int CalculateMaxStreak(
    List<HabitFlow.Domain.Entities.HabitLog> logs)
        {
            if (!logs.Any())
            {
                return 0;
            }

            var dates = logs
                .Select(l => l.ScheduledDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            int max = 1;
            int current = 1;

            for (int i = 1; i < dates.Count; i++)
            {
                var diff = (dates[i] - dates[i - 1]).Days;

                if (diff == 1)
                {
                    current++;
                    max = Math.Max(max, current);
                }
                else
                {
                    current = 1;
                }
            }

            return max;
        }

        private double CalculateVolatility(
            List<HabitFlow.Domain.Entities.HabitLog> logs)
        {
            if (logs.Count < 2)
            {
                return 0;
            }

            var intervals = new List<double>();
            for (int i = 1; i < logs.Count; i++)
            {
                intervals.Add(
                    (logs[i].ScheduledDate - logs[i - 1].ScheduledDate).TotalDays);
            }

            double mean = intervals.Average();
            double variance = intervals.Sum(x => Math.Pow(x - mean, 2)) / intervals.Count;
            return Math.Round(Math.Sqrt(variance), 3);
        }

        private List<DailyLogPoint> BuildDailyLogs(
            List<HabitFlow.Domain.Entities.HabitLog> completedLogs,
            DateTime startDate,
            DateTime today)
        {
            var result = new List<DailyLogPoint>();
            var completedDates = completedLogs
                .Select(l => l.ScheduledDate.Date)
                .ToHashSet();

            int streak = 0;
            var days = Math.Min((today - startDate).Days + 1, 90);

            for (int i = 0; i < days; i++)
            {
                var date = startDate.AddDays(i);
                bool completed = completedDates.Contains(date);

                if (completed)
                {
                    streak++;
                }
                else
                {
                    streak = 0;
                }

                result.Add(new DailyLogPoint
                {
                    Date = date,
                    Completed = completed,
                    CumulativeStreak = streak,
                });
            }

            return result;
        }

        private List<WeekdayStats> CalculateWeekdayStats(
            List<HabitFlow.Domain.Entities.HabitLog> completedLogs,
            int totalDays)
        {
            var dayNames = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };
            var result = new List<WeekdayStats>();

            for (int d = 0; d < 7; d++)
            {
                var dow = (DayOfWeek)((d + 1) % 7);
                var dayCount = completedLogs.Count(l =>
                    l.ScheduledDate.DayOfWeek == dow);
                var expectedDays = Math.Max(1, totalDays / 7);

                result.Add(new WeekdayStats
                {
                    Day = dayNames[d],
                    Total = expectedDays,
                    Completed = dayCount,
                    Rate = Math.Round((double)dayCount / expectedDays * 100, 1),
                });
            }

            return result;
        }
    }
}