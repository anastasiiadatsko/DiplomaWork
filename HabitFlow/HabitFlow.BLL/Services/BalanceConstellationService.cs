using System.Text.Json;
using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;

namespace HabitFlow.BLL.Services
{
    public class BalanceConstellationService : IBalanceConstellationService
    {
        private readonly IUserRepository userRepository;
        private readonly IHabitRepository habitRepository;
        private readonly IHabitLogRepository habitLogRepository;

        public BalanceConstellationService(
            IUserRepository userRepository,
            IHabitRepository habitRepository,
            IHabitLogRepository habitLogRepository)
        {
            this.userRepository = userRepository;
            this.habitRepository = habitRepository;
            this.habitLogRepository = habitLogRepository;
        }

        public async Task<BalanceConstellationViewModel> GetConstellationAsync(Guid userId)
        {
            var user = await this.userRepository.GetByIdAsync(userId);
            var habits = await this.habitRepository.GetByUserIdAsync(userId);

            var selfScores = this.GetSelfScores(user?.OnboardingDescription);

            var areas = new List<BalanceConstellationAreaDto>
            {
                this.CreateArea("Health", "Здоров’я", "💚", selfScores.Health),
                this.CreateArea("Career", "Кар’єра / навчання", "🎓", selfScores.Career),
                this.CreateArea("Finance", "Фінанси", "💰", selfScores.Finance),
                this.CreateArea("Relationships", "Стосунки", "🤝", selfScores.Relationships),
                this.CreateArea("SelfDevelopment", "Саморозвиток", "📚", selfScores.SelfDevelopment),
                this.CreateArea("Rest", "Відпочинок", "🌙", selfScores.Rest),
                this.CreateArea("EmotionalState", "Емоційний стан", "🧘", selfScores.EmotionalState),
                this.CreateArea("Environment", "Побут / оточення", "🏡", selfScores.Environment),
            };

            foreach (var habit in habits.Where(h => h.IsActive))
            {
                var areaKey = this.MapCategoryToAreaKey(habit.Category);
                var area = areas.FirstOrDefault(a => a.Key == areaKey);

                if (area == null)
                {
                    continue;
                }

                var logs = await this.habitLogRepository.GetByHabitIdAsync(habit.Id);

                var completedLogs = logs
                    .Where(l => l.UserId == userId && l.Status == LogStatus.Completed)
                    .Select(l => l.ScheduledDate.Date)
                    .Distinct()
                    .ToList();

                var daysSinceStart = Math.Max(
                    1,
                    (DateTime.Today - habit.StartDate.Date).Days + 1);

                var consistency = Math.Min(
                    completedLogs.Count * 100.0 / daysSinceStart,
                    100);

                area.HabitsCount++;
                area.CompletedCount += completedLogs.Count;
                area.ConsistencyRate += consistency;
            }

            foreach (var area in areas)
            {
                if (area.HabitsCount > 0)
                {
                    area.ConsistencyRate = Math.Round(
                        area.ConsistencyRate / area.HabitsCount,
                        1);

                    area.ActivityScore = area.ConsistencyRate;
                }
                else
                {
                    area.ConsistencyRate = 0;
                    area.ActivityScore = 0;
                }

                area.StatusText = this.GetStatusText(area);
            }

            var weakestArea = areas
                .OrderBy(a => a.SelfScore)
                .ThenBy(a => a.ActivityScore)
                .First();

            var mostActiveArea = areas
                .OrderByDescending(a => a.ActivityScore)
                .First();

            weakestArea.IsWeakest = true;
            mostActiveArea.IsMostActive = true;

            return new BalanceConstellationViewModel
            {
                Areas = areas,
                WeakestAreaName = weakestArea.Name,
                MostActiveAreaName = mostActiveArea.Name,
                AverageSelfScore = Math.Round(areas.Average(a => a.SelfScore), 1),
                AverageActivityScore = Math.Round(areas.Average(a => a.ActivityScore), 1),
                MainInsight = this.BuildMainInsight(weakestArea, mostActiveArea),
                RecommendationText = this.BuildRecommendation(weakestArea),
            };
        }

        private BalanceConstellationAreaDto CreateArea(
            string key,
            string name,
            string emoji,
            int selfScore)
        {
            return new BalanceConstellationAreaDto
            {
                Key = key,
                Name = name,
                Emoji = emoji,
                SelfScore = selfScore,
                ActivityScore = 0,
                HabitsCount = 0,
                CompletedCount = 0,
                ConsistencyRate = 0,
            };
        }

        private BalanceWheelDto GetSelfScores(string? onboardingDescription)
        {
            var defaultScores = new BalanceWheelDto
            {
                Health = 5,
                Career = 5,
                Finance = 5,
                Relationships = 5,
                SelfDevelopment = 5,
                Rest = 5,
                EmotionalState = 5,
                Environment = 5,
            };

            if (string.IsNullOrWhiteSpace(onboardingDescription) ||
                !onboardingDescription.Contains("BalanceWheel"))
            {
                return defaultScores;
            }

            try
            {
                using var document = JsonDocument.Parse(onboardingDescription);
                var root = document.RootElement;

                if (!root.TryGetProperty("BalanceWheel", out var balanceElement))
                {
                    return defaultScores;
                }

                return new BalanceWheelDto
                {
                    Health = balanceElement.GetProperty("Health").GetInt32(),
                    Career = balanceElement.GetProperty("Career").GetInt32(),
                    Finance = balanceElement.GetProperty("Finance").GetInt32(),
                    Relationships = balanceElement.GetProperty("Relationships").GetInt32(),
                    SelfDevelopment = balanceElement.GetProperty("SelfDevelopment").GetInt32(),
                    Rest = balanceElement.GetProperty("Rest").GetInt32(),
                    EmotionalState = balanceElement.GetProperty("EmotionalState").GetInt32(),
                    Environment = balanceElement.GetProperty("Environment").GetInt32(),
                };
            }
            catch
            {
                return defaultScores;
            }
        }

        private string MapCategoryToAreaKey(string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return "SelfDevelopment";
            }

            var value = category.Trim().ToLower();

            if (value.Contains("здоров") ||
                value.Contains("спорт") ||
                value.Contains("вода") ||
                value.Contains("сон") ||
                value.Contains("заряд") ||
                value.Contains("прогулян"))
            {
                return "Health";
            }

            if (value.Contains("навч") ||
                value.Contains("кар") ||
                value.Contains("курс") ||
                value.Contains("робот") ||
                value.Contains("англ"))
            {
                return "Career";
            }

            if (value.Contains("фін") ||
                value.Contains("грош") ||
                value.Contains("бюдж") ||
                value.Contains("витрат"))
            {
                return "Finance";
            }

            if (value.Contains("стос") ||
                value.Contains("друз") ||
                value.Contains("мам") ||
                value.Contains("сім") ||
                value.Contains("род"))
            {
                return "Relationships";
            }

            if (value.Contains("самороз") ||
                value.Contains("чит") ||
                value.Contains("книг") ||
                value.Contains("розвит"))
            {
                return "SelfDevelopment";
            }

            if (value.Contains("відпоч") ||
                value.Contains("релакс") ||
                value.Contains("перерв"))
            {
                return "Rest";
            }

            if (value.Contains("емо") ||
                value.Contains("медит") ||
                value.Contains("дих") ||
                value.Contains("щоденник"))
            {
                return "EmotionalState";
            }

            if (value.Contains("побут") ||
                value.Contains("дім") ||
                value.Contains("прибиран") ||
                value.Contains("оточ"))
            {
                return "Environment";
            }

            return "SelfDevelopment";
        }

        private string GetStatusText(BalanceConstellationAreaDto area)
        {
            if (area.HabitsCount == 0)
            {
                return "Немає звичок у цій сфері";
            }

            if (area.ActivityScore >= 80)
            {
                return "Сфера активно підтримується";
            }

            if (area.ActivityScore >= 50)
            {
                return "Є стабільна робота над сферою";
            }

            if (area.ActivityScore >= 20)
            {
                return "Активність поки нестабільна";
            }

            return "Сфера потребує уваги";
        }

        private string BuildMainInsight(
            BalanceConstellationAreaDto weakestArea,
            BalanceConstellationAreaDto mostActiveArea)
        {
            return $"Найнижча самооцінка зараз у сфері «{weakestArea.Name}», " +
                   $"а найактивніше ти працюєш над сферою «{mostActiveArea.Name}».";
        }

        private string BuildRecommendation(BalanceConstellationAreaDto weakestArea)
        {
            if (weakestArea.HabitsCount == 0)
            {
                return $"У сфері «{weakestArea.Name}» ще немає звичок. " +
                       $"Додай одну дуже просту дію, щоб почати підтримувати цю сферу.";
            }

            if (weakestArea.ActivityScore < 50)
            {
                return $"Сфера «{weakestArea.Name}» має низьку самооцінку і невисоку активність. " +
                       $"Спробуй спростити звички або виконувати мінімальну версію.";
            }

            return $"Сфера «{weakestArea.Name}» має низьку самооцінку, " +
                   $"але ти вже працюєш над нею через звички. Продовжуй у цьому темпі.";
        }
    }
}