using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace HabitFlow.BLL.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private const int HabitFormationTarget = 66;
        private const double EwmaAlpha = 0.3;
        private const double LaplacePseudoCount = 0.5;

        private readonly IHabitRepository habitRepository;
        private readonly IHabitLogRepository habitLogRepository;
        private readonly ISharedHabitRepository sharedHabitRepository;
        private readonly ILogger<AnalyticsService> logger;

        public AnalyticsService(
            IHabitRepository habitRepository,
            IHabitLogRepository habitLogRepository,
            ISharedHabitRepository sharedHabitRepository,
            ILogger<AnalyticsService> logger)
        {
            this.habitRepository = habitRepository;
            this.habitLogRepository = habitLogRepository;
            this.sharedHabitRepository = sharedHabitRepository;
            this.logger = logger;
        }

        public async Task<AnalyticsViewModel> GetHabitAnalyticsAsync(Guid habitId, Guid userId)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null)
                return new AnalyticsViewModel();

            bool isOwner = habit.UserId == userId;
            bool isParticipant = false;

            if (!isOwner)
            {
                var participant = await this.sharedHabitRepository.GetParticipantAsync(habitId, userId);
                isParticipant = participant != null;
            }

            if (!isOwner && !isParticipant)
                return new AnalyticsViewModel();

            var allLogs = await this.habitLogRepository.GetByHabitIdAsync(habitId, userId);

            var today = DateTime.Today;
            var startDate = habit.StartDate.Date;

            var completedDates = allLogs
                .Where(l => l.UserId == userId && l.Status == LogStatus.Completed)
                .Select(l => l.ScheduledDate.Date)
                .Distinct()
                .Where(d => d >= startDate && d <= today)
                .OrderBy(d => d)
                .ToList();

            var totalDays = Math.Max(1, (today - startDate).Days + 1);
            var completedSet = new HashSet<DateTime>(completedDates);

            var series = Enumerable.Range(0, totalDays)
                .Select(i => startDate.AddDays(i))
                .Where(d => d <= today)
                .Select(d => new DayRecord(d, completedSet.Contains(d)))
                .ToList();

            int n = Math.Max(1, series.Count);
            int totalCompleted = completedDates.Count;
            bool hasEnoughData = totalCompleted >= 3;
            bool alreadyFormed = totalCompleted >= HabitFormationTarget;

            double consistencyRate = Math.Round(Math.Min(100.0, totalCompleted * 100.0 / n), 2);
            int currentStreak = this.CalcCurrentStreak(series);
            int maxStreak = this.CalcMaxStreak(series);
            var weekdayStats = this.CalcWeekdayStats(series);

            List<MnkDataPoint> mnkPoints;
            List<MnkDataPoint> trendLine;
            double a0, a1, a2, mnkR2;
            int predictedDays;
            DateTime? formationDate;

            if (hasEnoughData)
            {
                mnkPoints = this.BuildMnkPoints(completedDates, startDate);
                (a0, a1, a2, trendLine, mnkR2) = this.CalculateMnk(mnkPoints, n);

                if (alreadyFormed)
                {
                    predictedDays = 0;
                    formationDate = null;
                }
                else
                {
                    (predictedDays, formationDate) = this.PredictFormation(a0, a1, a2, n);
                }
            }
            else
            {
                mnkPoints = new List<MnkDataPoint>();
                trendLine = new List<MnkDataPoint>();
                a0 = a1 = a2 = mnkR2 = 0;
                predictedDays = 0;
                formationDate = null;
            }

            double[][] transMatrix;
            double[] stationaryDist;
            double p01, p10, p00, p11;
            double tomorrowSkipRisk, skipImpact;
            List<double> next7;

            if (hasEnoughData)
            {
                (transMatrix, stationaryDist, p01, p10) = this.CalcMarkov(series);
                p00 = 1.0 - p01;
                p11 = 1.0 - p10;
                tomorrowSkipRisk = currentStreak > 0 ? p01 : p11;
                skipImpact = Math.Round(Math.Max(0, p00 - p10) * 100, 0);
                double ewma = this.CalcEwma(series, EwmaAlpha);
                next7 = this.CalcHybridForecast(series, weekdayStats, p01, p10, ewma);
            }
            else
            {
                transMatrix = new double[][] { new[] { 0.0, 0.0 }, new[] { 0.0, 0.0 } };
                stationaryDist = new[] { 0.0, 0.0 };
                p01 = p10 = p00 = p11 = 0;
                tomorrowSkipRisk = 0;
                skipImpact = 0;
                next7 = new List<double>();
            }

            double hss, alpha, beta, gamma;
            if (hasEnoughData)
            {
                double volatility = this.CalcVolatility(completedDates);
                (hss, alpha, beta, gamma) = this.CalcHss(currentStreak, consistencyRate, volatility, totalCompleted);
            }
            else
            {
                hss = 0;
                alpha = 0.30;
                beta = 0.30;
                gamma = 0.25;
            }

            var (weekdayRisks, optimalDay, riskyDay) = hasEnoughData
                ? this.CalcWeekdayRisk(series)
                : (new List<WeekdayRisk>(), null, null);

            int recoveryIndex = hasEnoughData ? this.CalcRecoveryIndex(series) : 0;

            this.logger.LogInformation("Аналітика для {HabitId}", habitId);

            return new AnalyticsViewModel
            {
                HabitName = habit.Name,
                HabitColor = habit.Color,
                DaysSinceStart = n,
                TotalCompleted = totalCompleted,
                CurrentStreak = currentStreak,
                MaxStreak = maxStreak,
                ConsistencyRate = consistencyRate,
                IsStreakActive = currentStreak > 0,
                HasEnoughData = hasEnoughData,
                AlreadyFormed = alreadyFormed,
                DailyLogs = this.BuildDailyLogs(series),
                MnkPoints = mnkPoints,
                MnkTrendLine = trendLine,
                MnkA0 = Math.Round(a0, 4),
                MnkA1 = Math.Round(a1, 4),
                MnkA2 = Math.Round(a2, 4),
                MnkR2 = Math.Round(mnkR2, 3),
                PredictedDaysToForm = predictedDays,
                PredictedFormationDate = formationDate,
                TransitionMatrix = transMatrix,
                MarkovP00 = hasEnoughData ? Math.Round(p00 * 100, 1) : 0,
                MarkovP10 = hasEnoughData ? Math.Round(p10 * 100, 1) : 0,
                MarkovP01 = hasEnoughData ? Math.Round(p01 * 100, 1) : 0,
                MarkovP11 = hasEnoughData ? Math.Round(p11 * 100, 1) : 0,
                MarkovProbCompleted = hasEnoughData ? Math.Round(stationaryDist[0] * 100, 1) : 0,
                MarkovProbSkipped = hasEnoughData ? Math.Round(stationaryDist[1] * 100, 1) : 0,
                Next7DaysProbabilities = next7,
                BreakRisk = hasEnoughData ? Math.Round(tomorrowSkipRisk * 100, 1) : 0,
                SkipImpact = skipImpact,
                HabitStrengthScore = hss,
                AlphaWeight = Math.Round(alpha, 3),
                BetaWeight = Math.Round(beta, 3),
                GammaWeight = Math.Round(gamma, 3),
                WeekdayRisks = weekdayRisks,
                OptimalDayToAct = optimalDay ?? string.Empty,
                MostRiskyDay = riskyDay ?? string.Empty,
                WeekdayStats = weekdayStats,
                RecoveryIndex = recoveryIndex,
                MainInsight = this.GenInsight(currentStreak, consistencyRate,
                    tomorrowSkipRisk * 100, totalCompleted, n, hasEnoughData, alreadyFormed),
                ActionTip = hasEnoughData
                    ? this.GenTip(weekdayRisks, tomorrowSkipRisk * 100, currentStreak, consistencyRate, alreadyFormed)
                    : "Виконай звичку хоча б 3 рази — і ми порахуємо твої перші прогнози.",
            };
        }

        private record DayRecord(DateTime Date, bool Completed);

        private int CalcCurrentStreak(List<DayRecord> series)
        {
            if (series.Count == 0) return 0;

            bool todayDone = series[^1].Completed;
            bool yesterdayDone = series.Count > 1 && series[^2].Completed;

            int startIdx;
            if (todayDone)
                startIdx = series.Count - 1;
            else if (yesterdayDone)
                startIdx = series.Count - 2;
            else
                return 0;

            int streak = 0;
            for (int i = startIdx; i >= 0; i--)
            {
                if (series[i].Completed) streak++;
                else break;
            }

            return streak;
        }

        private int CalcMaxStreak(List<DayRecord> series)
        {
            int max = 0, cur = 0;
            foreach (var d in series)
            {
                cur = d.Completed ? cur + 1 : 0;
                if (cur > max) max = cur;
            }
            return max;
        }

        private List<WeekdayStats> CalcWeekdayStats(List<DayRecord> series)
        {
            var names = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };

            return Enumerable.Range(0, 7).Select(d =>
            {
                var dow = (DayOfWeek)((d + 1) % 7);
                var recs = series.Where(r => r.Date.DayOfWeek == dow).ToList();
                int total = recs.Count;
                int done = recs.Count(r => r.Completed);

                return new WeekdayStats
                {
                    Day = names[d],
                    Total = total,
                    Completed = done,
                    Rate = total > 0 ? Math.Min(100.0, Math.Round(done * 100.0 / total, 1)) : 0.0,
                };
            }).ToList();
        }

        private List<MnkDataPoint> BuildMnkPoints(List<DateTime> dates, DateTime start)
        {
            int cum = 0;
            return dates
                .GroupBy(d => (d - start).Days)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    cum += g.Count();
                    return new MnkDataPoint { Day = g.Key, Value = cum };
                })
                .ToList();
        }

        private (double a0, double a1, double a2, List<MnkDataPoint> trend, double r2)
            CalculateMnk(List<MnkDataPoint> pts, int totalDays)
        {
            if (pts.Count < 3) return (0, 0, 0, new List<MnkDataPoint>(), 0);

            int n = pts.Count;
            double[,] A = new double[n, 3];
            double[] b = new double[n];

            for (int i = 0; i < n; i++)
            {
                double t = pts[i].Day;
                A[i, 0] = 1;
                A[i, 1] = t;
                A[i, 2] = t * t;
                b[i] = pts[i].Value;
            }

            double[,] AtA = new double[3, 3];
            double[] Atb = new double[3];

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    for (int k = 0; k < n; k++)
                        AtA[i, j] += A[k, i] * A[k, j];

            for (int i = 0; i < 3; i++)
                for (int k = 0; k < n; k++)
                    Atb[i] += A[k, i] * b[k];

            var c = this.GaussElim(AtA, Atb);
            if (c == null) return (0, 0, 0, new List<MnkDataPoint>(), 0);

            double a0 = c[0], a1 = c[1], a2 = c[2];

            double ssRes = pts.Sum(p => Math.Pow(p.Value - (a0 + a1 * p.Day + a2 * p.Day * p.Day), 2));
            double mean = pts.Average(p => p.Value);
            double ssTot = pts.Sum(p => Math.Pow(p.Value - mean, 2));
            double r2 = ssTot > 1e-10 ? Math.Max(0, 1.0 - ssRes / ssTot) : 0;

            var trend = new List<MnkDataPoint>();
            double prevTrendVal = double.MinValue;
            for (int t = 0; t <= totalDays + 30; t += 2)
            {
                double val = Math.Max(0, a0 + a1 * t + a2 * t * t);
                if (val < prevTrendVal) break;
                trend.Add(new MnkDataPoint { Day = t, Value = val });
                prevTrendVal = val;
            }

            return (a0, a1, a2, trend, r2);
        }

        private double[]? GaussElim(double[,] M, double[] v)
        {
            int n = v.Length;
            double[,] m = (double[,])M.Clone();
            double[] r = (double[])v.Clone();

            for (int col = 0; col < n; col++)
            {
                int mx = col;
                for (int row = col + 1; row < n; row++)
                    if (Math.Abs(m[row, col]) > Math.Abs(m[mx, col])) mx = row;

                for (int k = col; k < n; k++)
                    (m[col, k], m[mx, k]) = (m[mx, k], m[col, k]);
                (r[col], r[mx]) = (r[mx], r[col]);

                if (Math.Abs(m[col, col]) < 1e-10)
                {
                    this.logger.LogWarning("GaussElim: вироджена матриця на стовпці {Col}.", col);
                    return null;
                }

                for (int row = col + 1; row < n; row++)
                {
                    double f = m[row, col] / m[col, col];
                    for (int k = col; k < n; k++) m[row, k] -= f * m[col, k];
                    r[row] -= f * r[col];
                }
            }

            double[] res = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                res[i] = r[i];
                for (int j = i + 1; j < n; j++) res[i] -= m[i, j] * res[j];

                if (Math.Abs(m[i, i]) < 1e-10)
                {
                    this.logger.LogWarning("GaussElim: вироджена матриця на рядку {Row}.", i);
                    return null;
                }

                res[i] /= m[i, i];
            }

            return res;
        }

        private (int days, DateTime? date) PredictFormation(double a0, double a1, double a2, int cur)
        {
            double curVal = a0 + a1 * cur + a2 * cur * cur;
            if (curVal >= HabitFormationTarget) return (0, null);

            if (a2 < 0)
            {
                double vertex = -a1 / (2 * a2);
                if (vertex <= cur) return (0, null);
            }

            if (a1 <= 0 && a2 <= 0) return (0, null);

            double prevVal = curVal;
            for (int t = cur + 1; t <= 730; t++)
            {
                double val = a0 + a1 * t + a2 * t * t;
                if (val < prevVal) return (0, null);
                prevVal = val;
                if (val >= HabitFormationTarget)
                    return (t - cur, DateTime.Today.AddDays(t - cur));
            }

            return (0, null);
        }

        private (double[][] matrix, double[] stat, double p01, double p10)
            CalcMarkov(List<DayRecord> series)
        {
            double t00 = 0, t01 = 0, t10 = 0, t11 = 0;

            for (int i = 0; i < series.Count - 1; i++)
            {
                bool f = series[i].Completed, s = series[i + 1].Completed;
                if (f && s) t00++;
                else if (f && !s) t01++;
                else if (!f && s) t10++;
                else t11++;
            }

            double r0 = t00 + t01;
            double r1 = t10 + t11;

            var mx = new double[][]
            {
                new[]
                {
                    (t00 + LaplacePseudoCount) / (r0 + 2 * LaplacePseudoCount),
                    (t01 + LaplacePseudoCount) / (r0 + 2 * LaplacePseudoCount),
                },
                new[]
                {
                    (t10 + LaplacePseudoCount) / (r1 + 2 * LaplacePseudoCount),
                    (t11 + LaplacePseudoCount) / (r1 + 2 * LaplacePseudoCount),
                },
            };

            double p01v = mx[0][1];
            double p10v = mx[1][0];
            double den = p01v + p10v;

            double[] stat = den > 1e-10
                ? new[] { p10v / den, p01v / den }
                : new[] { 0.6, 0.4 };

            return (mx, stat, p01v, p10v);
        }

        private double CalcEwma(List<DayRecord> series, double alpha)
        {
            if (series.Count == 0) return 0.5;
            double ewma = series[0].Completed ? 1.0 : 0.0;
            for (int i = 1; i < series.Count; i++)
                ewma = alpha * (series[i].Completed ? 1.0 : 0.0) + (1 - alpha) * ewma;
            return ewma;
        }

        private List<double> CalcHybridForecast(
            List<DayRecord> series,
            List<WeekdayStats> weekdayStats,
            double p01,
            double p10,
            double ewma)
        {
            if (series.Count < 7)
                return Enumerable.Repeat(Math.Round(ewma * 100, 1), 7).ToList();

            double p00 = 1 - p01;
            int window = Math.Min(3, series.Count);
            double prevProb = series.TakeLast(window).Count(r => r.Completed) / (double)window;

            var dayNames = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };
            var dowRates = new Dictionary<DayOfWeek, double>();

            for (int d = 0; d < 7; d++)
            {
                var dow = (DayOfWeek)((d + 1) % 7);
                var stat = weekdayStats.FirstOrDefault(s => s.Day == dayNames[d]);
                dowRates[dow] = stat != null && stat.Total > 0 ? stat.Rate / 100.0 : ewma;
            }

            var forecast = new List<double>();
            for (int step = 0; step < 7; step++)
            {
                var dow = DateTime.Today.AddDays(step + 1).DayOfWeek;
                double mkP = prevProb * p00 + (1 - prevProb) * p10;
                double wdP = dowRates.TryGetValue(dow, out var r) ? r : ewma;
                double hybrid = Math.Max(0.05, Math.Min(0.95, 0.40 * mkP + 0.40 * wdP + 0.20 * ewma));
                forecast.Add(Math.Round(hybrid * 100, 1));
                prevProb = hybrid;
            }

            return forecast;
        }

        private double CalcVolatility(List<DateTime> dates)
        {
            if (dates.Count < 2) return 0;
            var gaps = dates.Zip(dates.Skip(1), (a, b) => (b - a).TotalDays).ToList();
            double mean = gaps.Average();
            return Math.Round(Math.Sqrt(gaps.Sum(x => Math.Pow(x - mean, 2)) / gaps.Count), 3);
        }

        private (double hss, double alpha, double beta, double gamma)
            CalcHss(int streak, double consistency, double volatility, int totalCompleted)
        {
            double streakScore = Math.Clamp(streak / (double)HabitFormationTarget, 0, 1);
            double consistencyScore = Math.Clamp(consistency / 100.0, 0, 1);
            double stabilityScore = Math.Clamp(1 - volatility / 7.0, 0, 1);
            double volumeScore = Math.Clamp(totalCompleted / (double)HabitFormationTarget, 0, 1);

            const double alpha = 0.30;
            const double beta = 0.30;
            const double gamma = 0.25;
            const double delta = 0.15;

            double raw =
                alpha * streakScore +
                beta * consistencyScore +
                gamma * stabilityScore +
                delta * volumeScore;

            double maturity = Math.Clamp(totalCompleted / 21.0, 0, 1);
            double score = raw * (0.35 + 0.65 * maturity) * 100;

            return (Math.Clamp(score, 0, 100), alpha, beta, gamma);
        }

        private (List<WeekdayRisk> risks, string optimal, string risky)
            CalcWeekdayRisk(List<DayRecord> series)
        {
            var names = new[]
            {
                "Понеділок", "Вівторок", "Середа",
                "Четвер", "П'ятниця", "Субота", "Неділя",
            };

            var risks = Enumerable.Range(0, 7).Select(d =>
            {
                var dow = (DayOfWeek)((d + 1) % 7);
                var recs = series.Where(r => r.Date.DayOfWeek == dow).ToList();
                int total = recs.Count;
                int done = recs.Count(r => r.Completed);

                double cr = total > 0 ? (double)done / total : 0.0;
                double fail = 1.0 - cr;
                double baseR = dow is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.35
                    : dow is DayOfWeek.Monday or DayOfWeek.Friday ? 0.20
                    : 0.10;
                double risk = Math.Min(1.0, 0.70 * fail + 0.30 * baseR);

                return new WeekdayRisk
                {
                    DayName = names[d],
                    CompletionRate = total > 0 ? Math.Round(cr * 100, 1) : 0.0,
                    RiskScore = Math.Round(risk * 100, 1),
                    RiskLevel = risk < 0.25 ? "Низький" : risk < 0.50 ? "Середній" : "Високий",
                };
            }).ToList();

            return (
                risks,
                risks.OrderBy(r => r.RiskScore).First().DayName,
                risks.OrderByDescending(r => r.RiskScore).First().DayName
            );
        }

        private int CalcRecoveryIndex(List<DayRecord> series)
        {
            var gaps = new List<int>();
            int missStreak = 0;

            foreach (var day in series)
            {
                if (!day.Completed)
                {
                    missStreak++;
                }
                else
                {
                    if (missStreak > 0)
                    {
                        gaps.Add(missStreak);
                        missStreak = 0;
                    }
                }
            }

            if (gaps.Count == 0) return 0;
            return (int)Math.Round(gaps.Average());
        }

        private List<DailyLogPoint> BuildDailyLogs(List<DayRecord> series)
        {
            int streak = 0;
            return series.Select(r =>
            {
                streak = r.Completed ? streak + 1 : 0;
                return new DailyLogPoint
                {
                    Date = r.Date,
                    Completed = r.Completed,
                    CumulativeStreak = streak,
                };
            }).ToList();
        }

        private string GenInsight(
            int streak, double consistency, double skipRiskPct,
            int total, int days, bool hasEnoughData, bool alreadyFormed)
        {
            if (!hasEnoughData)
                return $"Ти щойно починаєш! Виконай звичку ще {Math.Max(0, 3 - total)} рази — і перші прогнози будуть готові.";
            if (alreadyFormed)
                return $"Звичка повністю сформована! {total} виконань — мозок вже працює на автопілоті.";
            if (consistency >= 80 && streak >= 5)
                return $"Ти на підйомі! {streak} днів поспіль і {consistency}% — звичка майже автоматична.";
            if (streak >= 14)
                return $"Неймовірно — {streak} днів поспіль! Мозок вже формує автоматизм.";
            if (streak >= 7)
                return "Тиждень без перерви — реальне досягнення. Ще трохи і стане легше.";
            if (skipRiskPct > 50)
                return "Є ризик зупинитись. Сьогодні важливо виконати хоча б мінімум.";
            if (consistency < 30)
                return "Поки важко — але ти продовжуєш. Кожен день рахується.";
            if (total >= 21)
                return $"Ти вже виконував(ла) цю звичку {total} разів — мозок запам'ятовує паттерн.";

            return $"Стабільний прогрес за {days} днів. Продовжуй у тому ж темпі.";
        }

        private string GenTip(
            List<WeekdayRisk> risks, double skipRiskPct,
            int streak, double consistency, bool alreadyFormed)
        {
            if (alreadyFormed)
                return "Звичка вже сформована. Продовжуй підтримувати ритм — це найкраще що ти можеш зробити.";
            if (!risks.Any())
                return "Додай перші виконання щоб отримати поради.";

            var worst = risks.OrderByDescending(r => r.RiskScore).First();
            var best = risks.OrderBy(r => r.RiskScore).First();

            if (skipRiskPct > 50)
                return "Постав нагадування прямо зараз. Після пропуску повернутись вдвічі важче.";
            if (streak >= 3)
                return $"Твій слабкий день — {worst.DayName} ({worst.CompletionRate}% виконань). Заплануй щось просте заздалегідь.";

            return $"Найлегше тобі дається {best.DayName} ({best.CompletionRate}%). У {worst.DayName} постав нагадування на ранок.";
        }
    }
}