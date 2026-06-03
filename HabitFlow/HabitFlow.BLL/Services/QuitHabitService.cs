using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;

namespace HabitFlow.BLL.Services
{
    public class QuitHabitService : IQuitHabitService
    {
        private readonly IHabitRepository habitRepository;
        private readonly IHabitLogRepository habitLogRepository;
        private readonly ITriggerLogRepository triggerLogRepository;

        public QuitHabitService(
            IHabitRepository habitRepository,
            IHabitLogRepository habitLogRepository,
            ITriggerLogRepository triggerLogRepository)
        {
            this.habitRepository = habitRepository;
            this.habitLogRepository = habitLogRepository;
            this.triggerLogRepository = triggerLogRepository;
        }

        public async Task LogCleanDayAsync(Guid habitId, Guid userId)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);

            if (habit == null || habit.UserId != userId || habit.Mode != HabitMode.Quit)
            {
                throw new InvalidOperationException("Quit habit was not found.");
            }

            var today = DateTime.UtcNow.Date;
            var existingLog = await this.habitLogRepository.GetByDateAsync(habitId, today);

            if (existingLog != null)
            {
                existingLog.Status = LogStatus.Completed;
                existingLog.CompletedAt = DateTime.UtcNow;
                existingLog.Note = "Clean day";

                await this.habitLogRepository.UpdateAsync(existingLog);
                return;
            }

            var log = new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                ScheduledDate = today,
                CompletedAt = DateTime.UtcNow,
                Status = LogStatus.Completed,
                Note = "Clean day",
            };

            await this.habitLogRepository.AddAsync(log);
        }

        public async Task LogCravingAsync(Guid habitId, Guid userId, LogCravingDto dto)
        {
            await this.ValidateQuitHabitAsync(habitId, userId);

            var triggerLog = new TriggerLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                OccurredAt = DateTime.UtcNow,
                TimeOfDay = dto.TimeOfDay,
                Location = dto.Location,
                EmotionalState = dto.EmotionalState,
                CravingLevel = dto.CravingLevel,
                TriggerType = dto.TriggerType,
                DidRelapse = false,
                Resisted = true,
                Note = dto.Note,
            };

            await this.triggerLogRepository.AddAsync(triggerLog);
        }

        public async Task LogRelapseAsync(Guid habitId, Guid userId, LogRelapseDto dto)
        {
            await this.ValidateQuitHabitAsync(habitId, userId);

            var triggerLog = new TriggerLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                OccurredAt = DateTime.UtcNow,
                TimeOfDay = dto.TimeOfDay,
                Location = dto.Location,
                EmotionalState = dto.EmotionalState,
                CravingLevel = dto.CravingLevel,
                TriggerType = dto.TriggerType,
                DidRelapse = true,
                Resisted = false,
                Note = dto.Note,
            };

            await this.triggerLogRepository.AddAsync(triggerLog);

            var today = DateTime.UtcNow.Date;
            var existingLog = await this.habitLogRepository.GetByDateAsync(habitId, today);

            if (existingLog != null)
            {
                existingLog.Status = LogStatus.Failed;
                existingLog.CompletedAt = DateTime.UtcNow;
                existingLog.Note = "Relapse";

                await this.habitLogRepository.UpdateAsync(existingLog);
                return;
            }

            var log = new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                ScheduledDate = today,
                CompletedAt = DateTime.UtcNow,
                Status = LogStatus.Failed,
                Note = "Relapse",
            };

            await this.habitLogRepository.AddAsync(log);
        }

        public async Task<QuitProgressDto> GetQuitProgressAsync(Guid habitId, Guid userId)
        {
            await this.ValidateQuitHabitAsync(habitId, userId);

            var triggerLogs = await this.triggerLogRepository.GetByHabitAndUserAsync(habitId, userId);
            var habitLogs = await this.habitLogRepository.GetByHabitIdAsync(habitId, userId);

            var relapsesCount = triggerLogs.Count(t => t.DidRelapse);
            var defeatedCravings = triggerLogs.Count(t => t.Resisted && !t.DidRelapse);

            var averageCraving = triggerLogs.Count == 0
                ? 0
                : triggerLogs.Average(t => t.CravingLevel);

            var cleanDays = this.CalculateCleanDays(habitLogs);

            return new QuitProgressDto
            {
                HabitId = habitId,
                RelapsesCount = relapsesCount,
                DefeatedCravings = defeatedCravings,
                AverageCravingLevel = Math.Round(averageCraving, 1),
                CleanDays = cleanDays,
            };
        }

        private async Task ValidateQuitHabitAsync(Guid habitId, Guid userId)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);

            if (habit == null || habit.UserId != userId || habit.Mode != HabitMode.Quit)
            {
                throw new InvalidOperationException("Quit habit was not found.");
            }
        }

        private int CalculateCleanDays(List<HabitLog> habitLogs)
        {
            var orderedLogs = habitLogs
                .OrderByDescending(l => l.ScheduledDate.Date)
                .ToList();

            var cleanDays = 0;

            foreach (var log in orderedLogs)
            {
                if (log.Status == LogStatus.Failed)
                {
                    break;
                }

                if (log.Status == LogStatus.Completed)
                {
                    cleanDays++;
                }
            }

            return cleanDays;
        }

    }
}