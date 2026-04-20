using HabitFlow.BLL.DTOs;
using HabitFlow.Domain.Entities;

namespace HabitFlow.BLL.Interfaces
{
    public interface IUserService
    {
        Task<User?> GetByIdAsync(Guid id);

        Task<ProfileViewModel> GetProfileAsync(Guid userId);

        Task<(bool Success, string Error)> EditProfileAsync(Guid userId, EditProfileDto dto);

        Task<(bool Success, string Error)> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);

        Task SaveOnboardingAsync(Guid userId, OnboardingDto dto);
    }
}