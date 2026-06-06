using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface IAnalyticsService
    {
        Task<AnalyticsViewModel> GetHabitAnalyticsAsync(Guid habitId, Guid userId);
    }
}