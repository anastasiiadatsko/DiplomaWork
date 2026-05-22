using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
            var completedCount = await this.habitLogRepository.GetCompletedCountByUserIdAsync(userId);
            var habits = await this.habitRepository.GetByUserIdAsync(userId);
            var coachHabitId = habits.FirstOrDefault()?.Id;

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
                new() { Icon = "🔥", Name = "Перший крок", Description = "Виконай першу звичку", IsUnlocked = completedCount >= 1 },
                new() { Icon = "⚡", Name = "Тиждень сили", Description = "7 виконань підряд", IsUnlocked = completedCount >= 7 },
                new() { Icon = "🌟", Name = "На шляху", Description = "20 виконаних звичок", IsUnlocked = completedCount >= 20 },
                new() { Icon = "💎", Name = "Серйозний намір", Description = "50 виконаних звичок", IsUnlocked = completedCount >= 50 },
                new() { Icon = "🏆", Name = "Майстер звичок", Description = "100 виконаних звичок", IsUnlocked = completedCount >= 100 },
                new() { Icon = "🧘", Name = "Дослідник", Description = "Додай 5 різних звичок", IsUnlocked = habits.Count >= 5 },
            };

            BalanceWheelViewModel? balanceWheel = null;
            string? strongestArea = null;
            string? weakestArea = null;
            double balanceAverage = 0;

            if (!string.IsNullOrWhiteSpace(user?.OnboardingDescription) &&
                user.OnboardingDescription.Contains("BalanceWheel"))
            {
                try
                {
                    using var document = JsonDocument.Parse(user.OnboardingDescription);
                    var root = document.RootElement;

                    if (root.TryGetProperty("BalanceWheel", out var balanceElement))
                    {
                        balanceWheel = new BalanceWheelViewModel
                        {
                            Health = balanceElement.GetProperty("Health").GetInt32(),
                            Career = balanceElement.GetProperty("Career").GetInt32(),
                            Finance = balanceElement.GetProperty("Finance").GetInt32(),
                            Relationships = balanceElement.GetProperty("Relationships").GetInt32(),
                            SelfDevelopment = balanceElement.GetProperty("SelfDevelopment").GetInt32(),
                            Rest = balanceElement.GetProperty("Rest").GetInt32(),
                            EmotionalState = balanceElement.GetProperty("EmotionalState").GetInt32(),
                            Environment = balanceElement.GetProperty("Environment").GetInt32()
                        };

                        var areas = new Dictionary<string, int>
                        {
                            { "Здоров’я", balanceWheel.Health },
                            { "Кар’єра / навчання", balanceWheel.Career },
                            { "Фінанси", balanceWheel.Finance },
                            { "Стосунки", balanceWheel.Relationships },
                            { "Саморозвиток", balanceWheel.SelfDevelopment },
                            { "Відпочинок", balanceWheel.Rest },
                            { "Емоційний стан", balanceWheel.EmotionalState },
                            { "Побут / оточення", balanceWheel.Environment }
                        };

                        strongestArea = areas.OrderByDescending(x => x.Value).First().Key;
                        weakestArea = areas.OrderBy(x => x.Value).First().Key;
                        balanceAverage = areas.Average(x => x.Value);
                    }
                }
                catch
                {
                    balanceWheel = null;
                }
            }

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
                BalanceWheel = balanceWheel,
                StrongestBalanceArea = strongestArea,
                WeakestBalanceArea = weakestArea,
                BalanceAverage = balanceAverage,
                CoachHabitId = coachHabitId,
            };
        }

        public async Task<(bool Success, string Error)> EditProfileAsync(Guid userId, EditProfileDto dto)
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

        public async Task<(bool Success, string Error)> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
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

            await this.userRepository.UpdateAsync(user);

            this.logger.LogInformation("Перший крок онбордингу збережено: {UserId}", userId);
        }

        public async Task SaveBalanceWheelAsync(Guid userId, BalanceWheelDto dto)
        {
            var user = await this.userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return;
            }

            var balanceWheelData = new
            {
                DailyDescription = user.OnboardingDescription,
                BalanceWheel = new
                {
                    Health = dto.Health,
                    Career = dto.Career,
                    Finance = dto.Finance,
                    Relationships = dto.Relationships,
                    SelfDevelopment = dto.SelfDevelopment,
                    Rest = dto.Rest,
                    EmotionalState = dto.EmotionalState,
                    Environment = dto.Environment
                }
            };

            user.OnboardingDescription = JsonSerializer.Serialize(balanceWheelData);
            user.IsOnboardingCompleted = true;

            await this.userRepository.UpdateAsync(user);

            this.logger.LogInformation("Колесо балансу збережено: {UserId}", userId);
        }

        public async Task<(bool Success, string Error)> DeleteProfileAsync(
    Guid userId,
    DeleteProfileDto dto)
        {
            var user = await this.userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                return (false, "Користувача не знайдено");
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                return (false, "Пароль невірний");
            }

            await this.userRepository.DeleteAsync(user);

            this.logger.LogInformation("Профіль видалено: {UserId}", userId);
            return (true, string.Empty);
        }
    }
}