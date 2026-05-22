using HabitFlow.Domain.Entities;

namespace HabitFlow.Domain.Interfaces
{
    public interface ISharedHabitRepository
    {
        Task AddParticipantAsync(HabitParticipant participant);

        Task<HabitParticipant?> GetParticipantAsync(Guid habitId, Guid userId);

        Task<List<Habit>> GetSharedHabitsByUserIdAsync(Guid userId);

        Task<List<HabitParticipant>> GetParticipantsByHabitIdAsync(Guid habitId);

        Task AddInvitationAsync(HabitInvitation invitation);

        Task<HabitInvitation?> GetInvitationByTokenAsync(string token);

        Task<HabitInvitation?> GetPendingInvitationAsync(Guid habitId, Guid inviteeUserId);

        Task UpdateInvitationAsync(HabitInvitation invitation);
    }
}