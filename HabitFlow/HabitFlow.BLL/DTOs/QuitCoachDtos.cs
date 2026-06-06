using HabitFlow.Domain.Enums;

namespace HabitFlow.BLL.DTOs
{
    public class QuitCoachMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class QuitCoachRequest
    {
        public QuitCoachMode Mode { get; set; }
        public string? UserMessage { get; set; }
        public int? CurrentIntensity { get; set; }
        public string? TriggerDescription { get; set; }
        public List<QuitCoachMessage> History { get; set; } = new();
        public QuitAnalyticsViewModel? Analytics { get; set; }
    }

    public class QuitCoachResponse
    {
        public string Message { get; set; } = string.Empty;
        public List<string> SuggestedActions { get; set; } = new();
        public string MotivationalNote { get; set; } = string.Empty;
        public QuitCoachMode Mode { get; set; }
    }
}