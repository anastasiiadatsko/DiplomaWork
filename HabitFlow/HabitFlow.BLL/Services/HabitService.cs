using System.Text.Json;
using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace HabitFlow.BLL.Services
{
    public class HabitService : IHabitService
    {
        private readonly IHabitRepository habitRepository;
        private readonly IHabitLogRepository habitLogRepository;
        private readonly ILogger<HabitService> logger;

        public HabitService(
            IHabitRepository habitRepository,
            IHabitLogRepository habitLogRepository,
            ILogger<HabitService> logger)
        {
            this.habitRepository = habitRepository;
            this.habitLogRepository = habitLogRepository;
            this.logger = logger;
        }

        private static DateTime UtcDate(DateTime dt)
            => DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc);

        public async Task<DashboardViewModel> GetDashboardAsync(Guid userId, string userName)
        {
            var habits = await this.habitRepository.GetByUserIdAsync(userId);
            var today = DateTime.Today;

            var habitDtos = new List<HabitDto>();
            var todayHabits = new List<HabitDto>();

            foreach (var habit in habits)
            {
                var logs = await this.habitLogRepository.GetByHabitIdAsync(habit.Id);
                var dto = this.MapToDto(habit, logs, today);
                habitDtos.Add(dto);

                if (this.IsScheduledToday(habit, today))
                    todayHabits.Add(dto);
            }

            var totalCompleted = await this.habitLogRepository
                .GetCompletedCountByUserIdAsync(userId);

            var allLogs = new List<(DateTime Date, bool Completed)>();
            foreach (var habit in habits)
            {
                var logs = await this.habitLogRepository.GetByHabitIdAsync(habit.Id);
                foreach (var log in logs)
                    allLogs.Add((log.ScheduledDate.Date, log.Status == LogStatus.Completed));
            }

            return new DashboardViewModel
            {
                UserName = userName,
                TodayHabits = todayHabits,
                TotalHabits = habits.Count,
                CompletedToday = todayHabits.Count(h => h.IsCompletedToday),
                TotalCompleted = totalCompleted,
                LongestStreak = habitDtos.Any() ? habitDtos.Max(h => h.CurrentStreak) : 0,
                OverallConsistencyRate = habitDtos.Any()
                    ? Math.Round(habitDtos.Average(h => h.ConsistencyRate), 1)
                    : 0,
                HeatmapData = this.BuildHeatmap(allLogs),
            };
        }

        public async Task<List<HabitDto>> GetAllHabitsAsync(Guid userId)
        {
            var habits = await this.habitRepository.GetByUserIdAsync(userId);
            var today = DateTime.Today;
            var result = new List<HabitDto>();

            foreach (var habit in habits)
            {
                var logs = await this.habitLogRepository.GetByHabitIdAsync(habit.Id);
                result.Add(this.MapToDto(habit, logs, today));
            }

            return result.OrderByDescending(h => h.IsActive)
                         .ThenBy(h => h.Name)
                         .ToList();
        }

        public async Task<HabitDto?> GetByIdAsync(Guid habitId, Guid userId)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null || habit.UserId != userId)
                return null;

            var logs = await this.habitLogRepository.GetByHabitIdAsync(habitId);
            return this.MapToDto(habit, logs, DateTime.Today);
        }

        public async Task CreateHabitAsync(Guid userId, CreateHabitDto dto)
        {
            var habit = new Habit
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = dto.Name,
                Description = dto.Description,
                Category = dto.Category,
                FrequencyType = dto.FrequencyType,
                TargetDaysJson = JsonSerializer.Serialize(dto.TargetDays),
                Color = dto.Color,
                StartDate = UtcDate(DateTime.Today),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };

            await this.habitRepository.AddAsync(habit);
            this.logger.LogInformation("Звичка створена: {Name} для {UserId}", dto.Name, userId);
        }

        public async Task UpdateHabitAsync(Guid habitId, Guid userId, CreateHabitDto dto)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null || habit.UserId != userId)
                return;

            habit.Name = dto.Name;
            habit.Description = dto.Description;
            habit.Category = dto.Category;
            habit.FrequencyType = dto.FrequencyType;
            habit.TargetDaysJson = JsonSerializer.Serialize(dto.TargetDays);
            habit.Color = dto.Color;

            await this.habitRepository.UpdateAsync(habit);
            this.logger.LogInformation("Звичка оновлена: {HabitId}", habitId);
        }

        public async Task DeleteHabitAsync(Guid habitId, Guid userId)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null || habit.UserId != userId)
                return;

            await this.habitRepository.DeleteAsync(habitId);
            this.logger.LogInformation("Звичка видалена: {HabitId}", habitId);
        }

        public async Task<bool> ToggleCompletionAsync(Guid habitId, Guid userId)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null || habit.UserId != userId)
                return false;

            var today = UtcDate(DateTime.Today);
            var existingLog = await this.habitLogRepository.GetByDateAsync(habitId, today);

            if (existingLog != null)
            {
                if (existingLog.Status == LogStatus.Completed)
                {
                    existingLog.Status = LogStatus.Pending;
                    existingLog.CompletedAt = null;
                    await this.habitLogRepository.UpdateAsync(existingLog);
                    return false;
                }
                else
                {
                    existingLog.Status = LogStatus.Completed;
                    existingLog.CompletedAt = DateTime.UtcNow;
                    await this.habitLogRepository.UpdateAsync(existingLog);
                    return true;
                }
            }

            var newLog = new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                ScheduledDate = today,
                CompletedAt = DateTime.UtcNow,
                Status = LogStatus.Completed,
            };

            await this.habitLogRepository.AddAsync(newLog);
            this.logger.LogInformation("Звичка відмічена: {HabitId}", habitId);
            return true;
        }

        public async Task PauseHabitAsync(Guid habitId, Guid userId)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null || habit.UserId != userId)
                return;

            habit.IsActive = !habit.IsActive;
            await this.habitRepository.UpdateAsync(habit);
        }

        public async Task ManualLogAsync(Guid userId, ManualLogDto dto)
        {
            var habit = await this.habitRepository.GetByIdAsync(dto.HabitId);
            if (habit == null || habit.UserId != userId)
                return;

            var date = UtcDate(dto.Date);

            var existing = await this.habitLogRepository.GetByDateAsync(dto.HabitId, date);

            if (existing != null)
            {
                existing.Status = LogStatus.Completed;
                existing.CompletedAt = date;
                existing.Note = dto.Note;
                await this.habitLogRepository.UpdateAsync(existing);
                return;
            }

            await this.habitLogRepository.AddAsync(new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = dto.HabitId,
                UserId = userId,
                ScheduledDate = date,
                CompletedAt = date,
                Status = LogStatus.Completed,
                Note = dto.Note,
            });

            this.logger.LogInformation(
                "Ручний лог: {HabitId} за {Date}", dto.HabitId, date);
        }

        public async Task ManualLogRangeAsync(
            Guid userId, Guid habitId, DateTime from, DateTime to)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null || habit.UserId != userId)
                return;

            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                var date = UtcDate(d);
                var existing = await this.habitLogRepository.GetByDateAsync(habitId, date);

                if (existing != null)
                {
                    existing.Status = LogStatus.Completed;
                    existing.CompletedAt = date;
                    await this.habitLogRepository.UpdateAsync(existing);
                    continue;
                }

                await this.habitLogRepository.AddAsync(new HabitLog
                {
                    Id = Guid.NewGuid(),
                    HabitId = habitId,
                    UserId = userId,
                    ScheduledDate = date,
                    CompletedAt = date,
                    Status = LogStatus.Completed,
                });
            }

            this.logger.LogInformation(
                "Діапазон логів: {HabitId} з {From} по {To}", habitId, from, to);
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────

        private HabitDto MapToDto(Habit habit, List<HabitLog> logs, DateTime today)
        {
            var completedLogs = logs
                .Where(l => l.Status == LogStatus.Completed)
                .OrderBy(l => l.ScheduledDate)
                .ToList();

            var isCompletedToday = logs.Any(l =>
                l.ScheduledDate.Date == today.Date &&
                l.Status == LogStatus.Completed);

            var targetDays = new List<DayOfWeek>();
            try
            {
                targetDays = JsonSerializer.Deserialize<List<DayOfWeek>>(
                    habit.TargetDaysJson) ?? new();
            }
            catch { }

            return new HabitDto
            {
                Id = habit.Id,
                Name = habit.Name,
                Description = habit.Description,
                Category = habit.Category,
                FrequencyType = habit.FrequencyType,
                TargetDays = targetDays,
                Color = habit.Color,
                IsCompletedToday = isCompletedToday,
                CurrentStreak = this.CalculateStreak(completedLogs),
                ConsistencyRate = this.CalculateConsistencyRate(habit, completedLogs),
                StartDate = habit.StartDate,
                IsActive = habit.IsActive,
            };
        }

        private int CalculateStreak(List<HabitLog> completedLogs)
        {
            if (!completedLogs.Any()) return 0;

            var today = DateTime.Today;

            var dates = completedLogs
                .Select(l => l.ScheduledDate.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            bool doneToday = dates.Any(d => d == today);
            bool doneYesterday = dates.Any(d => d == today.AddDays(-1));

            DateTime startDate;
            if (doneToday)
                startDate = today;
            else if (doneYesterday)
                startDate = today.AddDays(-1);
            else
                return 0;

            int streak = 0;
            DateTime current = startDate;

            foreach (var date in dates)
            {
                if (date == current)
                {
                    streak++;
                    current = current.AddDays(-1);
                }
                else if (date < current)
                {
                    break;
                }
            }

            return streak;
        }

        private double CalculateConsistencyRate(Habit habit, List<HabitLog> completedLogs)
        {
            var daysSinceStart = (DateTime.Today - habit.StartDate.Date).Days + 1;
            if (daysSinceStart <= 0) return 0;

            var completedUniqueDays = completedLogs
                .Select(l => l.ScheduledDate.Date)
                .Distinct()
                .Count();

            return Math.Round(
                Math.Min((double)completedUniqueDays / daysSinceStart * 100, 100), 1);
        }

        private bool IsScheduledToday(Habit habit, DateTime today)
        {
            return habit.FrequencyType switch
            {
                FrequencyType.Daily => true,
                FrequencyType.SpecificDays => this.GetTargetDays(habit).Contains(today.DayOfWeek),
                FrequencyType.Weekly => today.DayOfWeek == DayOfWeek.Monday,
                _ => true,
            };
        }

        private List<DayOfWeek> GetTargetDays(Habit habit)
        {
            try
            {
                return JsonSerializer.Deserialize<List<DayOfWeek>>(habit.TargetDaysJson) ?? new();
            }
            catch
            {
                return new();
            }
        }

        private List<HeatmapDay> BuildHeatmap(List<(DateTime Date, bool Completed)> allLogs)
        {
            var result = new List<HeatmapDay>();
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-364);

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dayLogs = allLogs.Where(l => l.Date == date.Date).ToList();
                var completedCount = dayLogs.Count(l => l.Completed);

                result.Add(new HeatmapDay
                {
                    Date = date,
                    CompletedCount = completedCount,
                    Level = completedCount switch
                    {
                        0 => 0,
                        1 => 1,
                        2 => 2,
                        3 => 3,
                        _ => 4,
                    },
                });
            }

            return result;
        }
    }
}