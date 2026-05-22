using HabitFlow.DAL.Context;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HabitFlow.DAL.Repositories
{
    public class SharedHabitRepository : ISharedHabitRepository
    {
        private readonly AppDbContext context;

        public SharedHabitRepository(AppDbContext context)
        {
            this.context = context;
        }

        public async Task AddParticipantAsync(HabitParticipant participant)
        {
            await this.context.HabitParticipants.AddAsync(participant);
            await this.context.SaveChangesAsync();
        }

        public async Task<HabitParticipant?> GetParticipantAsync(Guid habitId, Guid userId)
        {
            return await this.context.HabitParticipants
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.HabitId == habitId && p.UserId == userId);
        }

        public async Task<List<Habit>> GetSharedHabitsByUserIdAsync(Guid userId)
        {
            return await this.context.HabitParticipants
                .Include(p => p.Habit)
                .Where(p => p.UserId == userId)
                .Select(p => p.Habit)
                .ToListAsync();
        }

        public async Task<List<HabitParticipant>> GetParticipantsByHabitIdAsync(Guid habitId)
        {
            return await this.context.HabitParticipants
                .Include(p => p.User)
                .Where(p => p.HabitId == habitId)
                .ToListAsync();
        }

        public async Task AddInvitationAsync(HabitInvitation invitation)
        {
            await this.context.HabitInvitations.AddAsync(invitation);
            await this.context.SaveChangesAsync();
        }

        public async Task<HabitInvitation?> GetInvitationByTokenAsync(string token)
        {
            return await this.context.HabitInvitations
                .Include(i => i.Habit)
                .Include(i => i.InviterUser)
                .Include(i => i.InviteeUser)
                .FirstOrDefaultAsync(i => i.Token == token);
        }

        public async Task<HabitInvitation?> GetPendingInvitationAsync(
            Guid habitId,
            Guid inviteeUserId)
        {
            return await this.context.HabitInvitations
                .FirstOrDefaultAsync(i =>
                    i.HabitId == habitId &&
                    i.InviteeUserId == inviteeUserId &&
                    i.Status == InvitationStatus.Pending);
        }

        public async Task UpdateInvitationAsync(HabitInvitation invitation)
        {
            this.context.HabitInvitations.Update(invitation);
            await this.context.SaveChangesAsync();
        }
    }
}