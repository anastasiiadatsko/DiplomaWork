using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace HabitFlow.BLL.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository userRepository;
        private readonly IEmailService emailService;
        private readonly ILogger<AuthService> logger;

        public AuthService(
            IUserRepository userRepository,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            this.userRepository = userRepository;
            this.emailService = emailService;
            this.logger = logger;
        }

        public async Task<(bool Success, string Error)> RegisterAsync(
            RegisterDto dto,
            string confirmationLink)
        {
            var existing = await this.userRepository.GetByEmailAsync(dto.Email);
            if (existing != null)
            {
                return (false, "Користувач з таким email вже існує");
            }

            var token = Guid.NewGuid().ToString();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsEmailConfirmed = false,
                EmailConfirmationToken = token,
                AvatarId = 1,
                CreatedAt = DateTime.UtcNow,
            };

            await this.userRepository.AddAsync(user);

            await this.emailService.SendConfirmationEmailAsync(
                dto.Email,
                dto.Name,
                confirmationLink + $"?token={token}&email={dto.Email}");

            this.logger.LogInformation("Новий користувач: {Email}", dto.Email);
            return (true, string.Empty);
        }

        public async Task<(bool Success, string Error, User? User)> LoginAsync(LoginDto dto)
        {
            var user = await this.userRepository.GetByEmailAsync(dto.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                this.logger.LogWarning("Невдала спроба входу: {Email}", dto.Email);
                return (false, "Невірний email або пароль", null);
            }

            if (!user.IsEmailConfirmed)
            {
                return (false, "Підтвердіть email перед входом. Перевірте пошту.", null);
            }

            this.logger.LogInformation("Користувач увійшов: {Email}", dto.Email);
            return (true, string.Empty, user);
        }

        public async Task<(bool Success, string Error)> ConfirmEmailAsync(
            string email,
            string token)
        {
            var user = await this.userRepository.GetByEmailAsync(email);

            if (user == null || user.EmailConfirmationToken != token)
            {
                return (false, "Невірне посилання підтвердження");
            }

            if (user.IsEmailConfirmed)
            {
                return (true, string.Empty);
            }

            user.IsEmailConfirmed = true;
            user.EmailConfirmationToken = null;
            await this.userRepository.UpdateAsync(user);

            this.logger.LogInformation("Email підтверджено: {Email}", email);
            return (true, string.Empty);
        }

        public async Task<(bool Success, string Error)> ForgotPasswordAsync(
            string email,
            string resetLink)
        {
            var user = await this.userRepository.GetByEmailAsync(email);

            if (user == null || !user.IsEmailConfirmed)
            {
                // Не розкриваємо чи існує email
                return (true, string.Empty);
            }

            var token = Guid.NewGuid().ToString();
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await this.userRepository.UpdateAsync(user);

            var fullResetLink = resetLink + $"?token={token}&email={email}";
            await this.emailService.SendPasswordResetEmailAsync(email, user.Name, fullResetLink);

            this.logger.LogInformation("Запит відновлення паролю: {Email}", email);
            return (true, string.Empty);
        }

        public async Task<(bool Success, string Error)> ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await this.userRepository.GetByEmailAsync(dto.Email);

            if (user == null
                || user.PasswordResetToken != dto.Token
                || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            {
                return (false, "Невірне або застаріле посилання");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            await this.userRepository.UpdateAsync(user);

            this.logger.LogInformation("Пароль змінено: {Email}", dto.Email);
            return (true, string.Empty);
        }
    }
}