using HabitFlow.BLL.DTOs;

namespace HabitFlow.BLL.Interfaces
{
    public interface IQuitHabitService
    {
        Task LogCleanDayAsync(Guid habitId, Guid userId);

        Task LogCravingAsync(Guid habitId, Guid userId, LogCravingDto dto);

        Task LogRelapseAsync(Guid habitId, Guid userId, LogRelapseDto dto);

        Task<QuitProgressDto> GetQuitProgressAsync(Guid habitId, Guid userId);
    }
}