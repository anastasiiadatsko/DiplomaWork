using HabitFlow.BLL.Interfaces;
using HabitFlow.BLL.Services;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;

namespace HabitFlow.Tests
{
    public class SharedHabitServiceTests
    {
        [Fact]
        public async Task InviteFriendAsync_ReturnsError_WhenHabitDoesNotBelongToInviter()
        {
            var ownerId = Guid.NewGuid();
            var inviterId = Guid.NewGuid();
            var friendId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = CreateHabit(habitId, ownerId);
            var inviter = CreateUser(inviterId, "owner@test.com", "Owner");
            var friend = CreateUser(friendId, "friend@test.com", "Friend");

            var service = CreateService(
                habits: new List<Habit> { habit },
                users: new List<User> { inviter, friend });

            var result = await service.InviteFriendAsync(
                habitId,
                inviterId,
                friend.Email,
                "https://test/accept",
                "https://test/decline");

            Assert.False(result.Success);
            Assert.Equal("Звичку не знайдено", result.Error);
        }

        [Fact]
        public async Task InviteFriendAsync_ReturnsError_WhenFriendDoesNotExist()
        {
            var ownerId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = CreateHabit(habitId, ownerId);
            var owner = CreateUser(ownerId, "owner@test.com", "Owner");

            var service = CreateService(
                habits: new List<Habit> { habit },
                users: new List<User> { owner });

            var result = await service.InviteFriendAsync(
                habitId,
                ownerId,
                "missing@test.com",
                "https://test/accept",
                "https://test/decline");

            Assert.False(result.Success);
            Assert.Equal("Користувача з таким email не знайдено", result.Error);
        }

        [Fact]
        public async Task InviteFriendAsync_ReturnsError_WhenInvitingYourself()
        {
            var ownerId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var owner = CreateUser(ownerId, "owner@test.com", "Owner");
            var habit = CreateHabit(habitId, ownerId);

            var service = CreateService(
                habits: new List<Habit> { habit },
                users: new List<User> { owner });

            var result = await service.InviteFriendAsync(
                habitId,
                ownerId,
                owner.Email,
                "https://test/accept",
                "https://test/decline");

            Assert.False(result.Success);
            Assert.Equal("Не можна запросити себе до власної звички", result.Error);
        }

        [Fact]
        public async Task InviteFriendAsync_CreatesInvitation_AndSendsEmail()
        {
            var ownerId = Guid.NewGuid();
            var friendId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var owner = CreateUser(ownerId, "owner@test.com", "Owner");
            var friend = CreateUser(friendId, "friend@test.com", "Friend");
            var habit = CreateHabit(habitId, ownerId);

            var sharedRepo = new FakeSharedHabitRepository();
            var emailService = new FakeEmailService();

            var service = CreateService(
                habits: new List<Habit> { habit },
                users: new List<User> { owner, friend },
                sharedRepo: sharedRepo,
                emailService: emailService);

            var result = await service.InviteFriendAsync(
                habitId,
                ownerId,
                friend.Email,
                "https://test/accept",
                "https://test/decline");

            Assert.True(result.Success);
            Assert.Single(sharedRepo.Invitations);
            Assert.Equal(InvitationStatus.Pending, sharedRepo.Invitations[0].Status);
            Assert.Equal(habitId, sharedRepo.Invitations[0].HabitId);
            Assert.Equal(friendId, sharedRepo.Invitations[0].InviteeUserId);

            Assert.Equal(1, emailService.InvitationEmailsSent);
            Assert.Contains("accept?token=", emailService.LastAcceptLink);
            Assert.Contains("decline?token=", emailService.LastDeclineLink);
        }

        [Fact]
        public async Task InviteFriendAsync_AddsOwnerParticipant_WhenOwnerParticipantDoesNotExist()
        {
            var ownerId = Guid.NewGuid();
            var friendId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var owner = CreateUser(ownerId, "owner@test.com", "Owner");
            var friend = CreateUser(friendId, "friend@test.com", "Friend");
            var habit = CreateHabit(habitId, ownerId);

            var sharedRepo = new FakeSharedHabitRepository();

            var service = CreateService(
                habits: new List<Habit> { habit },
                users: new List<User> { owner, friend },
                sharedRepo: sharedRepo);

            await service.InviteFriendAsync(
                habitId,
                ownerId,
                friend.Email,
                "https://test/accept",
                "https://test/decline");

            Assert.Contains(sharedRepo.Participants, p =>
                p.HabitId == habitId &&
                p.UserId == ownerId &&
                p.IsOwner);
        }

        [Fact]
        public async Task InviteFriendAsync_ReturnsError_WhenUserAlreadyParticipant()
        {
            var ownerId = Guid.NewGuid();
            var friendId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var owner = CreateUser(ownerId, "owner@test.com", "Owner");
            var friend = CreateUser(friendId, "friend@test.com", "Friend");
            var habit = CreateHabit(habitId, ownerId);

            var sharedRepo = new FakeSharedHabitRepository();
            sharedRepo.Participants.Add(new HabitParticipant
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = friendId,
                IsOwner = false,
                User = friend,
                Habit = habit,
            });

            var service = CreateService(
                habits: new List<Habit> { habit },
                users: new List<User> { owner, friend },
                sharedRepo: sharedRepo);

            var result = await service.InviteFriendAsync(
                habitId,
                ownerId,
                friend.Email,
                "https://test/accept",
                "https://test/decline");

            Assert.False(result.Success);
            Assert.Equal("Цей користувач вже є учасником звички", result.Error);
        }

        [Fact]
        public async Task AcceptInvitationAsync_AddsParticipant_AndMarksInvitationAccepted()
        {
            var ownerId = Guid.NewGuid();
            var friendId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var token = "test-token";

            var owner = CreateUser(ownerId, "owner@test.com", "Owner");
            var friend = CreateUser(friendId, "friend@test.com", "Friend");
            var habit = CreateHabit(habitId, ownerId);

            var invitation = new HabitInvitation
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                InviterUserId = ownerId,
                InviteeUserId = friendId,
                Status = InvitationStatus.Pending,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                Habit = habit,
                InviterUser = owner,
                InviteeUser = friend,
            };

            var sharedRepo = new FakeSharedHabitRepository();
            sharedRepo.Invitations.Add(invitation);

            var service = CreateService(
                habits: new List<Habit> { habit },
                users: new List<User> { owner, friend },
                sharedRepo: sharedRepo);

            var result = await service.AcceptInvitationAsync(token);

            Assert.True(result.Success);
            Assert.Equal(InvitationStatus.Accepted, invitation.Status);
            Assert.Contains(sharedRepo.Participants, p =>
                p.HabitId == habitId &&
                p.UserId == friendId &&
                !p.IsOwner);
        }

        [Fact]
        public async Task AcceptInvitationAsync_ExpiresOldInvitation()
        {
            var ownerId = Guid.NewGuid();
            var friendId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var token = "expired-token";

            var invitation = new HabitInvitation
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                InviterUserId = ownerId,
                InviteeUserId = friendId,
                Status = InvitationStatus.Pending,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
            };

            var sharedRepo = new FakeSharedHabitRepository();
            sharedRepo.Invitations.Add(invitation);

            var service = CreateService(sharedRepo: sharedRepo);

            var result = await service.AcceptInvitationAsync(token);

            Assert.False(result.Success);
            Assert.Equal("Термін дії запрошення минув", result.Error);
            Assert.Equal(InvitationStatus.Expired, invitation.Status);
        }

        [Fact]
        public async Task DeclineInvitationAsync_MarksInvitationDeclined()
        {
            var token = "decline-token";

            var invitation = new HabitInvitation
            {
                Id = Guid.NewGuid(),
                HabitId = Guid.NewGuid(),
                InviterUserId = Guid.NewGuid(),
                InviteeUserId = Guid.NewGuid(),
                Status = InvitationStatus.Pending,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
            };

            var sharedRepo = new FakeSharedHabitRepository();
            sharedRepo.Invitations.Add(invitation);

            var service = CreateService(sharedRepo: sharedRepo);

            var result = await service.DeclineInvitationAsync(token);

            Assert.True(result.Success);
            Assert.Equal(InvitationStatus.Declined, invitation.Status);
        }

        [Fact]
        public async Task GetParticipantsProgressAsync_ReturnsEmpty_WhenUserHasNoAccess()
        {
            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var habitId = Guid.NewGuid();

            var habit = CreateHabit(habitId, ownerId);
            var service = CreateService(habits: new List<Habit> { habit });

            var result = await service.GetParticipantsProgressAsync(habitId, otherUserId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetParticipantsProgressAsync_ReturnsProgress_ForOwnerAndParticipant()
        {
            var ownerId = Guid.NewGuid();
            var friendId = Guid.NewGuid();
            var habitId = Guid.NewGuid();
            var today = DateTime.Today;

            var owner = CreateUser(ownerId, "owner@test.com", "Owner");
            var friend = CreateUser(friendId, "friend@test.com", "Friend");
            var habit = CreateHabit(habitId, ownerId, today.AddDays(-4));

            var sharedRepo = new FakeSharedHabitRepository();
            sharedRepo.Participants.Add(new HabitParticipant
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = ownerId,
                IsOwner = true,
                JoinedAt = today.AddDays(-4),
                User = owner,
                Habit = habit,
            });
            sharedRepo.Participants.Add(new HabitParticipant
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = friendId,
                IsOwner = false,
                JoinedAt = today.AddDays(-4),
                User = friend,
                Habit = habit,
            });

            var logs = new List<HabitLog>
            {
                CreateCompletedLog(habitId, ownerId, today),
                CreateCompletedLog(habitId, ownerId, today.AddDays(-1)),
                CreateCompletedLog(habitId, friendId, today.AddDays(-1)),
            };

            var service = CreateService(
                habits: new List<Habit> { habit },
                users: new List<User> { owner, friend },
                logs: logs,
                sharedRepo: sharedRepo);

            var result = await service.GetParticipantsProgressAsync(habitId, ownerId);

            Assert.Equal(2, result.Count);

            var ownerProgress = result.First(p => p.UserId == ownerId);
            Assert.True(ownerProgress.IsOwner);
            Assert.Equal("Я", ownerProgress.UserName);
            Assert.True(ownerProgress.IsCompletedToday);
            Assert.Equal(2, ownerProgress.CurrentStreak);
            Assert.Equal(2, ownerProgress.TotalCompleted);

            var friendProgress = result.First(p => p.UserId == friendId);
            Assert.False(friendProgress.IsOwner);
            Assert.Equal("Friend", friendProgress.UserName);
            Assert.False(friendProgress.IsCompletedToday);
            Assert.Equal(1, friendProgress.CurrentStreak);
            Assert.Equal(1, friendProgress.TotalCompleted);
        }

        private static SharedHabitService CreateService(
            List<Habit>? habits = null,
            List<User>? users = null,
            List<HabitLog>? logs = null,
            FakeSharedHabitRepository? sharedRepo = null,
            FakeEmailService? emailService = null)
        {
            return new SharedHabitService(
                new FakeHabitRepository(habits ?? new List<Habit>()),
                new FakeUserRepository(users ?? new List<User>()),
                new FakeHabitLogRepository(logs ?? new List<HabitLog>()),
                sharedRepo ?? new FakeSharedHabitRepository(),
                emailService ?? new FakeEmailService());
        }

        private static User CreateUser(Guid id, string email, string name)
        {
            return new User
            {
                Id = id,
                Email = email,
                Name = name,
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
            };
        }

        private static Habit CreateHabit(Guid id, Guid userId, DateTime? startDate = null)
        {
            return new Habit
            {
                Id = id,
                UserId = userId,
                Name = "Спорт",
                Category = "Health",
                StartDate = startDate ?? DateTime.UtcNow.Date.AddDays(-5),
                FrequencyType = FrequencyType.Daily,
                IsActive = true,
            };
        }

        private static HabitLog CreateCompletedLog(Guid habitId, Guid userId, DateTime date)
        {
            return new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                UserId = userId,
                ScheduledDate = date.Date,
                CompletedAt = date.Date,
                Status = LogStatus.Completed,
            };
        }

        private sealed class FakeHabitRepository : IHabitRepository
        {
            private readonly List<Habit> habits;

            public FakeHabitRepository(List<Habit> habits)
            {
                this.habits = habits;
            }

            public Task<Habit?> GetByIdAsync(Guid id)
            {
                return Task.FromResult(this.habits.FirstOrDefault(h => h.Id == id));
            }

            public Task<List<Habit>> GetByUserIdAsync(Guid userId)
            {
                return Task.FromResult(this.habits.Where(h => h.UserId == userId).ToList());
            }

            public Task AddAsync(Habit habit)
            {
                this.habits.Add(habit);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(Habit habit)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(Guid id)
            {
                this.habits.RemoveAll(h => h.Id == id);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeUserRepository : IUserRepository
        {
            private readonly List<User> users;

            public FakeUserRepository(List<User> users)
            {
                this.users = users;
            }

            public Task<User?> GetByIdAsync(Guid id)
            {
                return Task.FromResult(this.users.FirstOrDefault(u => u.Id == id));
            }

            public Task<User?> GetByEmailAsync(string email)
            {
                return Task.FromResult(this.users.FirstOrDefault(u => u.Email == email));
            }

            public Task AddAsync(User user)
            {
                this.users.Add(user);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(User user)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(User user)
            {
                this.users.Remove(user);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeHabitLogRepository : IHabitLogRepository
        {
            private readonly List<HabitLog> logs;

            public FakeHabitLogRepository(List<HabitLog> logs)
            {
                this.logs = logs;
            }

            public Task<HabitLog?> GetByDateAsync(Guid habitId, DateTime date)
            {
                return Task.FromResult(this.logs.FirstOrDefault(l =>
                    l.HabitId == habitId &&
                    l.ScheduledDate.Date == date.Date));
            }

            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId)
            {
                return Task.FromResult(this.logs.Where(l => l.HabitId == habitId).ToList());
            }

            public Task<List<HabitLog>> GetByHabitIdAsync(Guid habitId, Guid userId)
            {
                return Task.FromResult(this.logs
                    .Where(l => l.HabitId == habitId && l.UserId == userId)
                    .ToList());
            }

            public Task<int> GetCompletedCountByUserIdAsync(Guid userId)
            {
                return Task.FromResult(this.logs.Count(l =>
                    l.UserId == userId &&
                    l.Status == LogStatus.Completed));
            }

            public Task AddAsync(HabitLog log)
            {
                this.logs.Add(log);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(HabitLog log)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeSharedHabitRepository : ISharedHabitRepository
        {
            public List<HabitParticipant> Participants { get; } = new();
            public List<HabitInvitation> Invitations { get; } = new();

            public Task AddParticipantAsync(HabitParticipant participant)
            {
                this.Participants.Add(participant);
                return Task.CompletedTask;
            }

            public Task<HabitParticipant?> GetParticipantAsync(Guid habitId, Guid userId)
            {
                return Task.FromResult(this.Participants.FirstOrDefault(p =>
                    p.HabitId == habitId &&
                    p.UserId == userId));
            }

            public Task<List<Habit>> GetSharedHabitsByUserIdAsync(Guid userId)
            {
                return Task.FromResult(this.Participants
                    .Where(p => p.UserId == userId)
                    .Select(p => p.Habit)
                    .Where(h => h != null)
                    .ToList());
            }

            public Task<List<HabitParticipant>> GetParticipantsByHabitIdAsync(Guid habitId)
            {
                return Task.FromResult(this.Participants
                    .Where(p => p.HabitId == habitId)
                    .ToList());
            }

            public Task AddInvitationAsync(HabitInvitation invitation)
            {
                this.Invitations.Add(invitation);
                return Task.CompletedTask;
            }

            public Task<HabitInvitation?> GetInvitationByTokenAsync(string token)
            {
                return Task.FromResult(this.Invitations.FirstOrDefault(i => i.Token == token));
            }

            public Task<HabitInvitation?> GetPendingInvitationAsync(Guid habitId, Guid inviteeUserId)
            {
                return Task.FromResult(this.Invitations.FirstOrDefault(i =>
                    i.HabitId == habitId &&
                    i.InviteeUserId == inviteeUserId &&
                    i.Status == InvitationStatus.Pending));
            }

            public Task UpdateInvitationAsync(HabitInvitation invitation)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeEmailService : IEmailService
        {
            public int InvitationEmailsSent { get; private set; }
            public string LastAcceptLink { get; private set; } = string.Empty;
            public string LastDeclineLink { get; private set; } = string.Empty;

            public Task SendConfirmationEmailAsync(string toEmail, string userName, string confirmationLink)
            {
                return Task.CompletedTask;
            }

            public Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
            {
                return Task.CompletedTask;
            }

            public Task SendHabitInvitationEmailAsync(
                string toEmail,
                string userName,
                string inviterName,
                string habitName,
                string acceptLink,
                string declineLink)
            {
                this.InvitationEmailsSent++;
                this.LastAcceptLink = acceptLink;
                this.LastDeclineLink = declineLink;
                return Task.CompletedTask;
            }
        }
    }
}