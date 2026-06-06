using HabitFlow.BLL.Services;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace HabitFlow.Tests
{
    public class QuitAnalyticsServiceTests
    {
        [Fact]
        public async Task GetAnalyticsAsync_ReturnsEmpty_WhenNoLogs()
        {
            var userId = Guid.NewGuid();
            var service = CreateService(new List<TriggerLog>());

            var result = await service.GetAnalyticsAsync(userId);

            Assert.Equal(0, result.CleanDays);
            Assert.Equal(0, result.RelapseCount);
            Assert.Equal(0, result.WonCravingsCount);
            Assert.Equal(0, result.TotalCravingsCount);
            Assert.Equal(0, result.AverageCravingIntensity);
            Assert.Equal(0, result.RelapseRisk);
            Assert.False(string.IsNullOrWhiteSpace(result.MainInsight));
            Assert.False(string.IsNullOrWhiteSpace(result.ActionTip));
        }

        [Fact]
        public async Task GetAnalyticsAsync_CalculatesRelapseCount_Correctly()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: true, cravingLevel: 7),
                CreateLog(userId, didRelapse: true, cravingLevel: 8),
                CreateLog(userId, didRelapse: false, cravingLevel: 5),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.Equal(2, result.RelapseCount);
            Assert.Equal(1, result.WonCravingsCount);
            Assert.Equal(3, result.TotalCravingsCount);
        }

        [Fact]
        public async Task GetAnalyticsAsync_CalculatesAverageIntensity_Correctly()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: false, cravingLevel: 4),
                CreateLog(userId, didRelapse: false, cravingLevel: 6),
                CreateLog(userId, didRelapse: false, cravingLevel: 8),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.Equal(6.0, result.AverageCravingIntensity);
        }

        [Fact]
        public async Task GetAnalyticsAsync_CleanDays_IsZero_WhenRelapseToday()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: true, cravingLevel: 7, occurredAt: DateTime.UtcNow),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.Equal(0, result.CleanDays);
        }

        [Fact]
        public async Task GetAnalyticsAsync_CleanDays_CountedFromLastRelapse()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: true, cravingLevel: 5, occurredAt: DateTime.UtcNow.AddDays(-5)),
                CreateLog(userId, didRelapse: false, cravingLevel: 3, occurredAt: DateTime.UtcNow.AddDays(-2)),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.Equal(5, result.CleanDays);
        }

        [Fact]
        public async Task GetAnalyticsAsync_CleanDays_CountedFromFirstLog_WhenNoRelapse()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: false, cravingLevel: 3, occurredAt: DateTime.UtcNow.AddDays(-10)),
                CreateLog(userId, didRelapse: false, cravingLevel: 4, occurredAt: DateTime.UtcNow.AddDays(-5)),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.Equal(10, result.CleanDays);
        }

        [Fact]
        public async Task GetAnalyticsAsync_RelapseRisk_IsHigh_WhenManyRecentRelapses()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: true, cravingLevel: 9, occurredAt: DateTime.UtcNow.AddHours(-1)),
                CreateLog(userId, didRelapse: true, cravingLevel: 8, occurredAt: DateTime.UtcNow.AddHours(-2)),
                CreateLog(userId, didRelapse: true, cravingLevel: 7, occurredAt: DateTime.UtcNow.AddHours(-3)),
                CreateLog(userId, didRelapse: true, cravingLevel: 9, occurredAt: DateTime.UtcNow.AddHours(-4)),
                CreateLog(userId, didRelapse: true, cravingLevel: 8, occurredAt: DateTime.UtcNow.AddHours(-5)),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.True(result.RelapseRisk >= 70);
        }

        [Fact]
        public async Task GetAnalyticsAsync_RelapseRisk_IsLow_WhenNoRecentRelapses()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: false, cravingLevel: 2, occurredAt: DateTime.UtcNow.AddHours(-1)),
                CreateLog(userId, didRelapse: false, cravingLevel: 2, occurredAt: DateTime.UtcNow.AddHours(-2)),
                CreateLog(userId, didRelapse: false, cravingLevel: 2, occurredAt: DateTime.UtcNow.AddHours(-3)),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.True(result.RelapseRisk < 30);
        }

        [Fact]
        public async Task GetAnalyticsAsync_MostDangerousTriggers_ReturnTopFive()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: true, cravingLevel: 8, triggerType: TriggerType.Stress),
                CreateLog(userId, didRelapse: true, cravingLevel: 7, triggerType: TriggerType.Stress),
                CreateLog(userId, didRelapse: false, cravingLevel: 5, triggerType: TriggerType.Boredom),
                CreateLog(userId, didRelapse: true, cravingLevel: 9, triggerType: TriggerType.Alcohol),
                CreateLog(userId, didRelapse: false, cravingLevel: 4, triggerType: TriggerType.AfterMeal),
                CreateLog(userId, didRelapse: false, cravingLevel: 3, triggerType: TriggerType.Morning),
                CreateLog(userId, didRelapse: true, cravingLevel: 6, triggerType: TriggerType.EmotionalPain),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.True(result.MostDangerousTriggers.Count <= 5);
        }

        [Fact]
        public async Task GetAnalyticsAsync_MostDangerousTriggers_OrderedByRiskPercent()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: true, cravingLevel: 8, triggerType: TriggerType.Stress),
                CreateLog(userId, didRelapse: true, cravingLevel: 8, triggerType: TriggerType.Stress),
                CreateLog(userId, didRelapse: false, cravingLevel: 5, triggerType: TriggerType.Boredom),
                CreateLog(userId, didRelapse: false, cravingLevel: 5, triggerType: TriggerType.Boredom),
                CreateLog(userId, didRelapse: false, cravingLevel: 5, triggerType: TriggerType.Boredom),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            var riskPercents = result.MostDangerousTriggers.Select(t => t.RiskPercent).ToList();
            Assert.Equal(riskPercents.OrderByDescending(r => r), riskPercents);
        }

        [Fact]
        public async Task GetAnalyticsAsync_MainInsight_MentionsCleanDays_WhenAbove30()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: false, cravingLevel: 3, occurredAt: DateTime.UtcNow.AddDays(-35)),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.Contains("35", result.MainInsight);
        }

        [Fact]
        public async Task GetAnalyticsAsync_MainInsight_MentionsHighRisk_WhenRiskAbove70()
        {
            var userId = Guid.NewGuid();
            var logs = Enumerable.Range(1, 10)
                .Select(i => CreateLog(userId, didRelapse: true, cravingLevel: 9,
                    occurredAt: DateTime.UtcNow.AddHours(-i)))
                .ToList();

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.True(result.RelapseRisk >= 70);
            Assert.False(string.IsNullOrWhiteSpace(result.MainInsight));
        }

        [Fact]
        public async Task GetAnalyticsAsync_ActionTip_MentionsTriggerName_WhenTriggersExist()
        {
            var userId = Guid.NewGuid();
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: true, cravingLevel: 3, triggerType: TriggerType.Stress),
                CreateLog(userId, didRelapse: false, cravingLevel: 2, triggerType: TriggerType.Stress),
                CreateLog(userId, didRelapse: false, cravingLevel: 2, triggerType: TriggerType.Stress),
                CreateLog(userId, didRelapse: false, cravingLevel: 2, triggerType: TriggerType.Stress),
                CreateLog(userId, didRelapse: false, cravingLevel: 2, triggerType: TriggerType.Stress),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.True(result.RelapseRisk < 70);
            Assert.Contains("Стрес", result.ActionTip);
        }

        [Fact]
        public async Task GetAnalyticsAsync_MostDangerousTime_ReturnsValidPeriod()
        {
            var userId = Guid.NewGuid();
            var eveningTime = DateTime.UtcNow.Date.AddHours(19);
            var logs = new List<TriggerLog>
            {
                CreateLog(userId, didRelapse: true, cravingLevel: 7, occurredAt: eveningTime),
                CreateLog(userId, didRelapse: true, cravingLevel: 8, occurredAt: eveningTime.AddMinutes(30)),
            };

            var service = CreateService(logs);
            var result = await service.GetAnalyticsAsync(userId);

            Assert.False(string.IsNullOrWhiteSpace(result.MostDangerousTime));
            Assert.NotEqual("Недостатньо даних", result.MostDangerousTime);
        }

        private static QuitAnalyticsService CreateService(List<TriggerLog> logs)
        {
            return new QuitAnalyticsService(
                new FakeTriggerLogRepository(logs),
                NullLogger<QuitAnalyticsService>.Instance);
        }

        private static TriggerLog CreateLog(
            Guid userId,
            bool didRelapse,
            int cravingLevel,
            TriggerType triggerType = TriggerType.Stress,
            DateTime? occurredAt = null)
        {
            return new TriggerLog
            {
                Id = Guid.NewGuid(),
                HabitId = Guid.NewGuid(),
                UserId = userId,
                OccurredAt = occurredAt ?? DateTime.UtcNow,
                CravingLevel = cravingLevel,
                TriggerType = triggerType,
                DidRelapse = didRelapse,
                Resisted = !didRelapse,
            };
        }

        private sealed class FakeTriggerLogRepository : ITriggerLogRepository
        {
            private readonly List<TriggerLog> logs;

            public FakeTriggerLogRepository(List<TriggerLog> logs) => this.logs = logs;

            public Task AddAsync(TriggerLog log)
            {
                this.logs.Add(log);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(TriggerLog log) => Task.CompletedTask;

            public Task DeleteAsync(Guid id)
            {
                this.logs.RemoveAll(t => t.Id == id);
                return Task.CompletedTask;
            }

            public Task<TriggerLog?> GetByIdAsync(Guid id) =>
                Task.FromResult(this.logs.FirstOrDefault(t => t.Id == id));

            public Task<List<TriggerLog>> GetByUserIdAsync(Guid userId) =>
                Task.FromResult(this.logs.Where(t => t.UserId == userId).ToList());

            public Task<List<TriggerLog>> GetByUserIdForPeriodAsync(Guid userId, DateTime from, DateTime to) =>
                Task.FromResult(this.logs
                    .Where(t => t.UserId == userId && t.OccurredAt >= from && t.OccurredAt <= to)
                    .ToList());

            public Task<List<TriggerLog>> GetByHabitAndUserAsync(Guid habitId, Guid userId) =>
                Task.FromResult(this.logs
                    .Where(t => t.HabitId == habitId && t.UserId == userId)
                    .ToList());
        }
    }
}