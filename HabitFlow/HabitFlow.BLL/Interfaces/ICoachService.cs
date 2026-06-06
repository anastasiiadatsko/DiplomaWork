using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface ICoachService
    {
        Task<CoachSessionResponse> GetSessionQuestionsAsync(
            Guid habitId, Guid userId, CoachSessionType sessionType);

        Task<CoachAdviceResponse> GetAdviceAsync(
            Guid userId, CoachAdviceRequest request);
        Task<CoachSessionType> DetectSessionTypeAsync(
            Guid habitId, Guid userId);

        Task<CoachSummaryResponse> GetSessionSummaryAsync(
            Guid userId, CoachSummaryRequest request);
        Task<AnalyticsViewModel> GetHabitAnalyticsForVoiceAsync(Guid habitId, Guid userId);
    }
}