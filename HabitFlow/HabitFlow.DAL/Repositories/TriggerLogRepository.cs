using HabitFlow.DAL.Context;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HabitFlow.DAL.Repositories
{
    public class TriggerLogRepository : ITriggerLogRepository
    {
        private readonly AppDbContext context;

        public TriggerLogRepository(AppDbContext context)
        {
            this.context = context;
        }

        public async Task AddAsync(TriggerLog triggerLog)
        {
            await this.context.TriggerLogs.AddAsync(triggerLog);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(TriggerLog triggerLog)
        {
            this.context.TriggerLogs.Update(triggerLog);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var triggerLog = await this.context.TriggerLogs.FindAsync(id);

            if (triggerLog == null)
            {
                return;
            }

            this.context.TriggerLogs.Remove(triggerLog);
            await this.context.SaveChangesAsync();
        }

        public async Task<TriggerLog?> GetByIdAsync(Guid id)
        {
            return await this.context.TriggerLogs
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<TriggerLog>> GetByUserIdAsync(Guid userId)
        {
            return await this.context.TriggerLogs
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.OccurredAt)
                .ToListAsync();
        }

        public async Task<List<TriggerLog>> GetByUserIdForPeriodAsync(
            Guid userId,
            DateTime from,
            DateTime to)
        {
            return await this.context.TriggerLogs
                .Where(t =>
                    t.UserId == userId &&
                    t.OccurredAt >= from &&
                    t.OccurredAt <= to)
                .OrderByDescending(t => t.OccurredAt)
                .ToListAsync();
        }
    }
}