using HabitFlow.Domain.Entities;

namespace HabitFlow.Domain.Interfaces
{
    public interface ITriggerLogRepository
    {
        Task AddAsync(TriggerLog triggerLog);

        Task UpdateAsync(TriggerLog triggerLog);

        Task DeleteAsync(Guid id);

        Task<TriggerLog?> GetByIdAsync(Guid id);

        Task<List<TriggerLog>> GetByUserIdAsync(Guid userId);

        Task<List<TriggerLog>> GetByUserIdForPeriodAsync(
            Guid userId,
            DateTime from,
            DateTime to);
    }
}