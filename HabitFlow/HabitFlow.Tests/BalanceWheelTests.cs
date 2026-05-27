using System.Text.Json;
using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Services;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace HabitFlow.Tests
{
    public class BalanceWheelTests
    {
        [Fact]
        public async Task SaveBalanceWheelAsync_SavesBalanceWheelJson_AndCompletesOnboarding()
        {
            var userId = Guid.NewGuid();

            var user = new User
            {
                Id = userId,
                Email = "test@test.com",
                Name = "Test User",
                PasswordHash = "hash",
                OnboardingDescription = "Хочу краще організувати день",
                IsOnboardingCompleted = false,
            };

            var userRepo = new FakeUserRepository(user);
            var service = CreateService(userRepo);

            await service.SaveBalanceWheelAsync(userId, new BalanceWheelDto
            {
                Health = 8,
                Career = 7,
                Finance = 5,
                Relationships = 6,
                SelfDevelopment = 9,
                Rest = 4,
                EmotionalState = 3,
                Environment = 10,
            });

            Assert.True(user.IsOnboardingCompleted);
            Assert.False(string.IsNullOrWhiteSpace(user.OnboardingDescription));
            Assert.Contains("BalanceWheel", user.OnboardingDescription);

            using var document = JsonDocument.Parse(user.OnboardingDescription!);
            var root = document.RootElement;
            var balanceWheel = root.GetProperty("BalanceWheel");

            Assert.Equal(8, balanceWheel.GetProperty("Health").GetInt32());
            Assert.Equal(7, balanceWheel.GetProperty("Career").GetInt32());
            Assert.Equal(5, balanceWheel.GetProperty("Finance").GetInt32());
            Assert.Equal(6, balanceWheel.GetProperty("Relationships").GetInt32());
            Assert.Equal(9, balanceWheel.GetProperty("SelfDevelopment").GetInt32());
            Assert.Equal(4, balanceWheel.GetProperty("Rest").GetInt32());
            Assert.Equal(3, balanceWheel.GetProperty("EmotionalState").GetInt32());
            Assert.Equal(10, balanceWheel.GetProperty("Environment").GetInt32());

            Assert.Equal(1, userRepo.UpdateCalls);
        }

        [Fact]
        public async Task GetProfileAsync_ReturnsBalanceWheel_WhenJsonExists()
        {
            var userId = Guid.NewGuid();

            var json = JsonSerializer.Serialize(new
            {
                DailyDescription = "test",
                BalanceWheel = new
                {
                    Health = 8,
                    Career = 7,
                    Finance = 2,
                    Relationships = 6,
                    SelfDevelopment = 9,
                    Rest = 4,
                    EmotionalState = 3,
                    Environment = 10,
                },
            });

            var user = new User
            {
                Id = userId,
                Email = "test@test.com",
                Name = "Test User",
                PasswordHash = "hash",
                OnboardingDescription = json,
            };

            var userRepo = new FakeUserRepository(user);
            var service = CreateService(userRepo);

            var result = await service.GetProfileAsync(userId);

            Assert.NotNull(result.BalanceWheel);
            Assert.Equal(8, result.BalanceWheel!.Health);
            Assert.Equal(7, result.BalanceWheel.Career);
            Assert.Equal(2, result.BalanceWheel.Finance);
            Assert.Equal(10, result.BalanceWheel.Environment);

            Assert.Equal("Побут / оточення", result.StrongestBalanceArea);
            Assert.Equal("Фінанси", result.WeakestBalanceArea);
            Assert.Equal(6.125, result.BalanceAverage);
        }

        [Fact]
        public async Task GetProfileAsync_ReturnsNullBalanceWheel_WhenJsonIsInvalid()
        {
            var userId = Guid.NewGuid();

            var user = new User
            {
                Id = userId,
                Email = "test@test.com",
                Name = "Test User",
                PasswordHash = "hash",
                OnboardingDescription = "{ invalid json BalanceWheel",
            };

            var userRepo = new FakeUserRepository(user);
            var service = CreateService(userRepo);

            var result = await service.GetProfileAsync(userId);

            Assert.Null(result.BalanceWheel);
            Assert.Null(result.StrongestBalanceArea);
            Assert.Null(result.WeakestBalanceArea);
            Assert.Equal(0, result.BalanceAverage);
        }

        private static UserService CreateService(FakeUserRepository userRepository)
        {
            return new UserService(
                userRepository,
                new FakeHabitLogRepository(),
                new FakeHabitRepository(),
                NullLogger<UserService>.Instance);
        }

        private sealed class FakeUserRepository : IUserRepository
        {
            private readonly User user;

            public int UpdateCalls { get; private set; }

            public FakeUserRepository(User user)
            {
                this.user = user;
            }

            public Task<User?> GetByIdAsync(Guid id)
            {
                return Task.FromResult(this.user.Id == id ? this.user : null);
            }

            public Task<User?> GetByEmailAsync(string email)
            {
                return Task.FromResult(this.user.Email == email ? this.user : null);
            }

            public Task AddAsync(User user)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(User user)
            {
                this.UpdateCalls++;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(User user)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeHabitLogRepository : IHabitLogRepository
        {
            public Task<HabitLog?> GetByDateAsync(Guid habitId, DateTime date)
            {
                return Task.FromResult<HabitLog?>(null);
            }

            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId)
            {
                return Task.FromResult(new List<HabitLog>());
            }

            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId, Guid userId)
            {
                return Task.FromResult(new List<HabitLog>());
            }

            public Task<int> GetCompletedCountByUserIdAsync(Guid userId)
            {
                return Task.FromResult(0);
            }

            public Task AddAsync(HabitLog log)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(HabitLog log)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeHabitRepository : IHabitRepository
        {
            public Task<Habit?> GetByIdAsync(Guid id)
            {
                return Task.FromResult<Habit?>(null);
            }

            public Task<List<Habit>> GetByUserIdAsync(Guid userId)
            {
                return Task.FromResult(new List<Habit>());
            }

            public Task AddAsync(Habit habit)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(Habit habit)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(Guid id)
            {
                return Task.CompletedTask;
            }
        }
    }
}