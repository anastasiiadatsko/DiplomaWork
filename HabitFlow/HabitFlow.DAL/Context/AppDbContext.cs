using HabitFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HabitFlow.DAL.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Habit> Habits => Set<Habit>();

        public DbSet<HabitLog> HabitLogs => Set<HabitLog>();

        public DbSet<HabitAnalytics> HabitAnalytics => Set<HabitAnalytics>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(u => u.Id);
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.Email).IsRequired().HasMaxLength(255);
                e.Property(u => u.Name).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<Habit>(e =>
            {
                e.HasKey(h => h.Id);
                e.Property(h => h.Name).IsRequired().HasMaxLength(200);
                e.HasOne(h => h.User)
                 .WithMany(u => u.Habits)
                 .HasForeignKey(h => h.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<HabitLog>(e =>
            {
                e.HasKey(l => l.Id);
                e.HasOne(l => l.Habit)
                 .WithMany(h => h.Logs)
                 .HasForeignKey(l => l.HabitId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<HabitAnalytics>(e =>
            {
                e.HasKey(a => a.Id);
                e.HasOne(a => a.Habit)
                 .WithOne(h => h.Analytics)
                 .HasForeignKey<HabitAnalytics>(a => a.HabitId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}