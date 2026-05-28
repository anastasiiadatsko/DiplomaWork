using HabitFlow.Domain.Enums;

namespace HabitFlow.BLL.Interfaces
{
    public interface IGoogleCalendarService
    {
        string BuildAuthorizationUrl(Guid userId);

        Task<bool> HandleCallbackAsync(Guid userId, string code);

        Task<bool> DisconnectAsync(Guid userId);

        Task<bool> CreateHabitReminderEventAsync(
    Guid userId,
    string habitName,
    DateTime date,
    TimeOnly reminderTime,
    FrequencyType frequencyType,
    List<DayOfWeek> targetDays);

        string BuildGoogleCalendarTemplateUrl(
    string habitName,
    string? description,
    DateTime date,
    TimeOnly reminderTime,
    FrequencyType frequencyType,
    List<DayOfWeek> targetDays);
    }
}