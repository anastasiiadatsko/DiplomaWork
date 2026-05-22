using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Interfaces;
using HabitFlow.DAL.Context;
using Microsoft.EntityFrameworkCore;

namespace HabitFlow.DAL.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext context;

        public UserRepository(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await this.context.Users.FindAsync(id);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await this.context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task AddAsync(User user)
        {
            await this.context.Users.AddAsync(user);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            this.context.Users.Update(user);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(User user)
        {
            this.context.Users.Remove(user);
            await this.context.SaveChangesAsync();
        }
    }
}