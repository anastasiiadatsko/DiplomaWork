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

        public DbSet<HabitParticipant> HabitParticipants => Set<HabitParticipant>();

        public DbSet<HabitInvitation> HabitInvitations => Set<HabitInvitation>();

        public DbSet<TriggerLog> TriggerLogs => Set<TriggerLog>();

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

            modelBuilder.Entity<HabitParticipant>(e =>
            {
                e.HasKey(p => p.Id);

                e.HasIndex(p => new { p.HabitId, p.UserId }).IsUnique();

                e.HasOne(p => p.Habit)
                 .WithMany(h => h.Participants)
                 .HasForeignKey(p => p.HabitId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(p => p.User)
                 .WithMany(u => u.HabitParticipants)
                 .HasForeignKey(p => p.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<HabitInvitation>(e =>
            {
                e.HasKey(i => i.Id);

                e.HasIndex(i => i.Token).IsUnique();

                e.HasOne(i => i.Habit)
                 .WithMany(h => h.Invitations)
                 .HasForeignKey(i => i.HabitId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(i => i.InviterUser)
                 .WithMany(u => u.SentHabitInvitations)
                 .HasForeignKey(i => i.InviterUserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(i => i.InviteeUser)
                 .WithMany(u => u.ReceivedHabitInvitations)
                 .HasForeignKey(i => i.InviteeUserId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TriggerLog>(e =>
            {
                e.HasKey(t => t.Id);

                e.Property(t => t.CravingLevel)
                 .IsRequired();

                e.Property(t => t.TriggerType)
                 .IsRequired();

                e.Property(t => t.TimeOfDay)
                 .HasMaxLength(50);

                e.Property(t => t.Location)
                 .HasMaxLength(200);

                e.Property(t => t.EmotionalState)
                 .HasMaxLength(200);

                e.Property(t => t.Note)
                 .HasMaxLength(1000);

                e.HasOne(t => t.Habit)
                 .WithMany()
                 .HasForeignKey(t => t.HabitId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(t => t.User)
                 .WithMany()
                 .HasForeignKey(t => t.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(t => new { t.HabitId, t.UserId, t.OccurredAt });
            });
        }
    }
}