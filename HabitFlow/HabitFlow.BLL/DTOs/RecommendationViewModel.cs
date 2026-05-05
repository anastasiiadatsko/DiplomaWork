namespace HabitFlow.BLL.DTOs
{
    public class RecommendationViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public int Priority { get; set; }

        public Guid? HabitId { get; set; }

        public string? HabitName { get; set; }
    }
}