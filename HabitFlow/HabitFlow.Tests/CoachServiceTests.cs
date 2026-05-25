using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.BLL.Services;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HabitFlow.Tests
{
    public class CoachServiceTests
    {
        [Fact]
        public async Task DetectSessionTypeAsync_ReturnsFreeChat_WhenHabitDoesNotExist()
        {
            var service = CreateService(null, new List<HabitLog>());

            var result = await service.DetectSessionTypeAsync(Guid.NewGuid(), Guid.NewGuid());

            Assert.Equal(CoachSessionType.FreeChat, result);
        }

        [Fact]
        public async Task DetectSessionTypeAsync_ReturnsOnboarding_WhenHabitIsNew()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = CreateHabit(habitId, userId, DateTime.Today.AddDays(-1));
            var service = CreateService(habit, new List<HabitLog>());

            var result = await service.DetectSessionTypeAsync(habitId, userId);

            Assert.Equal(CoachSessionType.Onboarding, result);
        }

        [Fact]
        public async Task DetectSessionTypeAsync_ReturnsOnboarding_WhenThereAreNoCompletedLogs()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = CreateHabit(habitId, userId, DateTime.Today.AddDays(-10));
            var service = CreateService(habit, new List<HabitLog>());

            var result = await service.DetectSessionTypeAsync(habitId, userId);

            Assert.Equal(CoachSessionType.Onboarding, result);
        }

        [Fact]
        public async Task DetectSessionTypeAsync_ReturnsMilestoneReached_WhenCompletedCountIsSeven()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var startDate = DateTime.Today.AddDays(-10);

            var habit = CreateHabit(habitId, userId, startDate);
            var logs = Enumerable.Range(0, 7)
                .Select(i => CreateCompletedLog(habitId, userId, startDate.AddDays(i)))
                .ToList();

            var service = CreateService(habit, logs);

            var result = await service.DetectSessionTypeAsync(habitId, userId);

            Assert.Equal(CoachSessionType.MilestoneReached, result);
        }

        [Fact]
        public async Task DetectSessionTypeAsync_ReturnsAfterStreakBreak_WhenLastTwoDaysAreMissed()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;
            var startDate = today.AddDays(-10);

            var habit = CreateHabit(habitId, userId, startDate);

            var logs = new List<HabitLog>
            {
                CreateCompletedLog(habitId, userId, today.AddDays(-6)),
                CreateCompletedLog(habitId, userId, today.AddDays(-5)),
                CreateCompletedLog(habitId, userId, today.AddDays(-4)),
                CreateCompletedLog(habitId, userId, today.AddDays(-3)),
            };

            var service = CreateService(habit, logs);

            var result = await service.DetectSessionTypeAsync(habitId, userId);

            Assert.Equal(CoachSessionType.AfterStreakBreak, result);
        }

        [Fact]
        public async Task GetSessionQuestionsAsync_ReturnsContextFromAnalytics()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var analytics = CreateAnalyticsViewModel(habitId);
            var service = CreateService(
                CreateHabit(habitId, userId, DateTime.Today.AddDays(-20)),
                new List<HabitLog>(),
                analytics);

            var result = await service.GetSessionQuestionsAsync(
                habitId,
                userId,
                CoachSessionType.WeeklyCheckIn);

            Assert.Equal(CoachSessionType.WeeklyCheckIn, result.SessionType);
            Assert.NotEmpty(result.SessionTitle);
            Assert.NotEmpty(result.Questions);
            Assert.Equal("Читання", result.Context.HabitName);
            Assert.Equal(12, result.Context.CurrentStreak);
            Assert.Equal(87.5, result.Context.ConsistencyRate);
            Assert.Equal("Понеділок", result.Context.MostRiskyDay);
        }

        [Fact]
        public async Task GetAdviceAsync_ReturnsFallbackAdvice_WhenGeminiApiKeyIsMissing()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var analytics = CreateAnalyticsViewModel(habitId);
            var service = CreateService(
                CreateHabit(habitId, userId, DateTime.Today.AddDays(-20)),
                new List<HabitLog>(),
                analytics);

            var result = await service.GetAdviceAsync(userId, new CoachAdviceRequest
            {
                HabitId = habitId,
                SessionType = CoachSessionType.FreeChat,
                UserMessage = "Як не пропускати звичку?",
            });

            Assert.False(string.IsNullOrWhiteSpace(result.Advice));
            Assert.Equal(3, result.ActionItems.Count);
            Assert.False(string.IsNullOrWhiteSpace(result.Motivation));
            Assert.Contains("87.5", result.Advice);
        }

        [Fact]
        public async Task GetAdviceAsync_ReturnsFallbackForMilestoneSession()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var analytics = CreateAnalyticsViewModel(habitId);
            analytics.TotalCompleted = 21;

            var service = CreateService(
                CreateHabit(habitId, userId, DateTime.Today.AddDays(-25)),
                new List<HabitLog>(),
                analytics);

            var result = await service.GetAdviceAsync(userId, new CoachAdviceRequest
            {
                HabitId = habitId,
                SessionType = CoachSessionType.MilestoneReached,
            });

            Assert.Contains("21", result.Advice);
            Assert.Equal(3, result.ActionItems.Count);
            Assert.False(string.IsNullOrWhiteSpace(result.Motivation));
        }

        [Fact]
        public async Task GetSessionSummaryAsync_ReturnsFallbackSummary_WhenGeminiApiKeyIsMissing()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var analytics = CreateAnalyticsViewModel(habitId);
            var service = CreateService(
                CreateHabit(habitId, userId, DateTime.Today.AddDays(-20)),
                new List<HabitLog>(),
                analytics);

            var result = await service.GetSessionSummaryAsync(userId, new CoachSummaryRequest
            {
                HabitId = habitId,
                ActionItems = new List<string>
                {
                    "Поставити нагадування",
                    "Зробити мінімальний варіант",
                },
            });

            Assert.Contains("Читання", result.Overview);
            Assert.NotEmpty(result.KeyInsights);
            Assert.Equal(2, result.ActionPlan.Count);
            Assert.Contains("Поставити нагадування", result.ActionPlan);
            Assert.False(string.IsNullOrWhiteSpace(result.ClosingNote));
        }

        [Fact]
        public async Task GetSessionSummaryAsync_UsesDefaultActionPlan_WhenRequestHasNoActions()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var analytics = CreateAnalyticsViewModel(habitId);
            var service = CreateService(
                CreateHabit(habitId, userId, DateTime.Today.AddDays(-20)),
                new List<HabitLog>(),
                analytics);

            var result = await service.GetSessionSummaryAsync(userId, new CoachSummaryRequest
            {
                HabitId = habitId,
            });

            Assert.Contains("Читання", result.Overview);
            Assert.Equal(3, result.ActionPlan.Count);
            Assert.Equal(3, result.KeyInsights.Count);
            Assert.False(string.IsNullOrWhiteSpace(result.SessionDate));
        }

        private static CoachService CreateService(
            Habit? habit,
            List<HabitLog> logs,
            AnalyticsViewModel? analytics = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Gemini:ApiKey"] = string.Empty,
                    ["Gemini:Model"] = "gemini-test",
                })
                .Build();

            return new CoachService(
                new FakeHabitRepository(habit),
                new FakeHabitLogRepository(logs),
                new FakeAnalyticsService(analytics ?? new AnalyticsViewModel()),
                new HttpClient(),
                configuration,
                NullLogger<CoachService>.Instance);
        }

        private static Habit CreateHabit(Guid habitId, Guid userId, DateTime startDate)
        {
            return new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Читання",
                StartDate = startDate.Date,
                FrequencyType = FrequencyType.Daily,
                IsActive = true,
            };
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

        private static AnalyticsViewModel CreateAnalyticsViewModel(Guid habitId)
        {
            return new AnalyticsViewModel
            {
                HabitName = "Читання",
                DaysSinceStart = 20,
                CurrentStreak = 12,
                MaxStreak = 15,
                ConsistencyRate = 87.5,
                TotalCompleted = 18,
                BreakRisk = 14.2,
                IsStreakActive = true,
                MostRiskyDay = "Понеділок",
                OptimalDayToAct = "Середа",
                MarkovP00 = 88.1,
                MarkovP10 = 45.3,
                HabitStrengthScore = 76.4,
                WeekdayStats = new List<WeekdayStats>
                {
                    new() { Day = "Пн", Total = 3, Completed = 1, Rate = 33.3 },
                    new() { Day = "Вт", Total = 3, Completed = 3, Rate = 100 },
                    new() { Day = "Ср", Total = 3, Completed = 3, Rate = 100 },
                },
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
                var result = this.habit != null && this.habit.UserId == userId
                    ? new List<Habit> { this.habit }
                    : new List<Habit>();

                return Task.FromResult(result);
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

        private sealed class FakeAnalyticsService : IAnalyticsService
        {
            private readonly AnalyticsViewModel analytics;

            public FakeAnalyticsService(AnalyticsViewModel analytics)
            {
                this.analytics = analytics;
            }

            public Task<AnalyticsViewModel> GetHabitAnalyticsAsync(
                Guid habitId,
                Guid userId)
            {
                return Task.FromResult(this.analytics);
            }
        }
    }
}