using HabitFlow.Domain.Entities;


namespace HabitFlow.Domain.Interfaces
{
    public interface IHabitRepository
    {
        Task<Habit?> GetByIdAsync(Guid id);

        Task<List<Habit>> GetByUserIdAsync(Guid userId);

        Task AddAsync(Habit habit);

        Task UpdateAsync(Habit habit);

        Task DeleteAsync(Guid id);
    }
}