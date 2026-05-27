using HabitFlow.BLL.Services;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace HabitFlow.Tests
{
    public class AnalyticsServiceTests
    {
        [Fact]
        public async Task GetHabitAnalyticsAsync_IgnoresFutureLogs_AndCapsConsistencyAt100()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;
            var startDate = today.AddDays(-3);

            var habit = new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Test habit",
                StartDate = startDate,
                FrequencyType = FrequencyType.Daily,
            };

            var logs = new List<HabitLog>();

            for (var date = startDate; date <= today; date = date.AddDays(1))
            {
                logs.Add(CreateCompletedLog(habitId, userId, date));
            }

            for (int i = 1; i <= 66; i++)
            {
                logs.Add(CreateCompletedLog(habitId, userId, today.AddDays(i)));
            }

            var service = CreateService(habit, logs);

            var result = await service.GetHabitAnalyticsAsync(habitId, userId);

            Assert.Equal(4, result.DaysSinceStart);
            Assert.Equal(4, result.TotalCompleted);
            Assert.Equal(100, result.ConsistencyRate);
            Assert.InRange(result.HabitStrengthScore, 0, 100);
        }

        [Fact]
        public async Task GetHabitAnalyticsAsync_CalculatesConsistencyRate_ForPartialProgress()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;
            var startDate = today.AddDays(-62);

            var habit = new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Reading",
                StartDate = startDate,
                FrequencyType = FrequencyType.Daily,
            };

            var logs = new List<HabitLog>();

            for (int i = 0; i < 61; i++)
            {
                logs.Add(CreateCompletedLog(habitId, userId, startDate.AddDays(i)));
            }

            var service = CreateService(habit, logs);

            var result = await service.GetHabitAnalyticsAsync(habitId, userId);

            Assert.Equal(63, result.DaysSinceStart);
            Assert.Equal(61, result.TotalCompleted);
            Assert.Equal(96.83, result.ConsistencyRate);
            Assert.InRange(result.HabitStrengthScore, 0, 100);
        }

        [Fact]
        public async Task GetHabitAnalyticsAsync_ReturnsEmptyModel_WhenHabitDoesNotBelongToUser()
        {
            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = new Habit
            {
                Id = habitId,
                UserId = ownerId,
                Name = "Workout",
                StartDate = DateTime.Today.AddDays(-10),
                FrequencyType = FrequencyType.Daily,
            };

            var service = CreateService(habit, new List<HabitLog>());

            var result = await service.GetHabitAnalyticsAsync(habitId, otherUserId);

            Assert.Equal(string.Empty, result.HabitName);
            Assert.Equal(0, result.TotalCompleted);
            Assert.Equal(0, result.ConsistencyRate);
        }

        [Fact]
        public async Task GetHabitAnalyticsAsync_ReturnsSevenDayForecast()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;
            var startDate = today.AddDays(-9);

            var habit = new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Water",
                StartDate = startDate,
                FrequencyType = FrequencyType.Daily,
            };

            var logs = new List<HabitLog>();

            for (int i = 0; i < 10; i += 2)
            {
                logs.Add(CreateCompletedLog(habitId, userId, startDate.AddDays(i)));
            }

            var service = CreateService(habit, logs);

            var result = await service.GetHabitAnalyticsAsync(habitId, userId);

            Assert.Equal(7, result.Next7DaysProbabilities.Count);
            Assert.All(result.Next7DaysProbabilities, p => Assert.InRange(p, 0, 100));
            Assert.InRange(result.BreakRisk, 0, 100);
        }

        private static AnalyticsService CreateService(Habit habit, List<HabitLog> logs)
        {
            return new AnalyticsService(
                new FakeHabitRepository(habit),
                new FakeHabitLogRepository(logs),
                new FakeSharedHabitRepository(),
                NullLogger<AnalyticsService>.Instance);
        }

        private static HabitLog CreateCompletedLog(
            Guid habitId,
            Guid userId,
            DateTime date)
        {
            return new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                ScheduledDate = date.Date,
                CompletedAt = date.Date,
                Status = LogStatus.Completed,
            };
        }

        private sealed class FakeHabitRepository : IHabitRepository
        {
            private readonly Habit? habit;

            public FakeHabitRepository(Habit? habit)
            {
                this.habit = habit;
            }

            public Task<Habit?> GetByIdAsync(Guid id)
            {
                return Task.FromResult(this.habit?.Id == id ? this.habit : null);
            }

            public Task<List<Habit>> GetByUserIdAsync(Guid userId)
            {
                var habits = this.habit != null && this.habit.UserId == userId
                    ? new List<Habit> { this.habit }
                    : new List<Habit>();

                return Task.FromResult(habits);
            }

            public Task AddAsync(Habit habit)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(Habit habit)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(Guid id)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeHabitLogRepository : IHabitLogRepository
        {
            private readonly List<HabitLog> logs;

            public FakeHabitLogRepository(List<HabitLog> logs)
            {
                this.logs = logs;
            }
            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId, Guid userId)
            {
                return Task.FromResult(
                    logs.Where(l => l.HabitId == habitId && l.UserId == userId).ToList());
            }

            public Task<HabitLog?> GetByDateAsync(Guid habitId, DateTime date)
            {
                return Task.FromResult(this.logs.FirstOrDefault(l =>
                    l.HabitId == habitId &&
                    l.ScheduledDate.Date == date.Date));
            }

            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId)
            {
                return Task.FromResult(this.logs
                    .Where(l => l.HabitId == habitId)
                    .ToList());
            }

            public Task<int> GetCompletedCountByUserIdAsync(Guid userId)
            {
                return Task.FromResult(this.logs.Count(l =>
                    l.UserId == userId &&
                    l.Status == LogStatus.Completed));
            }

            public Task AddAsync(HabitLog log)
            {
                this.logs.Add(log);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(HabitLog log)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeSharedHabitRepository : ISharedHabitRepository
        {
            public Task AddParticipantAsync(HabitParticipant participant)
            {
                return Task.CompletedTask;
            }

            public Task<HabitParticipant?> GetParticipantAsync(Guid habitId, Guid userId)
            {
                return Task.FromResult<HabitParticipant?>(null);
            }

            public Task<List<Habit>> GetSharedHabitsByUserIdAsync(Guid userId)
            {
                return Task.FromResult(new List<Habit>());
            }

            public Task<List<HabitParticipant>> GetParticipantsByHabitIdAsync(Guid habitId)
            {
                return Task.FromResult(new List<HabitParticipant>());
            }

            public Task AddInvitationAsync(HabitInvitation invitation)
            {
                return Task.CompletedTask;
            }

            public Task<HabitInvitation?> GetInvitationByTokenAsync(string token)
            {
                return Task.FromResult<HabitInvitation?>(null);
            }

            public Task<HabitInvitation?> GetPendingInvitationAsync(Guid habitId, Guid inviteeUserId)
            {
                return Task.FromResult<HabitInvitation?>(null);
            }

            public Task UpdateInvitationAsync(HabitInvitation invitation)
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task GetHabitAnalyticsAsync_IgnoresSkippedLogs()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;
            var startDate = today.AddDays(-4);

            var habit = new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Test",
                StartDate = startDate,
                FrequencyType = FrequencyType.Daily,
            };

            var logs = new List<HabitLog>
    {
        CreateCompletedLog(habitId, userId, startDate),
        new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habitId,
            UserId = userId,
            ScheduledDate = startDate.AddDays(1),
            Status = LogStatus.Skipped,
        },
    };

            var service = CreateService(habit, logs);

            var result = await service.GetHabitAnalyticsAsync(habitId, userId);

            Assert.Equal(5, result.DaysSinceStart);
            Assert.Equal(1, result.TotalCompleted);
            Assert.Equal(20, result.ConsistencyRate);
        }

        [Fact]
        public async Task GetHabitAnalyticsAsync_CurrentStreakCanUseYesterday_WhenTodayNotCompleted()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;
            var startDate = today.AddDays(-4);

            var habit = new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Test",
                StartDate = startDate,
                FrequencyType = FrequencyType.Daily,
            };

            var logs = new List<HabitLog>
    {
        CreateCompletedLog(habitId, userId, today.AddDays(-3)),
        CreateCompletedLog(habitId, userId, today.AddDays(-2)),
        CreateCompletedLog(habitId, userId, today.AddDays(-1)),
    };

            var service = CreateService(habit, logs);

            var result = await service.GetHabitAnalyticsAsync(habitId, userId);

            Assert.Equal(3, result.CurrentStreak);
            Assert.Equal(3, result.MaxStreak);
        }

        [Fact]
        public async Task GetHabitAnalyticsAsync_ReturnsZeroMetrics_WhenThereAreNoLogs()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Empty habit",
                StartDate = DateTime.Today.AddDays(-9),
                FrequencyType = FrequencyType.Daily,
            };

            var service = CreateService(habit, new List<HabitLog>());

            var result = await service.GetHabitAnalyticsAsync(habitId, userId);

            Assert.Equal(10, result.DaysSinceStart);
            Assert.Equal(0, result.TotalCompleted);
            Assert.Equal(0, result.CurrentStreak);
            Assert.Equal(0, result.MaxStreak);
            Assert.Equal(0, result.ConsistencyRate);
            Assert.InRange(result.HabitStrengthScore, 0, 100);
        }
    }
}