using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace HabitFlow.BLL.Services
{
    public class QuitAnalyticsService : IQuitAnalyticsService
    {
        private readonly ITriggerLogRepository triggerLogRepository;
        private readonly ILogger<QuitAnalyticsService> logger;

        public QuitAnalyticsService(
            ITriggerLogRepository triggerLogRepository,
            ILogger<QuitAnalyticsService> logger)
        {
            this.triggerLogRepository = triggerLogRepository;
            this.logger = logger;
        }

        public async Task<QuitAnalyticsViewModel> GetAnalyticsAsync(Guid userId)
        {
            var logs = await this.triggerLogRepository.GetByUserIdAsync(userId);

            if (!logs.Any())
            {
                return new QuitAnalyticsViewModel
                {
                    CleanDays = 0,
                    RelapseCount = 0,
                    WonCravingsCount = 0,
                    TotalCravingsCount = 0,
                    AverageCravingIntensity = 0,
                    RelapseRisk = 0,
                    MainInsight = "Поки немає записів про потяги або зриви.",
                    ActionTip = "Коли відчуєш потяг, запиши його силу та причину. Так система зможе знайти твої ризикові моменти.",
                };
            }

            var orderedLogs = logs
                .OrderBy(l => l.OccurredAt)
                .ToList();

            var relapseLogs = orderedLogs
                .Where(l => l.DidRelapse)
                .ToList();

            var cravingLogs = orderedLogs
                .Where(l => !l.DidRelapse)
                .ToList();

            var cleanDays = this.CalculateCleanDays(orderedLogs);
            var relapseCount = relapseLogs.Count;
            var wonCravingsCount = cravingLogs.Count;
            var totalCravingsCount = orderedLogs.Count;

            var averageIntensity = Math.Round(
                orderedLogs.Average(l => l.CravingLevel),
                1);

            var mostDangerousTime = this.CalculateMostDangerousTime(relapseLogs, orderedLogs);

            var dangerousTriggers = this.CalculateTriggerStats(orderedLogs);

            var relapseRisk = this.CalculateRelapseRisk(orderedLogs);

            this.logger.LogInformation(
                "Quit-аналітика сформована для користувача {UserId}",
                userId);

            return new QuitAnalyticsViewModel
            {
                CleanDays = cleanDays,
                RelapseCount = relapseCount,
                WonCravingsCount = wonCravingsCount,
                TotalCravingsCount = totalCravingsCount,
                AverageCravingIntensity = averageIntensity,
                MostDangerousTime = mostDangerousTime,
                MostDangerousTriggers = dangerousTriggers,
                RelapseRisk = relapseRisk,
                MainInsight = this.GenerateInsight(cleanDays, relapseCount, wonCravingsCount, relapseRisk),
                ActionTip = this.GenerateTip(dangerousTriggers, mostDangerousTime, relapseRisk),
            };
        }

        private int CalculateCleanDays(List<TriggerLog> logs)
        {
            var lastRelapse = logs
                .Where(l => l.DidRelapse)
                .OrderByDescending(l => l.OccurredAt)
                .FirstOrDefault();

            var startDate = lastRelapse?.OccurredAt.Date
                ?? logs.Min(l => l.OccurredAt.Date);

            return Math.Max(0, (DateTime.UtcNow.Date - startDate).Days);
        }

        private string CalculateMostDangerousTime(
            List<TriggerLog> relapseLogs,
            List<TriggerLog> allLogs)
        {
            var source = relapseLogs.Any()
                ? relapseLogs
                : allLogs;

            var group = source
                .GroupBy(l => this.GetTimePeriod(l.OccurredAt))
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return group?.Key ?? "Недостатньо даних";
        }

        private List<QuitTriggerStatsDto> CalculateTriggerStats(List<TriggerLog> logs)
        {
            return logs
                .GroupBy(l => l.TriggerType)
                .Select(g =>
                {
                    var count = g.Count();
                    var relapseCount = g.Count(l => l.DidRelapse);

                    return new QuitTriggerStatsDto
                    {
                        TriggerType = g.Key,
                        TriggerName = this.GetTriggerName(g.Key),
                        Count = count,
                        RelapseCount = relapseCount,
                        AverageIntensity = Math.Round(g.Average(l => l.CravingLevel), 1),
                        RiskPercent = Math.Round(relapseCount * 100.0 / count, 1),
                    };
                })
                .OrderByDescending(t => t.RiskPercent)
                .ThenByDescending(t => t.Count)
                .Take(5)
                .ToList();
        }

        private double CalculateRelapseRisk(List<TriggerLog> logs)
        {
            if (!logs.Any())
            {
                return 0;
            }

            var recentLogs = logs
                .OrderByDescending(l => l.OccurredAt)
                .Take(10)
                .ToList();

            var relapseRate = recentLogs.Count(l => l.DidRelapse) / (double)recentLogs.Count;
            var avgIntensity = recentLogs.Average(l => l.CravingLevel) / 10.0;

            var risk = 0.60 * relapseRate + 0.40 * avgIntensity;

            return Math.Round(Math.Clamp(risk * 100, 0, 100), 1);
        }

        private string GetTimePeriod(DateTime dateTime)
        {
            var hour = dateTime.ToLocalTime().Hour;

            if (hour >= 5 && hour < 12)
            {
                return "Ранок";
            }

            if (hour >= 12 && hour < 17)
            {
                return "День";
            }

            if (hour >= 17 && hour < 22)
            {
                return "Вечір";
            }

            return "Ніч";
        }

        private string GetTriggerName(TriggerType triggerType)
        {
            return triggerType switch
            {
                TriggerType.Stress => "Стрес",
                TriggerType.Boredom => "Нудьга",
                TriggerType.SocialPressure => "Соціальний тиск",
                TriggerType.Alcohol => "Алкоголь",
                TriggerType.AfterMeal => "Після їжі",
                TriggerType.Morning => "Ранок",
                TriggerType.EmotionalPain => "Емоційний біль",
                TriggerType.Habit => "Автоматична звичка",
                _ => "Інше",
            };
        }

        private string GenerateInsight(
            int cleanDays,
            int relapseCount,
            int wonCravingsCount,
            double relapseRisk)
        {
            if (cleanDays == 0 && relapseCount == 0)
            {
                return "Ти тільки починаєш шлях. Перший запис допоможе побачити твої тригери.";
            }

            if (cleanDays >= 30)
            {
                return $"У тебе вже {cleanDays} чистих днів. Це сильний результат.";
            }

            if (cleanDays >= 7)
            {
                return $"Ти тримаєшся {cleanDays} днів. Перший тиждень уже позаду.";
            }

            if (relapseRisk >= 70)
            {
                return "Зараз ризик зриву підвищений. Варто підготувати простий план дії на найближчі години.";
            }

            if (wonCravingsCount >= 5)
            {
                return $"Ти вже переміг(ла) {wonCravingsCount} потягів. Це показує, що контроль поступово зростає.";
            }

            return "Система вже починає бачити твої патерни. Продовжуй записувати потяги та зриви.";
        }

        private string GenerateTip(
            List<QuitTriggerStatsDto> triggers,
            string dangerousTime,
            double relapseRisk)
        {
            var worstTrigger = triggers.FirstOrDefault();

            if (relapseRisk >= 70)
            {
                return "Зроби паузу на 10 хвилин, відійди від тригера і напиши коротко, що саме зараз відчуваєш.";
            }

            if (worstTrigger != null)
            {
                return $"Найризикованіший тригер зараз — {worstTrigger.TriggerName}. Підготуй альтернативну дію саме для цього випадку.";
            }

            if (dangerousTime != "Недостатньо даних")
            {
                return $"Найчастіше потяги з'являються у період: {dangerousTime}. Заплануй на цей час просту заміну.";
            }

            return "Записуй навіть слабкі потяги. Це допоможе побачити закономірності раніше.";
        }
    }
}