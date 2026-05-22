using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;

namespace HabitFlow.BLL.Services
{
    public class SharedHabitService : ISharedHabitService
    {
        private readonly IHabitRepository habitRepository;
        private readonly IUserRepository userRepository;
        private readonly IHabitLogRepository habitLogRepository;
        private readonly ISharedHabitRepository sharedHabitRepository;
        private readonly IEmailService emailService;

        public SharedHabitService(
            IHabitRepository habitRepository,
            IUserRepository userRepository,
            IHabitLogRepository habitLogRepository,
            ISharedHabitRepository sharedHabitRepository,
            IEmailService emailService)
        {
            this.habitRepository = habitRepository;
            this.userRepository = userRepository;
            this.habitLogRepository = habitLogRepository;
            this.sharedHabitRepository = sharedHabitRepository;
            this.emailService = emailService;
        }

        public async Task<(bool Success, string Error)> InviteFriendAsync(
            Guid habitId,
            Guid inviterUserId,
            string friendEmail,
            string acceptBaseUrl,
            string declineBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(friendEmail))
            {
                return (true, string.Empty);
            }

            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null || habit.UserId != inviterUserId)
            {
                return (false, "Звичку не знайдено");
            }

            var inviter = await this.userRepository.GetByIdAsync(inviterUserId);
            if (inviter == null)
            {
                return (false, "Користувача не знайдено");
            }

            var friend = await this.userRepository.GetByEmailAsync(friendEmail.Trim());
            if (friend == null)
            {
                return (false, "Користувача з таким email не знайдено");
            }

            if (friend.Id == inviterUserId)
            {
                return (false, "Не можна запросити себе до власної звички");
            }

            var ownerParticipant = await this.sharedHabitRepository
                .GetParticipantAsync(habitId, inviterUserId);

            if (ownerParticipant == null)
            {
                await this.sharedHabitRepository.AddParticipantAsync(new HabitParticipant
                {
                    Id = Guid.NewGuid(),
                    HabitId = habitId,
                    UserId = inviterUserId,
                    IsOwner = true,
                    JoinedAt = DateTime.UtcNow,
                });
            }

            var existingParticipant = await this.sharedHabitRepository
                .GetParticipantAsync(habitId, friend.Id);

            if (existingParticipant != null)
            {
                return (false, "Цей користувач вже є учасником звички");
            }

            var existingInvitation = await this.sharedHabitRepository
                .GetPendingInvitationAsync(habitId, friend.Id);

            if (existingInvitation != null)
            {
                return (false, "Запрошення цьому користувачу вже надіслано");
            }

            var token = Guid.NewGuid().ToString();

            var invitation = new HabitInvitation
            {
                Id = Guid.NewGuid(),
                HabitId = habitId,
                InviterUserId = inviterUserId,
                InviteeUserId = friend.Id,
                Status = InvitationStatus.Pending,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(3),
            };

            await this.sharedHabitRepository.AddInvitationAsync(invitation);

            var acceptLink = $"{acceptBaseUrl}?token={token}";
            var declineLink = $"{declineBaseUrl}?token={token}";

            await this.emailService.SendHabitInvitationEmailAsync(
                friend.Email,
                friend.Name,
                inviter.Name,
                habit.Name,
                acceptLink,
                declineLink);

            return (true, string.Empty);
        }

        public async Task<(bool Success, string Error)> AcceptInvitationAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "Некоректне посилання");
            }

            var invitation = await this.sharedHabitRepository.GetInvitationByTokenAsync(token);
            if (invitation == null)
            {
                return (false, "Запрошення не знайдено");
            }

            if (invitation.Status != InvitationStatus.Pending)
            {
                return (false, "Запрошення вже було оброблено");
            }

            if (invitation.ExpiresAt < DateTime.UtcNow)
            {
                invitation.Status = InvitationStatus.Expired;
                await this.sharedHabitRepository.UpdateInvitationAsync(invitation);

                return (false, "Термін дії запрошення минув");
            }

            var existingParticipant = await this.sharedHabitRepository.GetParticipantAsync(
                invitation.HabitId,
                invitation.InviteeUserId);

            if (existingParticipant == null)
            {
                await this.sharedHabitRepository.AddParticipantAsync(new HabitParticipant
                {
                    Id = Guid.NewGuid(),
                    HabitId = invitation.HabitId,
                    UserId = invitation.InviteeUserId,
                    IsOwner = false,
                    JoinedAt = DateTime.UtcNow,
                });
            }

            invitation.Status = InvitationStatus.Accepted;
            await this.sharedHabitRepository.UpdateInvitationAsync(invitation);

            return (true, string.Empty);
        }

        public async Task<(bool Success, string Error)> DeclineInvitationAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "Некоректне посилання");
            }

            var invitation = await this.sharedHabitRepository.GetInvitationByTokenAsync(token);
            if (invitation == null)
            {
                return (false, "Запрошення не знайдено");
            }

            if (invitation.Status != InvitationStatus.Pending)
            {
                return (false, "Запрошення вже було оброблено");
            }

            invitation.Status = InvitationStatus.Declined;
            await this.sharedHabitRepository.UpdateInvitationAsync(invitation);

            return (true, string.Empty);
        }

        public async Task<List<SharedHabitParticipantProgressDto>> GetParticipantsProgressAsync(
            Guid habitId,
            Guid currentUserId)
        {
            var currentParticipant = await this.sharedHabitRepository
                .GetParticipantAsync(habitId, currentUserId);

            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null)
            {
                return new List<SharedHabitParticipantProgressDto>();
            }

            if (habit.UserId != currentUserId && currentParticipant == null)
            {
                return new List<SharedHabitParticipantProgressDto>();
            }

            var ownerParticipant = await this.sharedHabitRepository
                .GetParticipantAsync(habitId, habit.UserId);

            if (ownerParticipant == null)
            {
                await this.sharedHabitRepository.AddParticipantAsync(new HabitParticipant
                {
                    Id = Guid.NewGuid(),
                    HabitId = habitId,
                    UserId = habit.UserId,
                    IsOwner = true,
                    JoinedAt = DateTime.UtcNow,
                });
            }

            var participants = await this.sharedHabitRepository
                .GetParticipantsByHabitIdAsync(habitId);

            var logs = await this.habitLogRepository.GetByHabitIdAsync(habitId);
            var today = DateTime.UtcNow.Date;
            var daysSinceStart = Math.Max(1, (today - habit.StartDate.Date).Days + 1);

            return participants
                .Select(p =>
                {
                    var userLogs = logs
                        .Where(l => l.UserId == p.UserId && l.Status == LogStatus.Completed)
                        .ToList();

                    var completedDates = userLogs
                        .Select(l => l.ScheduledDate.Date)
                        .ToHashSet();

                    return new SharedHabitParticipantProgressDto
                    {
                        UserId = p.UserId,
                        UserName = p.UserId == currentUserId ? "Я" : p.User.Name,
                        UserEmail = p.User.Email,
                        IsOwner = p.IsOwner,
                        IsCompletedToday = completedDates.Contains(today),
                        CurrentStreak = this.CalculateCurrentStreak(completedDates, today),
                        TotalCompleted = userLogs.Count,
                        ConsistencyRate = Math.Round(userLogs.Count * 100.0 / daysSinceStart, 1),
                    };
                })
                .OrderByDescending(p => p.IsOwner)
                .ThenBy(p => p.UserName)
                .ToList();
        }

        private int CalculateCurrentStreak(HashSet<DateTime> completedDates, DateTime today)
        {
            var streak = 0;
            var date = today;

            while (completedDates.Contains(date))
            {
                streak++;
                date = date.AddDays(-1);
            }

            return streak;
        }
    }
}