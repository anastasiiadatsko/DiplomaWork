using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace HabitFlow.BLL.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository userRepository;
        private readonly IHabitLogRepository habitLogRepository;
        private readonly IHabitRepository habitRepository;
        private readonly ILogger<UserService> logger;

        public UserService(
            IUserRepository userRepository,
            IHabitLogRepository habitLogRepository,
            IHabitRepository habitRepository,
            ILogger<UserService> logger)
        {
            this.userRepository = userRepository;
            this.habitLogRepository = habitLogRepository;
            this.habitRepository = habitRepository;
            this.logger = logger;
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await this.userRepository.GetByIdAsync(id);
        }

        public async Task<ProfileViewModel> GetProfileAsync(Guid userId)
        {
            var user = await this.userRepository.GetByIdAsync(userId);
            var completedCount = await this.habitLogRepository
                .GetCompletedCountByUserIdAsync(userId);
            var habits = await this.habitRepository.GetByUserIdAsync(userId);

            var starLevel = completedCount switch
            {
                < 6 => 1,
                < 21 => 2,
                < 51 => 3,
                < 101 => 4,
                _ => 5,
            };

            var starLevelName = starLevel switch
            {
                1 => "Початківець ✨",
                2 => "На шляху 🌟",
                3 => "Зростаєш 💫",
                4 => "Майстер звичок ⭐",
                5 => "Легенда 🌠",
                _ => "Початківець",
            };

            var nextLevelAt = starLevel switch
            {
                1 => 6,
                2 => 21,
                3 => 51,
                4 => 101,
                5 => 0,
                _ => 6,
            };

            var progressPercent = starLevel switch
            {
                1 => (int)((double)completedCount / 6 * 100),
                2 => (int)(((double)completedCount - 6) / 15 * 100),
                3 => (int)(((double)completedCount - 21) / 30 * 100),
                4 => (int)(((double)completedCount - 51) / 50 * 100),
                5 => 100,
                _ => 0,
            };

            var achievements = new List<Achievement>
            {
                new Achievement
                {
                    Icon = "🔥",
                    Name = "Перший крок",
                    Description = "Виконай першу звичку",
                    IsUnlocked = completedCount >= 1,
                },
                new Achievement
                {
                    Icon = "⚡",
                    Name = "Тиждень сили",
                    Description = "7 виконань підряд",
                    IsUnlocked = completedCount >= 7,
                },
                new Achievement
                {
                    Icon = "🌟",
                    Name = "На шляху",
                    Description = "20 виконаних звичок",
                    IsUnlocked = completedCount >= 20,
                },
                new Achievement
                {
                    Icon = "💎",
                    Name = "Серйозний намір",
                    Description = "50 виконаних звичок",
                    IsUnlocked = completedCount >= 50,
                },
                new Achievement
                {
                    Icon = "🏆",
                    Name = "Майстер звичок",
                    Description = "100 виконаних звичок",
                    IsUnlocked = completedCount >= 100,
                },
                new Achievement
                {
                    Icon = "🧘",
                    Name = "Дослідник",
                    Description = "Додай 5 різних звичок",
                    IsUnlocked = habits.Count >= 5,
                },
            };

            return new ProfileViewModel
            {
                User = user!,
                TotalCompletedHabits = completedCount,
                TotalHabits = habits.Count,
                StarLevel = starLevel,
                StarLevelName = starLevelName,
                NextLevelAt = nextLevelAt,
                ProgressPercent = Math.Min(progressPercent, 100),
                Achievements = achievements,
            };
        }

        public async Task<(bool Success, string Error)> EditProfileAsync(
            Guid userId, EditProfileDto dto)
        {
            var user = await this.userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return (false, "Користувача не знайдено");
            }

            user.Name = dto.Name;
            user.TimeZone = dto.TimeZone;
            user.NotificationsEnabled = dto.NotificationsEnabled;
            await this.userRepository.UpdateAsync(user);

            this.logger.LogInformation("Профіль оновлено: {UserId}", userId);
            return (true, string.Empty);
        }

        public async Task<(bool Success, string Error)> ChangePasswordAsync(
            Guid userId, ChangePasswordDto dto)
        {
            var user = await this.userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return (false, "Користувача не знайдено");
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                return (false, "Поточний пароль невірний");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await this.userRepository.UpdateAsync(user);

            this.logger.LogInformation("Пароль змінено: {UserId}", userId);
            return (true, string.Empty);
        }

        public async Task SaveOnboardingAsync(Guid userId, OnboardingDto dto)
        {
            var user = await this.userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return;
            }

            user.OnboardingGoal = dto.Goal;
            user.OnboardingDescription = dto.Description;
            user.OnboardingTime = dto.AvailableTime;
            user.IsOnboardingCompleted = true;
            await this.userRepository.UpdateAsync(user);

            this.logger.LogInformation("Онбординг завершено: {UserId}", userId);
        }
    }
}