using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Services;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using static System.Reflection.Metadata.BlobBuilder;
using HabitFlow.BLL.Interfaces;

namespace HabitFlow.Tests
{
    public class HabitServiceTests
    {
        [Fact]
        public async Task ManualLogAsync_CreatesLog_ForValidDate()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;

            var habit = CreateHabit(habitId, userId, today.AddDays(-5));
            var logRepo = new FakeHabitLogRepository();

            var service = CreateService(habit, logRepo);

            await service.ManualLogAsync(userId, new ManualLogDto
            {
                HabitId = habitId,
                Date = today,
                Note = "done",
            });

            Assert.Single(logRepo.Logs);
            Assert.Equal(LogStatus.Completed, logRepo.Logs[0].Status);
            Assert.Equal(today, logRepo.Logs[0].ScheduledDate.Date);
            Assert.Equal("done", logRepo.Logs[0].Note);
        }

        [Fact]
        public async Task ManualLogAsync_DoesNotCreateLog_ForFutureDate()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;

            var habit = CreateHabit(habitId, userId, today.AddDays(-5));
            var logRepo = new FakeHabitLogRepository();

            var service = CreateService(habit, logRepo);

            await service.ManualLogAsync(userId, new ManualLogDto
            {
                HabitId = habitId,
                Date = today.AddDays(10),
            });

            Assert.Empty(logRepo.Logs);
        }

        [Fact]
        public async Task ManualLogAsync_DoesNotCreateLog_BeforeHabitStartDate()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;
            var startDate = today.AddDays(-5);

            var habit = CreateHabit(habitId, userId, startDate);
            var logRepo = new FakeHabitLogRepository();

            var service = CreateService(habit, logRepo);

            await service.ManualLogAsync(userId, new ManualLogDto
            {
                HabitId = habitId,
                Date = startDate.AddDays(-1),
            });

            Assert.Empty(logRepo.Logs);
        }

        [Fact]
        public async Task ManualLogAsync_UpdatesExistingLog_InsteadOfCreatingDuplicate()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;

            var habit = CreateHabit(habitId, userId, today.AddDays(-5));
            var existingLog = CreateLog(habitId, userId, today, LogStatus.Pending);
            var logRepo = new FakeHabitLogRepository(existingLog);

            var service = CreateService(habit, logRepo);

            await service.ManualLogAsync(userId, new ManualLogDto
            {
                HabitId = habitId,
                Date = today,
                Note = "updated",
            });

            Assert.Single(logRepo.Logs);
            Assert.Equal(LogStatus.Completed, logRepo.Logs[0].Status);
            Assert.Equal("updated", logRepo.Logs[0].Note);
            Assert.Equal(1, logRepo.UpdateCalls);
        }

        [Fact]
        public async Task ManualLogRangeAsync_CreatesOnlyDatesFromStartToToday()
        {
            var userId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;
            var startDate = today.AddDays(-2);

            var habit = CreateHabit(habitId, userId, startDate);
            var logRepo = new FakeHabitLogRepository();

            var service = CreateService(habit, logRepo);

            await service.ManualLogRangeAsync(
                userId,
                habitId,
                startDate.AddDays(-5),
                today.AddDays(10));

            Assert.Equal(3, logRepo.Logs.Count);
            Assert.All(logRepo.Logs, log =>
            {
                Assert.True(log.ScheduledDate.Date >= startDate);
                Assert.True(log.ScheduledDate.Date <= today);
            });
        }

        [Fact]
        public async Task ManualLogRangeAsync_DoesNothing_WhenUserHasNoAccess()
        {
            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;

            var habit = CreateHabit(habitId, ownerId, today.AddDays(-5));
            var logRepo = new FakeHabitLogRepository();

            var service = CreateService(habit, logRepo);

            await service.ManualLogRangeAsync(
                otherUserId,
                habitId,
                today.AddDays(-2),
                today);

            Assert.Empty(logRepo.Logs);
        }

        private static HabitService CreateService(
    Habit habit,
    FakeHabitLogRepository logRepository)
        {
            return new HabitService(
                new FakeHabitRepository(habit),
                logRepository,
                new FakeSharedHabitRepository(),
                NullLogger<HabitService>.Instance,
                new FakeGoogleCalendarService());
        }

        private static Habit CreateHabit(Guid habitId, Guid userId, DateTime startDate)
        {
            return new Habit
            {
                Id = habitId,
                UserId = userId,
                Name = "Test habit",
                StartDate = startDate.Date,
                FrequencyType = FrequencyType.Daily,
                IsActive = true,
            };
        }

        private static HabitLog CreateLog(
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
                CompletedAt = status == LogStatus.Completed ? date.Date : null,
                Status = status,
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
            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId, Guid userId)
            {
                return Task.FromResult(
                    this.Logs.Where(l => l.HabitId == habitId && l.UserId == userId).ToList());
            }

            public List<HabitLog> Logs { get; }

            public int UpdateCalls { get; private set; }

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
                this.UpdateCalls++;
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

        private sealed class FakeGoogleCalendarService : IGoogleCalendarService
        {
            public string BuildAuthorizationUrl(Guid userId)
            {
                return "https://test.google.com";
            }

            public Task<bool> HandleCallbackAsync(Guid userId, string code)
            {
                return Task.FromResult(true);
            }

            public Task<bool> DisconnectAsync(Guid userId)
            {
                return Task.FromResult(true);
            }

            public Task<bool> CreateHabitReminderEventAsync(
    Guid userId,
    string habitName,
    DateTime date,
    TimeOnly reminderTime,
    FrequencyType frequencyType,
    List<DayOfWeek> targetDays)
            {
                return Task.FromResult(true);
            }

            public string BuildGoogleCalendarTemplateUrl(
    string habitName,
    string? description,
    DateTime date,
    TimeOnly reminderTime,
    FrequencyType frequencyType,
    List<DayOfWeek> targetDays)
            {
                return "https://calendar.google.com/calendar/render?action=TEMPLATE";
            }
        }
    }
}