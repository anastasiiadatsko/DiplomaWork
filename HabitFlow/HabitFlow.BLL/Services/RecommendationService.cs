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

        public RecommendationService(
            IHabitRepository habitRepository,
            IHabitLogRepository habitLogRepository,
            IUserRepository userRepository)
        {
            this.habitRepository = habitRepository;
            this.habitLogRepository = habitLogRepository;
            this.userRepository = userRepository;
        }

        public async Task<List<RecommendationViewModel>> GetRecommendationsAsync(Guid userId)
        {
            var recommendations = new List<RecommendationViewModel>();

            var habits = await this.habitRepository.GetByUserIdAsync(userId);
            var user = await this.userRepository.GetByIdAsync(userId);

            foreach (var habit in habits.Where(h => h.IsActive))
            {
                var logs = await this.habitLogRepository.GetByHabitIdAsync(habit.Id);
                var completedLogs = logs
                    .Where(l => l.Status == LogStatus.Completed)
                    .OrderBy(l => l.ScheduledDate)
                    .ToList();

                var skippedOrFailedLogs = logs
                    .Where(l => l.Status == LogStatus.Skipped || l.Status == LogStatus.Failed)
                    .OrderByDescending(l => l.ScheduledDate)
                    .ToList();

                var today = DateTime.UtcNow.Date;
                var yesterday = today.AddDays(-1);

                var completedDates = completedLogs
                    .Select(l => l.ScheduledDate.Date)
                    .ToHashSet();

                var currentStreak = this.CalculateCurrentStreak(completedDates, today);
                var wasCompletedYesterday = completedDates.Contains(yesterday);
                var isCompletedToday = completedDates.Contains(today);

                /*if (currentStreak >= 5 && !isCompletedToday)
                {
                    recommendations.Add(new RecommendationViewModel
                    {
                        Title = "Ризик зриву серії",
                        Message = $"Ти тримала серію {currentStreak} днів у звичці «{habit.Name}». Не зупиняйся сьогодні — виконай хоча б мінімальну версію.",
                        Icon = "🔥",
                        Type = "Warning",
                        Priority = 1,
                        HabitId = habit.Id,
                        HabitName = habit.Name,
                    });
                }*/
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
                        Title = "Ризик зриву серії",
                        Message = $"Ти маєш хорошу серію у звичці «{habit.Name}», але сьогодні ще не виконав її. Спробуй зробити хоча б мінімальну версію, щоб не втратити темп.",
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
                        Message = $"Звичка «{habit.Name}» була виконана вчора. Сьогодні важливо повернутися до неї, навіть якщо у спрощеному форматі.",
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
                        Message = $"Стабільність звички «{habit.Name}» нижча за 50%. Можливо, вона занадто складна. Спробуй зменшити обсяг дії або перенести її на зручніший час.",
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
                        Message = $"Для звички «{habit.Name}» найчастіше виникають пропуски у день: {riskyDay}. Заплануй на цей день легшу версію звички або додай нагадування.",
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

        private string? FindMostRiskyDay(List<HabitFlow.Domain.Entities.HabitLog> logs)
        {
            var riskyLogs = logs
                .Where(l => l.Status == LogStatus.Skipped || l.Status == LogStatus.Failed)
                .ToList();

            if (riskyLogs.Count < 2)
            {
                return null;
            }

            var mostRiskyDay = riskyLogs
                .GroupBy(l => l.ScheduledDate.DayOfWeek)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;

            return mostRiskyDay switch
            {
                DayOfWeek.Monday => "понеділок",
                DayOfWeek.Tuesday => "вівторок",
                DayOfWeek.Wednesday => "середа",
                DayOfWeek.Thursday => "четвер",
                DayOfWeek.Friday => "п’ятниця",
                DayOfWeek.Saturday => "субота",
                DayOfWeek.Sunday => "неділя",
                _ => string.Empty,
            };
        }

        private RecommendationViewModel? BuildBalanceWheelRecommendation(string? onboardingDescription)
        {
            if (string.IsNullOrWhiteSpace(onboardingDescription) ||
                !onboardingDescription.Contains("BalanceWheel"))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(onboardingDescription);
                var root = document.RootElement;

                if (!root.TryGetProperty("BalanceWheel", out var balanceElement))
                {
                    return null;
                }

                var areas = new Dictionary<string, (int Value, string HabitSuggestion)>
                {
                    { "Здоров’я", (balanceElement.GetProperty("Health").GetInt32(), "10 хв прогулянки або склянка води зранку") },
                    { "Кар’єра / навчання", (balanceElement.GetProperty("Career").GetInt32(), "25 хв навчання без телефону") },
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