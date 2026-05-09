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
                return new AnalyticsViewModel();

            var allLogs = await this.habitLogRepository.GetByHabitIdAsync(habitId);

            var completedDates = allLogs
                .Where(l => l.Status == LogStatus.Completed)
                .Select(l => l.ScheduledDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var today = DateTime.Today;
            var startDate = habit.StartDate.Date;
            var totalDays = Math.Max(1, (today - startDate).Days + 1);

            // ── Повна бінарна серія ────────────────────────────────
            var completedSet = new HashSet<DateTime>(completedDates);
            var series = Enumerable.Range(0, totalDays)
                .Select(i => startDate.AddDays(i))
                .Where(d => d <= today)
                .Select(d => new DayRecord(d, completedSet.Contains(d)))
                .ToList();

            int n = series.Count;

            // ── Базові метрики ─────────────────────────────────────
            int totalCompleted = completedDates.Count;
            double consistencyRate = Math.Round(totalCompleted * 100.0 / n, 2);
            int currentStreak = this.CalcCurrentStreak(series);
            int maxStreak = this.CalcMaxStreak(series);

            // ── Статистика по днях тижня ───────────────────────────
            var weekdayStats = this.CalcWeekdayStats(series);

            // ── МНК ───────────────────────────────────────────────
            var mnkPoints = this.BuildMnkPoints(completedDates, startDate);
            var (a0, a1, a2, trendLine) = this.CalculateMnk(mnkPoints, n);
            var (predictedDays, formationDate) = this.PredictFormation(a0, a1, a2, n);

            // ── Марків — матриця + стаціонарний розподіл ──────────
            var (transMatrix, stationaryDist, p01, p10) = this.CalcMarkov(series);

            double p00 = 1.0 - p01; // виконав вчора  → виконає сьогодні
            double p11 = 1.0 - p10; // пропустив вчора → пропустить сьогодні

            //   streak > 0 → серія жива → ризик зламати = p01
            //   streak = 0 → серія вже зламана → ризик пропустити = p11
            bool isStreakActive = currentStreak > 0;
            double tomorrowSkipRisk = isStreakActive ? p01 : p11;

            // Скільки % втрачається через один пропуск (динамічно, не захардкожено)
            // p00 - p10: різниця між "виконав вчора" і "пропустив вчора"
            double skipImpact = Math.Round(Math.Max(0, p00 - p10) * 100, 0);

            // ── EWMA тренд ─────────────────────────────────────────
            double ewma = this.CalcEwma(series, alpha: 0.3);

            // ── Гібридний прогноз на 7 днів ────────────────────────
            var next7 = this.CalcHybridForecast(
                series, weekdayStats, p01, p10, ewma);

            // ── HSS (градієнтний спуск) ────────────────────────────
            double volatility = this.CalcVolatility(completedDates);
            var (hss, alpha, beta, gamma) = this.CalcHss(
                currentStreak, consistencyRate, volatility);

            // ── Мінімакс ──────────────────────────────────────────
            var (weekdayRisks, optimalDay, riskyDay) = this.CalcMinimax(series);

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
                IsStreakActive = isStreakActive,
                DailyLogs = this.BuildDailyLogs(series),
                MnkPoints = mnkPoints,
                MnkTrendLine = trendLine,
                MnkA0 = Math.Round(a0, 4),
                MnkA1 = Math.Round(a1, 4),
                MnkA2 = Math.Round(a2, 4),
                PredictedDaysToForm = predictedDays,
                PredictedFormationDate = formationDate,
                TransitionMatrix = transMatrix,

                // Перехідні ймовірності Маркова (для UI "паттерни поведінки")
                // p00: виконала вчора  → виконає сьогодні
                // p10: пропустила вчора → виконає сьогодні
                // p01: виконала вчора  → пропустить сьогодні
                // p11: пропустила вчора → пропустить сьогодні
                MarkovP00 = Math.Round(p00 * 100, 1),
                MarkovP10 = Math.Round(p10 * 100, 1),
                MarkovP01 = Math.Round(p01 * 100, 1),
                MarkovP11 = Math.Round(p11 * 100, 1),

                // Стаціонарний розподіл (довгостроковий темп)
                MarkovProbCompleted = Math.Round(stationaryDist[0] * 100, 1),
                MarkovProbSkipped = Math.Round(stationaryDist[1] * 100, 1),

                Next7DaysProbabilities = next7,

                // ВИПРАВЛЕНО: правильний ризик залежно від стану серії
                BreakRisk = Math.Round(tomorrowSkipRisk * 100, 1),
                SkipImpact = skipImpact, // "один пропуск знижує шанси на X%"

                HabitStrengthScore = Math.Round(hss, 1),
                AlphaWeight = Math.Round(alpha, 3),
                BetaWeight = Math.Round(beta, 3),
                GammaWeight = Math.Round(gamma, 3),
                WeekdayRisks = weekdayRisks,
                OptimalDayToAct = optimalDay,
                MostRiskyDay = riskyDay,
                WeekdayStats = weekdayStats,
                MainInsight = this.GenInsight(currentStreak, consistencyRate,
                    tomorrowSkipRisk * 100, totalCompleted, n),
                ActionTip = this.GenTip(weekdayRisks, tomorrowSkipRisk * 100,
                    currentStreak, consistencyRate),
            };
        }

        // ============================================================
        // ВНУТРІШНІЙ ТИП
        // ============================================================
        private record DayRecord(DateTime Date, bool Completed);

        // ============================================================
        // СЕРІЯ
        // ============================================================
        private int CalcCurrentStreak(List<DayRecord> series)
        {
            if (!series.Any()) return 0;

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

        // ============================================================
        // WEEKDAY STATS
        // ============================================================
        private List<WeekdayStats> CalcWeekdayStats(List<DayRecord> series)
        {
            var names = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };
            return Enumerable.Range(0, 7).Select(d =>
            {
                var dow = (DayOfWeek)((d + 1) % 7);
                var recs = series.Where(r => r.Date.DayOfWeek == dow).ToList();
                int exp = Math.Max(1, recs.Count);
                int done = recs.Count(r => r.Completed);
                return new WeekdayStats
                {
                    Day = names[d],
                    Total = exp,
                    Completed = done,
                    Rate = Math.Min(100.0, Math.Round(done * 100.0 / exp, 1)),
                };
            }).ToList();
        }

        // ============================================================
        // МНК
        // ============================================================
        private List<MnkDataPoint> BuildMnkPoints(
            List<DateTime> dates, DateTime start)
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

        private (double a0, double a1, double a2, List<MnkDataPoint> trend)
            CalculateMnk(List<MnkDataPoint> pts, int totalDays)
        {
            if (pts.Count < 3) return (0, 0.5, 0, new List<MnkDataPoint>());
            int n = pts.Count;
            double[,] A = new double[n, 3];
            double[] b = new double[n];
            for (int i = 0; i < n; i++)
            {
                double t = pts[i].Day;
                A[i, 0] = 1; A[i, 1] = t; A[i, 2] = t * t;
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
            double a0 = c[0], a1 = c[1], a2 = c[2];

            var trend = new List<MnkDataPoint>();
            for (int t = 0; t <= totalDays + 30; t += 2)
                trend.Add(new MnkDataPoint
                { Day = t, Value = Math.Max(0, a0 + a1 * t + a2 * t * t) });

            return (a0, a1, a2, trend);
        }

        private double[] GaussElim(double[,] M, double[] v)
        {
            int n = v.Length;
            double[,] m = (double[,])M.Clone();
            double[] r = (double[])v.Clone();
            for (int col = 0; col < n; col++)
            {
                int mx = col;
                for (int row = col + 1; row < n; row++)
                    if (Math.Abs(m[row, col]) > Math.Abs(m[mx, col])) mx = row;
                for (int k = col; k < n; k++) (m[col, k], m[mx, k]) = (m[mx, k], m[col, k]);
                (r[col], r[mx]) = (r[mx], r[col]);
                for (int row = col + 1; row < n; row++)
                {
                    if (Math.Abs(m[col, col]) < 1e-10) continue;
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
                if (Math.Abs(m[i, i]) > 1e-10) res[i] /= m[i, i];
            }
            return res;
        }

        private (int days, DateTime date) PredictFormation(
            double a0, double a1, double a2, int cur)
        {
            if (a1 <= 0 && a2 <= 0) return (0, DateTime.Today);
            for (int t = cur; t <= 730; t++)
                if (a0 + a1 * t + a2 * t * t >= 66)
                    return (t - cur, DateTime.Today.AddDays(t - cur));
            return (999, DateTime.Today.AddDays(999));
        }

        // ============================================================
        // МАРКІВ — матриця + стаціонарний розподіл
        // ============================================================
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
            double r0 = t00 + t01, r1 = t10 + t11;
            var mx = new double[][]
            {
                new[]{ r0>0 ? t00/r0 : 0.7, r0>0 ? t01/r0 : 0.3 },
                new[]{ r1>0 ? t10/r1 : 0.4, r1>0 ? t11/r1 : 0.6 },
            };
            double p01v = mx[0][1], p10v = mx[1][0];
            double den = p01v + p10v;
            double[] stat = den > 0
                ? new[] { p10v / den, p01v / den }
                : new[] { 0.6, 0.4 };
            return (mx, stat, p01v, p10v);
        }

        // ============================================================
        // EWMA
        // ============================================================
        private double CalcEwma(List<DayRecord> series, double alpha)
        {
            if (!series.Any()) return 0.5;
            double ewma = series[0].Completed ? 1.0 : 0.0;
            for (int i = 1; i < series.Count; i++)
                ewma = alpha * (series[i].Completed ? 1.0 : 0.0) + (1 - alpha) * ewma;
            return ewma;
        }

        // ============================================================
        // ПРОГНОЗ НА 7 ДНІВ
        // ============================================================
        private List<double> CalcHybridForecast(
            List<DayRecord> series,
            List<WeekdayStats> weekdayStats,
            double p01, double p10,
            double ewma)
        {
            if (series.Count < 7)
                return Enumerable.Repeat(Math.Round(ewma * 100, 1), 7).ToList();

            double p00 = 1 - p01;

            // Початкова ймовірність: середнє по останніх 3 днях
            int window = Math.Min(3, series.Count);
            double prevProb = series.TakeLast(window).Count(r => r.Completed)
                              / (double)window;

            // WeekdayRate → словник DayOfWeek
            var dowRates = new Dictionary<DayOfWeek, double>();
            var dayNames = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };
            for (int d = 0; d < 7; d++)
            {
                var dow = (DayOfWeek)((d + 1) % 7);
                var stat = weekdayStats.FirstOrDefault(s => s.Day == dayNames[d]);
                dowRates[dow] = stat != null ? stat.Rate / 100.0 : ewma;
            }

            var forecast = new List<double>();
            for (int step = 0; step < 7; step++)
            {
                var nextDate = DateTime.Today.AddDays(step + 1);
                var dow = nextDate.DayOfWeek;

                double mkP = prevProb * p00 + (1 - prevProb) * p10;
                double wdP = dowRates.TryGetValue(dow, out var r) ? r : ewma;

                double hybrid = 0.40 * mkP + 0.40 * wdP + 0.20 * ewma;
                hybrid = Math.Max(0.05, Math.Min(0.95, hybrid));

                forecast.Add(Math.Round(hybrid * 100, 1));
                prevProb = hybrid;
            }

            return forecast;
        }

        // ============================================================
        // HSS — градієнтний спуск
        // ============================================================
        private double CalcVolatility(List<DateTime> dates)
        {
            if (dates.Count < 2) return 0;
            var gaps = dates.Zip(dates.Skip(1), (a, b) => (b - a).TotalDays).ToList();
            double mean = gaps.Average();
            return Math.Round(
                Math.Sqrt(gaps.Sum(x => Math.Pow(x - mean, 2)) / gaps.Count), 3);
        }

        private (double hss, double alpha, double beta, double gamma)
            CalcHss(int streak, double consistency, double volatility)
        {
            double ns = Math.Min(streak / 66.0, 1.0);
            double nc = consistency / 100.0;
            double nv = Math.Max(0, 1 - volatility / 7.0);
            double a = 0.333, b = 0.333, g = 0.333;
            for (int i = 0; i < 1000; i++)
            {
                double h = a * ns + b * nc + g * nv;
                double e = h - 0.75;
                if (e * e < 0.0001) break;
                a -= 0.01 * 2 * e * ns;
                b -= 0.01 * 2 * e * nc;
                g -= 0.01 * 2 * e * nv;
                a = Math.Max(0.1, a);
                b = Math.Max(0.1, b);
                g = Math.Max(0.1, g);
                double s = a + b + g; a /= s; b /= s; g /= s;
            }
            return ((a * ns + b * nc + g * nv) * 100, a, b, g);
        }

        // ============================================================
        // МІНІМАКС
        // ============================================================
        private (List<WeekdayRisk> risks, string optimal, string risky)
            CalcMinimax(List<DayRecord> series)
        {
            var names = new[]
            {
                "Понеділок","Вівторок","Середа",
                "Четвер","П'ятниця","Субота","Неділя",
            };
            var risks = Enumerable.Range(0, 7).Select(d =>
            {
                var dow = (DayOfWeek)((d + 1) % 7);
                var recs = series.Where(r => r.Date.DayOfWeek == dow).ToList();
                int exp = Math.Max(1, recs.Count);
                int done = recs.Count(r => r.Completed);
                double cr = (double)done / exp;
                double fail = 1.0 - cr;
                double baseR = dow is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.35
                    : dow is DayOfWeek.Monday or DayOfWeek.Friday ? 0.20
                    : 0.10;
                double risk = Math.Min(1.0, 0.70 * fail + 0.30 * baseR);
                return new WeekdayRisk
                {
                    DayName = names[d],
                    CompletionRate = Math.Round(cr * 100, 1),
                    RiskScore = Math.Round(risk * 100, 1),
                    RiskLevel = risk < 0.25 ? "Низький"
                                   : risk < 0.50 ? "Середній" : "Високий",
                };
            }).ToList();

            return (risks,
                risks.OrderBy(r => r.RiskScore).First().DayName,
                risks.OrderByDescending(r => r.RiskScore).First().DayName);
        }

        // ============================================================
        // BUILD HELPERS
        // ============================================================
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

        // ============================================================
        //ВИСНОВКИ
        // ============================================================
        private string GenInsight(int streak, double consistency,
            double tomorrowSkipRiskPct, int total, int days)
        {
            if (consistency >= 80 && streak >= 5)
                return $"Ти на підйомі! {streak} днів поспіль і {consistency}% — звичка майже автоматична.";
            if (streak >= 14)
                return $"Неймовірно — {streak} днів поспіль! Мозок вже формує автоматизм.";
            if (streak >= 7)
                return $"Тиждень без перерви — реальне досягнення. Ще трохи і стане легше.";
            if (tomorrowSkipRiskPct > 50)
                return "Є ризик зупинитись. Сьогодні важливо виконати хоча б мінімум.";
            if (consistency < 30)
                return "Поки важко — але ти продовжуєш. Кожен день рахується.";
            if (total >= 21)
                return $"Ти вже виконала цю звичку {total} разів — мозок запам'ятовує паттерн.";
            return $"Стабільний прогрес за {days} днів. Продовжуй у тому ж темпі.";
        }

        private string GenTip(List<WeekdayRisk> risks, double tomorrowSkipRiskPct,
            int streak, double consistency)
        {
            if (!risks.Any())
                return "Додай перші виконання щоб отримати поради.";
            var worst = risks.OrderByDescending(r => r.RiskScore).First();
            var best = risks.OrderBy(r => r.RiskScore).First();
            if (tomorrowSkipRiskPct > 50)
                return "Постав нагадування прямо зараз. Після пропуску повернутись вдвічі важче.";
            if (streak >= 3)
                return $"Твій слабкий день — {worst.DayName} ({worst.CompletionRate}% виконань). " +
                       $"Заплануй щось просте заздалегідь.";
            return $"Найлегше тобі дається {best.DayName} ({best.CompletionRate}%). " +
                   $"У {worst.DayName} постав нагадування на ранок.";
        }
    }
}