using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface ICoachService
    {
        /// <summary>Returns typed session questions for the given habit.</summary>
        Task<CoachSessionResponse> GetSessionQuestionsAsync(
            Guid habitId, Guid userId, CoachSessionType sessionType);

        /// <summary>Returns AI advice based on questionnaire answers / chat messages.</summary>
        Task<CoachAdviceResponse> GetAdviceAsync(
            Guid userId, CoachAdviceRequest request);

        /// <summary>Auto-detects which session type to use for the current habit state.</summary>
        Task<CoachSessionType> DetectSessionTypeAsync(
            Guid habitId, Guid userId);

        /// <summary>Generates a structured session summary using AI.</summary>
        Task<CoachSummaryResponse> GetSessionSummaryAsync(
            Guid userId, CoachSummaryRequest request);
    }
}