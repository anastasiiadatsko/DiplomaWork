namespace HabitFlow.BLL.DTOs
{
    public class ManualLogDto
    {
        public Guid HabitId { get; set; }

        public DateTime Date { get; set; }

        public string? Note { get; set; }
    }
}