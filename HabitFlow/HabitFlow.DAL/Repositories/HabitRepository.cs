using HabitFlow.DAL.Context;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Interfaces;
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
            => await context.Habits.FindAsync(id);

        public async Task<List<Habit>> GetByUserIdAsync(Guid userId)
            => await context.Habits
                .Where(h => h.UserId == userId)
                .ToListAsync();

        public async Task AddAsync(Habit habit)
        {
            await context.Habits.AddAsync(habit);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Habit habit)
        {
            context.Habits.Update(habit);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await context.Habits.FindAsync(id);
            if (entity != null)
            {
                context.Habits.Remove(entity);
                await context.SaveChangesAsync();
            }
        }
    }
}