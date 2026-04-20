using HabitFlow.Domain.Entities;

namespace HabitFlow.Domain.Interfaces
{
    public interface IHabitLogRepository
    {
        Task<HabitLog?> GetByDateAsync(Guid habitId, DateTime date);

        Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId);

        Task<int> GetCompletedCountByUserIdAsync(Guid userId);

        Task AddAsync(HabitLog log);

        Task UpdateAsync(HabitLog log);
    }
}