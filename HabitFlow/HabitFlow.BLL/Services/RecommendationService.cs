using System.Text.Json;
using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;

namespace HabitFlow.BLL.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly IHabitRepository habitRepository;
        private readonly IHabitLogRepository habitLogRepository;
        private readonly IUserRepository userRepository;
        private readonly ITriggerLogRepository triggerLogRepository;

        public RecommendationService(
            IHabitRepository habitRepository,
            IHabitLogRepository habitLogRepository,
            IUserRepository userRepository,
            ITriggerLogRepository triggerLogRepository)
        {
            this.habitRepository = habitRepository;
            this.habitLogRepository = habitLogRepository;
            this.userRepository = userRepository;
            this.triggerLogRepository = triggerLogRepository;
        }

        public async Task<List<RecommendationViewModel>> GetRecommendationsAsync(Guid userId)
        {
            var recommendations = new List<RecommendationViewModel>();

            var habits = await this.habitRepository.GetByUserIdAsync(userId);
            var user = await this.userRepository.GetByIdAsync(userId);

            foreach (var habit in habits.Where(h => h.IsActive))
            {
                var logs = await this.habitLogRepository.GetByHabitIdAsync(habit.Id);
                var today = DateTime.UtcNow.Date;
                var yesterday = today.AddDays(-1);

                // ===== QUIT HABITS =====
                if (habit.Mode == HabitMode.Quit)
                {
                    var triggerLogs = await this.triggerLogRepository.GetByHabitAndUserAsync(habit.Id, userId);
                    var userLogs = logs.Where(l => l.UserId == userId).ToList();

                    var relapses = triggerLogs.Count(t => t.DidRelapse);
                    var cleanDays = this.CalculateCleanDays(userLogs);
                    var recentCravings = triggerLogs
                        .Where(t => t.OccurredAt >= today.AddDays(-3) && !t.DidRelapse)
                        .Count();

                    // Нещодавній зрив
                    var recentRelapse = triggerLogs
                        .Where(t => t.DidRelapse)
                        .OrderByDescending(t => t.OccurredAt)
                        .FirstOrDefault();

                    if (recentRelapse != null && recentRelapse.OccurredAt.Date >= today.AddDays(-2))
                    {
                        recommendations.Add(new RecommendationViewModel
                        {
                            Title = "Після зриву — не здавайся",
                            Message = $"Ти мала зрив у «{habit.Name}». Це нормальна частина процесу. Один крок назад — не кінець. Починай знову сьогодні.",
                            Icon = "💪",
                            Type = "Warning",
                            Priority = 1,
                            HabitId = habit.Id,
                            HabitName = habit.Name,
                        });
                    }
                    // Багато потягів за останні дні
                    else if (recentCravings >= 3)
                    {
                        recommendations.Add(new RecommendationViewModel
                        {
                            Title = "Підвищений ризик зриву",
                            Message = $"За останні 3 дні у «{habit.Name}» зафіксовано {recentCravings} потягів. Спробуй уникнути тригерів або зверніться до кризового агента.",
                            Icon = "⚠️",
                            Type = "Warning",
                            Priority = 1,
                            HabitId = habit.Id,
                            HabitName = habit.Name,
                        });
                    }
                    // Досягнення milestone
                    else if (cleanDays == 7 || cleanDays == 14 || cleanDays == 30 || cleanDays == 100)
                    {
                        recommendations.Add(new RecommendationViewModel
                        {
                            Title = $"🎉 {cleanDays} чистих днів!",
                            Message = $"Вітаємо! Ти досягла важливої позначки у «{habit.Name}». Це величезний прогрес — продовжуй у тому ж дусі!",
                            Icon = "🏆",
                            Type = "Advice",
                            Priority = 2,
                            HabitId = habit.Id,
                            HabitName = habit.Name,
                        });
                    }
                    // Перші дні відмови
                    else if (cleanDays <= 3 && cleanDays > 0)
                    {
                        recommendations.Add(new RecommendationViewModel
                        {
                            Title = "Перші дні — найважчі",
                            Message = $"Ти вже {cleanDays} {(cleanDays == 1 ? "день" : "дні")} без «{habit.Name}». Перший тиждень найскладніший — тримайся, стане легше!",
                            Icon = "🌱",
                            Type = "Reminder",
                            Priority = 3,
                            HabitId = habit.Id,
                            HabitName = habit.Name,
                        });
                    }

                    continue; // Не застосовувати звичайні рекомендації до quit-звичок
                }

                // ===== REGULAR HABITS =====
                var completedLogs = logs
                    .Where(l => l.Status == LogStatus.Completed)
                    .OrderBy(l => l.ScheduledDate)
                    .ToList();

                var completedDates = completedLogs
                    .Select(l => l.ScheduledDate.Date)
                    .ToHashSet();

                var currentStreak = this.CalculateCurrentStreak(completedDates, today);
                var wasCompletedYesterday = completedDates.Contains(yesterday);
                var isCompletedToday = completedDates.Contains(today);

                if (wasCompletedYesterday && !isCompletedToday && completedLogs.Count >= 5)
                {
                    recommendations.Add(new RecommendationViewModel
                    {
                        Title = "Ризик зриву серії",
                        Message = $"Ти тримала серію у звичці «{habit.Name}», але сьогодні ще не виконала її. Зроби хоча б мінімум, щоб не втратити прогрес.",
                        Icon = "🔥",
                        Type = "Warning",
                        Priority = 1,
                        HabitId = habit.Id,
                        HabitName = habit.Name,
                    });
                }

                if (currentStreak >= 7 && !isCompletedToday)
                {
                    recommendations.Add(new RecommendationViewModel
                    {
                        Title = "Не переривай серію",
                        Message = $"Ти маєш серію {currentStreak} днів у «{habit.Name}»! Сьогодні ще не виконала — не дай їй перерватися.",
                        Icon = "⚠️",
                        Type = "Warning",
                        Priority = 1,
                        HabitId = habit.Id,
                        HabitName = habit.Name,
                    });
                }

                if (wasCompletedYesterday && !isCompletedToday)
                {
                    recommendations.Add(new RecommendationViewModel
                    {
                        Title = "Не пропусти другий день",
                        Message = $"Звичка «{habit.Name}» була виконана вчора. Сьогодні важливо повернутися до неї, навіть у спрощеному форматі.",
                        Icon = "🔥",
                        Type = "Reminder",
                        Priority = 2,
                        HabitId = habit.Id,
                        HabitName = habit.Name,
                    });
                }

                var daysSinceStart = Math.Max(1, (today - habit.StartDate.Date).Days + 1);
                var consistencyRate = (double)completedLogs.Count / daysSinceStart * 100;

                if (daysSinceStart >= 7 && consistencyRate < 50)
                {
                    recommendations.Add(new RecommendationViewModel
                    {
                        Title = "Звичка поки нестабільна",
                        Message = $"Стабільність звички «{habit.Name}» нижча за 50%. Можливо, вона занадто складна. Спробуй зменшити обсяг або перенести на зручніший час.",
                        Icon = "🌱",
                        Type = "Advice",
                        Priority = 3,
                        HabitId = habit.Id,
                        HabitName = habit.Name,
                    });
                }

                var riskyDay = this.FindMostRiskyDay(logs);
                if (!string.IsNullOrEmpty(riskyDay))
                {
                    recommendations.Add(new RecommendationViewModel
                    {
                        Title = "Ризиковий день тижня",
                        Message = $"Для звички «{habit.Name}» найчастіше пропуски у {riskyDay}. Заплануй легшу версію або додай нагадування на цей день.",
                        Icon = "📅",
                        Type = "Insight",
                        Priority = 4,
                        HabitId = habit.Id,
                        HabitName = habit.Name,
                    });
                }
            }

            var balanceRecommendation = this.BuildBalanceWheelRecommendation(user?.OnboardingDescription);
            if (balanceRecommendation != null)
            {
                recommendations.Add(balanceRecommendation);
            }

            return recommendations
                .OrderBy(r => r.Priority)
                .Take(5)
                .ToList();
        }

        private int CalculateCurrentStreak(HashSet<DateTime> completedDates, DateTime today)
        {
            var streak = 0;
            var date = today;
            while (completedDates.Contains(date))
            {
                streak++;
                date = date.AddDays(-1);
            }
            return streak;
        }

        private int CalculateCleanDays(List<HabitFlow.Domain.Entities.HabitLog> habitLogs)
        {
            var ordered = habitLogs
                .OrderByDescending(l => l.ScheduledDate.Date)
                .ToList();

            var cleanDays = 0;
            foreach (var log in ordered)
            {
                if (log.Status == LogStatus.Failed) break;
                if (log.Status == LogStatus.Completed) cleanDays++;
            }
            return cleanDays;
        }

        private string? FindMostRiskyDay(List<HabitFlow.Domain.Entities.HabitLog> logs)
        {
            var riskyLogs = logs
                .Where(l => l.Status == LogStatus.Skipped || l.Status == LogStatus.Failed)
                .ToList();

            if (riskyLogs.Count < 2) return null;

            var mostRiskyDay = riskyLogs
                .GroupBy(l => l.ScheduledDate.DayOfWeek)
                .OrderByDescending(g => g.Count())
                .First().Key;

            return mostRiskyDay switch
            {
                DayOfWeek.Monday => "понеділок",
                DayOfWeek.Tuesday => "вівторок",
                DayOfWeek.Wednesday => "середу",
                DayOfWeek.Thursday => "четвер",
                DayOfWeek.Friday => "п'ятницю",
                DayOfWeek.Saturday => "суботу",
                DayOfWeek.Sunday => "неділю",
                _ => string.Empty,
            };
        }

        private RecommendationViewModel? BuildBalanceWheelRecommendation(string? onboardingDescription)
        {
            if (string.IsNullOrWhiteSpace(onboardingDescription) ||
                !onboardingDescription.Contains("BalanceWheel"))
                return null;

            try
            {
                using var document = JsonDocument.Parse(onboardingDescription);
                var root = document.RootElement;
                if (!root.TryGetProperty("BalanceWheel", out var balanceElement)) return null;

                var areas = new Dictionary<string, (int Value, string HabitSuggestion)>
                {
                    { "Здоров'я", (balanceElement.GetProperty("Health").GetInt32(), "10 хв прогулянки або склянка води зранку") },
                    { "Кар'єра / навчання", (balanceElement.GetProperty("Career").GetInt32(), "25 хв навчання без телефону") },
                    { "Фінанси", (balanceElement.GetProperty("Finance").GetInt32(), "записати витрати дня") },
                    { "Стосунки", (balanceElement.GetProperty("Relationships").GetInt32(), "написати або подзвонити близькій людині") },
                    { "Саморозвиток", (balanceElement.GetProperty("SelfDevelopment").GetInt32(), "прочитати 5 сторінок книги") },
                    { "Відпочинок", (balanceElement.GetProperty("Rest").GetInt32(), "30 хв відпочинку без телефону") },
                    { "Емоційний стан", (balanceElement.GetProperty("EmotionalState").GetInt32(), "5 хв дихальної практики") },
                    { "Побут / оточення", (balanceElement.GetProperty("Environment").GetInt32(), "прибрати одну маленьку зону вдома") },
                };

                var weakestArea = areas.OrderBy(a => a.Value.Value).First();

                return new RecommendationViewModel
                {
                    Title = "Рекомендована нова звичка",
                    Message = $"За колесом балансу найменше оцінена сфера — {weakestArea.Key}. Спробуй додати просту звичку: {weakestArea.Value.HabitSuggestion}.",
                    Icon = "🧭",
                    Type = "Suggestion",
                    Priority = 5,
                };
            }
            catch
            {
                return null;
            }
        }
    }
}