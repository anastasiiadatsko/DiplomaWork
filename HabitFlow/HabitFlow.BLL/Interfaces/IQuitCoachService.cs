using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface IQuitCoachService
    {
        Task<QuitCoachResponse> GetResponseAsync(Guid userId, QuitCoachRequest request);
    }
}