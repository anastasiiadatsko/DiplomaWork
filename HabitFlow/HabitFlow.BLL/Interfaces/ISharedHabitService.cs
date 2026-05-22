using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface ISharedHabitService
    {
        Task<(bool Success, string Error)> InviteFriendAsync(
            Guid habitId,
            Guid inviterUserId,
            string friendEmail,
            string acceptBaseUrl,
            string declineBaseUrl);

        Task<(bool Success, string Error)> AcceptInvitationAsync(string token);

        Task<(bool Success, string Error)> DeclineInvitationAsync(string token);

        Task<List<SharedHabitParticipantProgressDto>> GetParticipantsProgressAsync(
            Guid habitId,
            Guid currentUserId);
    }
}