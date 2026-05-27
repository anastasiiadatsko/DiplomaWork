using System.Text.Json;
using HabitFlow.BLL.Services;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;

namespace HabitFlow.Tests
{
    public class RecommendationServiceTests
    {
        [Fact]
        public async Task GetRecommendationsAsync_AddsBalanceWheelRecommendation_ForWeakestArea()
        {
            var userId = Guid.NewGuid();

            var onboardingJson = JsonSerializer.Serialize(new
            {
                DailyDescription = "test",
                BalanceWheel = new
                {
                    Health = 8,
                    Career = 7,
                    Finance = 2,
                    Relationships = 6,
                    SelfDevelopment = 9,
                    Rest = 4,
                    EmotionalState = 3,
                    Environment = 10,
                },
            });

            var user = new User
            {
                Id = userId,
                Email = "test@test.com",
                Name = "Test",
                PasswordHash = "hash",
                OnboardingDescription = onboardingJson,
            };

            var service = CreateService(
                user,
                habits: new List<Habit>(),
                logs: new List<HabitLog>());

            var result = await service.GetRecommendationsAsync(userId);

            Assert.Contains(result, r =>
                r.Title == "Рекомендована нова звичка" &&
                r.Message.Contains("Фінанси") &&
                r.Message.Contains("записати витрати дня"));
        }

        [Fact]
        public async Task GetRecommendationsAsync_AddsStreakRisk_WhenYesterdayCompletedAndTodayNotCompleted()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.UtcNow.Date;

            var user = CreateUser(userId);

            var habit = new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Читання",
                StartDate = today.AddDays(-10),
                IsActive = true,
                FrequencyType = FrequencyType.Daily,
            };

            var logs = new List<HabitLog>
            {
                CreateCompletedLog(habitId, userId, today.AddDays(-5)),
                CreateCompletedLog(habitId, userId, today.AddDays(-4)),
                CreateCompletedLog(habitId, userId, today.AddDays(-3)),
                CreateCompletedLog(habitId, userId, today.AddDays(-2)),
                CreateCompletedLog(habitId, userId, today.AddDays(-1)),
            };

            var service = CreateService(
                user,
                habits: new List<Habit> { habit },
                logs: logs);

            var result = await service.GetRecommendationsAsync(userId);

            Assert.Contains(result, r =>
                r.Title == "Ризик зриву серії" &&
                r.Type == "Warning" &&
                r.HabitId == habitId);

            Assert.Contains(result, r =>
                r.Title == "Не пропусти другий день" &&
                r.Type == "Reminder" &&
                r.HabitId == habitId);
        }

        [Fact]
        public async Task GetRecommendationsAsync_AddsUnstableHabitAdvice_WhenConsistencyBelow50()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.UtcNow.Date;

            var user = CreateUser(userId);

            var habit = new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Спорт",
                StartDate = today.AddDays(-10),
                IsActive = true,
                FrequencyType = FrequencyType.Daily,
            };

            var logs = new List<HabitLog>
            {
                CreateCompletedLog(habitId, userId, today.AddDays(-9)),
                CreateCompletedLog(habitId, userId, today.AddDays(-8)),
            };

            var service = CreateService(
                user,
                habits: new List<Habit> { habit },
                logs: logs);

            var result = await service.GetRecommendationsAsync(userId);

            Assert.Contains(result, r =>
                r.Title == "Звичка поки нестабільна" &&
                r.Type == "Advice" &&
                r.HabitId == habitId);
        }

        [Fact]
        public async Task GetRecommendationsAsync_AddsRiskyWeekdayInsight_WhenSkippedLogsExist()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var user = CreateUser(userId);

            var habit = new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Вода",
                StartDate = DateTime.UtcNow.Date.AddDays(-14),
                IsActive = true,
                FrequencyType = FrequencyType.Daily,
            };

            var monday = GetPreviousDay(DayOfWeek.Monday);

            var logs = new List<HabitLog>
            {
                CreateLog(habitId, userId, monday, LogStatus.Skipped),
                CreateLog(habitId, userId, monday.AddDays(-7), LogStatus.Failed),
            };

            var service = CreateService(
                user,
                habits: new List<Habit> { habit },
                logs: logs);

            var result = await service.GetRecommendationsAsync(userId);

            Assert.Contains(result, r =>
                r.Title == "Ризиковий день тижня" &&
                r.Type == "Insight" &&
                r.Message.Contains("понеділок"));
        }

        [Fact]
        public async Task GetRecommendationsAsync_ReturnsMaximumFiveRecommendations()
        {
            var userId = Guid.NewGuid();
            var today = DateTime.UtcNow.Date;

            var user = CreateUser(userId);

            var habits = Enumerable.Range(1, 10)
                .Select(i => new Habit
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = $"Habit {i}",
                    StartDate = today.AddDays(-10),
                    IsActive = true,
                    FrequencyType = FrequencyType.Daily,
                })
                .ToList();

            var logs = habits
                .SelectMany(h => new List<HabitLog>
                {
                    CreateCompletedLog(h.Id, userId, today.AddDays(-2)),
                    CreateCompletedLog(h.Id, userId, today.AddDays(-1)),
                })
                .ToList();

            var service = CreateService(user, habits, logs);

            var result = await service.GetRecommendationsAsync(userId);

            Assert.True(result.Count <= 5);
            Assert.Equal(result.OrderBy(r => r.Priority).Select(r => r.Priority), result.Select(r => r.Priority));
        }

        private static RecommendationService CreateService(
            User user,
            List<Habit> habits,
            List<HabitLog> logs)
        {
            return new RecommendationService(
                new FakeHabitRepository(habits),
                new FakeHabitLogRepository(logs),
                new FakeUserRepository(user));
        }

        private static User CreateUser(Guid userId)
        {
            return new User
            {
                Id = userId,
                Email = "test@test.com",
                Name = "Test",
                PasswordHash = "hash",
            };
        }

        private static HabitLog CreateCompletedLog(Guid habitId, Guid userId, DateTime date)
        {
            return CreateLog(habitId, userId, date, LogStatus.Completed);
        }

        private static HabitLog CreateLog(Guid habitId, Guid userId, DateTime date, LogStatus status)
        {
            return new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                ScheduledDate = date.Date,
                CompletedAt = status == LogStatus.Completed ? date.Date : null,
                Status = status,
            };
        }

        private static DateTime GetPreviousDay(DayOfWeek dayOfWeek)
        {
            var date = DateTime.UtcNow.Date;

            while (date.DayOfWeek != dayOfWeek)
            {
                date = date.AddDays(-1);
            }

            return date;
        }

        private sealed class FakeHabitRepository : IHabitRepository
        {
            private readonly List<Habit> habits;

            public FakeHabitRepository(List<Habit> habits)
            {
                this.habits = habits;
            }

            public Task<Habit?> GetByIdAsync(Guid id)
            {
                return Task.FromResult(this.habits.FirstOrDefault(h => h.Id == id));
            }

            public Task<List<Habit>> GetByUserIdAsync(Guid userId)
            {
                return Task.FromResult(this.habits.Where(h => h.UserId == userId).ToList());
            }

            public Task AddAsync(Habit habit)
            {
                this.habits.Add(habit);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(Habit habit)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(Guid id)
            {
                this.habits.RemoveAll(h => h.Id == id);
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

            public Task<HabitLog?> GetByDateAsync(Guid habitId, DateTime date)
            {
                return Task.FromResult(this.logs.FirstOrDefault(l =>
                    l.HabitId == habitId &&
                    l.ScheduledDate.Date == date.Date));
            }

            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId)
            {
                return Task.FromResult(this.logs.Where(l => l.HabitId == habitId).ToList());
            }

            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId, Guid userId)
            {
                return Task.FromResult(this.logs
                    .Where(l => l.HabitId == habitId && l.UserId == userId)
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

        private sealed class FakeUserRepository : IUserRepository
        {
            private readonly User user;

            public FakeUserRepository(User user)
            {
                this.user = user;
            }

            public Task<User?> GetByIdAsync(Guid id)
            {
                return Task.FromResult(this.user.Id == id ? this.user : null);
            }

            public Task<User?> GetByEmailAsync(string email)
            {
                return Task.FromResult(this.user.Email == email ? this.user : null);
            }

            public Task AddAsync(User user)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(User user)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(User user)
            {
                return Task.CompletedTask;
            }
        }
    }
}