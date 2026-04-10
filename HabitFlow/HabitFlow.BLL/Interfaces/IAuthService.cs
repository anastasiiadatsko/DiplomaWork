using HabitFlow.BLL.DTOs;
using HabitFlow.Domain.Entities;

namespace HabitFlow.BLL.Interfaces
{
    public interface IAuthService
    {
        Task<(bool Success, string Error)> RegisterAsync(
            RegisterDto dto,
            string confirmationLink);

        Task<(bool Success, string Error, User? User)> LoginAsync(LoginDto dto);

        Task<(bool Success, string Error)> ConfirmEmailAsync(
            string email,
            string token);
    }
}