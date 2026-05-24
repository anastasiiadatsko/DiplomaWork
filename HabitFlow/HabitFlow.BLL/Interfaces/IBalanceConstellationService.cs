using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface IBalanceConstellationService
    {
        Task<BalanceConstellationViewModel> GetConstellationAsync(Guid userId);
    }
}