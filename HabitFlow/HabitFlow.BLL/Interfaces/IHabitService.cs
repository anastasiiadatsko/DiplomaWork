using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface IHabitService
    {
        Task<DashboardViewModel> GetDashboardAsync(Guid userId, string userName);

        Task<List<HabitDto>> GetAllHabitsAsync(Guid userId);

        Task<HabitDto?> GetByIdAsync(Guid habitId, Guid userId);

        Task CreateHabitAsync(Guid userId, CreateHabitDto dto);

        Task UpdateHabitAsync(Guid habitId, Guid userId, CreateHabitDto dto);

        Task DeleteHabitAsync(Guid habitId, Guid userId);

        Task<bool> ToggleCompletionAsync(Guid habitId, Guid userId);

        Task PauseHabitAsync(Guid habitId, Guid userId);
        Task ManualLogAsync(Guid userId, ManualLogDto dto);
        Task ManualLogRangeAsync(Guid userId, Guid habitId, DateTime from, DateTime to);
    }
}