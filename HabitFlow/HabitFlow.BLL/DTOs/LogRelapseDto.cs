using HabitFlow.Domain.Enums;

namespace HabitFlow.BLL.DTOs
{
    public class LogRelapseDto
    {
        public int CravingLevel { get; set; }

        public TriggerType TriggerType { get; set; }

        public string? TimeOfDay { get; set; }

        public string? Location { get; set; }

        public string? EmotionalState { get; set; }

        public string? Note { get; set; }
    }
}