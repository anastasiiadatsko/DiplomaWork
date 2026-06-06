using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface IQuitAnalyticsService
    {
        Task<QuitAnalyticsViewModel> GetAnalyticsAsync(Guid userId);
    }
}