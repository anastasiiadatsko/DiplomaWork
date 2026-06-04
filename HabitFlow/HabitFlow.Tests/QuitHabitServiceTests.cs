using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Services;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;

namespace HabitFlow.Tests
{
    public class QuitHabitServiceTests
    {
        [Fact]
        public async Task LogCleanDayAsync_CreatesCompletedHabitLog()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = CreateQuitHabit(habitId, userId);
            var habitLogRepository = new FakeHabitLogRepository();
            var triggerLogRepository = new FakeTriggerLogRepository();

            var service = new QuitHabitService(
                new FakeHabitRepository(habit),
                habitLogRepository,
                triggerLogRepository);

            await service.LogCleanDayAsync(habitId, userId);

            Assert.Single(habitLogRepository.Logs);
            Assert.Equal(LogStatus.Completed, habitLogRepository.Logs[0].Status);
            Assert.Equal(habitId, habitLogRepository.Logs[0].HabitId);
            Assert.Equal(userId, habitLogRepository.Logs[0].UserId);
        }

        [Fact]
        public async Task LogCravingAsync_CreatesTriggerLogWithoutRelapse()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = CreateQuitHabit(habitId, userId);
            var habitLogRepository = new FakeHabitLogRepository();
            var triggerLogRepository = new FakeTriggerLogRepository();

            var service = new QuitHabitService(
                new FakeHabitRepository(habit),
                habitLogRepository,
                triggerLogRepository);

            await service.LogCravingAsync(habitId, userId, new LogCravingDto
            {
                CravingLevel = 5,
                TriggerType = TriggerType.Stress,
                Note = "Test craving",
            });

            Assert.Single(triggerLogRepository.Logs);
            Assert.False(triggerLogRepository.Logs[0].DidRelapse);
            Assert.True(triggerLogRepository.Logs[0].Resisted);
            Assert.Equal(5, triggerLogRepository.Logs[0].CravingLevel);
        }

        [Fact]
        public async Task LogRelapseAsync_CreatesTriggerLogAndFailedHabitLog()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = CreateQuitHabit(habitId, userId);
            var habitLogRepository = new FakeHabitLogRepository();
            var triggerLogRepository = new FakeTriggerLogRepository();

            var service = new QuitHabitService(
                new FakeHabitRepository(habit),
                habitLogRepository,
                triggerLogRepository);

            await service.LogRelapseAsync(habitId, userId, new LogRelapseDto
            {
                CravingLevel = 8,
                TriggerType = TriggerType.Other,
                Note = "Test relapse",
            });

            Assert.Single(triggerLogRepository.Logs);
            Assert.True(triggerLogRepository.Logs[0].DidRelapse);
            Assert.False(triggerLogRepository.Logs[0].Resisted);

            Assert.Single(habitLogRepository.Logs);
            Assert.Equal(LogStatus.Failed, habitLogRepository.Logs[0].Status);
        }

        [Fact]
        public async Task GetQuitProgressAsync_ReturnsCorrectProgress()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = CreateQuitHabit(habitId, userId);

            var habitLogRepository = new FakeHabitLogRepository(
                CreateHabitLog(habitId, userId, DateTime.UtcNow.Date, LogStatus.Completed),
                CreateHabitLog(habitId, userId, DateTime.UtcNow.Date.AddDays(-1), LogStatus.Completed));

            var triggerLogRepository = new FakeTriggerLogRepository(
                CreateTriggerLog(habitId, userId, 4, false, true),
                CreateTriggerLog(habitId, userId, 8, true, false));

            var service = new QuitHabitService(
                new FakeHabitRepository(habit),
                habitLogRepository,
                triggerLogRepository);

            var progress = await service.GetQuitProgressAsync(habitId, userId);

            Assert.Equal(2, progress.CleanDays);
            Assert.Equal(1, progress.DefeatedCravings);
            Assert.Equal(1, progress.RelapsesCount);
            Assert.Equal(6, progress.AverageCravingLevel);
        }

        private static Habit CreateQuitHabit(Guid habitId, Guid userId)
        {
            return new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Quit smoking",
                Mode = HabitMode.Quit,
                QuitCategory = QuitCategory.Smoking,
                FrequencyType = FrequencyType.Daily,
                StartDate = DateTime.UtcNow.Date,
                IsActive = true,
            };
        }

        private static HabitLog CreateHabitLog(
            Guid habitId,
            Guid userId,
            DateTime date,
            LogStatus status)
        {
            return new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                ScheduledDate = date.Date,
                CompletedAt = status == LogStatus.Completed ? date : null,
                Status = status,
            };
        }

        private static TriggerLog CreateTriggerLog(
            Guid habitId,
            Guid userId,
            int cravingLevel,
            bool didRelapse,
            bool resisted)
        {
            return new TriggerLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                OccurredAt = DateTime.UtcNow,
                CravingLevel = cravingLevel,
                TriggerType = TriggerType.Other,
                DidRelapse = didRelapse,
                Resisted = resisted,
            };
        }

        private sealed class FakeHabitRepository : IHabitRepository
        {
            private readonly Habit habit;

            public FakeHabitRepository(Habit habit)
            {
                this.habit = habit;
            }

            public Task<Habit?> GetByIdAsync(Guid id)
            {
                return Task.FromResult(this.habit.Id == id ? this.habit : null);
            }

            public Task<List<Habit>> GetByUserIdAsync(Guid userId)
            {
                return Task.FromResult(this.habit.UserId == userId
                    ? new List<Habit> { this.habit }
                    : new List<Habit>());
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
            public FakeHabitLogRepository(params HabitLog[] logs)
            {
                this.Logs = logs.ToList();
            }

            public List<HabitLog> Logs { get; }

            public Task<HabitLog?> GetByDateAsync(Guid habitId, DateTime date)
            {
                return Task.FromResult(this.Logs.FirstOrDefault(l =>
                    l.HabitId == habitId &&
                    l.ScheduledDate.Date == date.Date));
            }

            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId)
            {
                return Task.FromResult(this.Logs
                    .Where(l => l.HabitId == habitId)
                    .ToList());
            }

            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId, Guid userId)
            {
                return Task.FromResult(this.Logs
                    .Where(l => l.HabitId == habitId && l.UserId == userId)
                    .ToList());
            }

            public Task<int> GetCompletedCountByUserIdAsync(Guid userId)
            {
                return Task.FromResult(this.Logs.Count(l =>
                    l.UserId == userId &&
                    l.Status == LogStatus.Completed));
            }

            public Task AddAsync(HabitLog log)
            {
                this.Logs.Add(log);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(HabitLog log)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeTriggerLogRepository : ITriggerLogRepository
        {
            public FakeTriggerLogRepository(params TriggerLog[] logs)
            {
                this.Logs = logs.ToList();
            }

            public List<TriggerLog> Logs { get; }

            public Task AddAsync(TriggerLog triggerLog)
            {
                this.Logs.Add(triggerLog);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(TriggerLog triggerLog)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(Guid id)
            {
                var log = this.Logs.FirstOrDefault(l => l.Id == id);

                if (log != null)
                {
                    this.Logs.Remove(log);
                }

                return Task.CompletedTask;
            }

            public Task<TriggerLog?> GetByIdAsync(Guid id)
            {
                return Task.FromResult(this.Logs.FirstOrDefault(l => l.Id == id));
            }

            public Task<List<TriggerLog>> GetByUserIdAsync(Guid userId)
            {
                return Task.FromResult(this.Logs
                    .Where(l => l.UserId == userId)
                    .ToList());
            }

            public Task<List<TriggerLog>> GetByUserIdForPeriodAsync(
                Guid userId,
                DateTime from,
                DateTime to)
            {
                return Task.FromResult(this.Logs
                    .Where(l =>
                        l.UserId == userId &&
                        l.OccurredAt >= from &&
                        l.OccurredAt <= to)
                    .ToList());
            }

            public Task<List<TriggerLog>> GetByHabitAndUserAsync(Guid habitId, Guid userId)
            {
                return Task.FromResult(this.Logs
                    .Where(l => l.HabitId == habitId && l.UserId == userId)
                    .ToList());
            }
        }
    }
}