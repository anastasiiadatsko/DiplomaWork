using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface IRecommendationService
    {
        Task<List<RecommendationViewModel>> GetRecommendationsAsync(Guid userId);
    }
}