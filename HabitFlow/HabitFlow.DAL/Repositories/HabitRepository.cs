using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Interfaces;
using HabitFlow.DAL.Context;
using Microsoft.EntityFrameworkCore;

namespace HabitFlow.DAL.Repositories
{
    public class HabitRepository : IHabitRepository
    {
        private readonly AppDbContext context;

        public HabitRepository(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<Habit?> GetByIdAsync(Guid id)
        {
            return await this.context.Habits
                .Include(h => h.Logs)
                .Include(h => h.Analytics)
                .FirstOrDefaultAsync(h => h.Id == id);
        }

        public async Task<List<Habit>> GetByUserIdAsync(Guid userId)
        {
            return await this.context.Habits
                .Where(h => h.UserId == userId && h.IsActive)
                .Include(h => h.Analytics)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(Habit habit)
        {
            await this.context.Habits.AddAsync(habit);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Habit habit)
        {
            this.context.Habits.Update(habit);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var habit = await this.context.Habits.FindAsync(id);
            if (habit != null)
            {
                habit.IsActive = false;
                await this.context.SaveChangesAsync();
            }
        }
    }
}