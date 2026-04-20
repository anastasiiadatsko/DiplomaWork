using HabitFlow.DAL.Context;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HabitFlow.DAL.Repositories
{
    public class HabitLogRepository : IHabitLogRepository
    {
        private readonly AppDbContext context;

        public HabitLogRepository(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<HabitLog?> GetByDateAsync(Guid habitId, DateTime date)
        {
            return await context.HabitLogs
                .FirstOrDefaultAsync(l =>
                    l.HabitId == habitId &&
                    l.ScheduledDate.Date == date.Date);
        }

        public async Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId)
        {
            return await context.HabitLogs
                .Where(l => l.HabitId == habitId)
                .ToListAsync();
        }

        public async Task<int> GetCompletedCountByUserIdAsync(Guid userId)
        {
            return await context.HabitLogs
                .CountAsync(l =>
                    l.UserId == userId &&
                    l.Status == HabitFlow.Domain.Enums.LogStatus.Completed);
        }

        public async Task AddAsync(HabitLog log)
        {
            await context.HabitLogs.AddAsync(log);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(HabitLog log)
        {
            context.HabitLogs.Update(log);
            await context.SaveChangesAsync();
        }
    }
}