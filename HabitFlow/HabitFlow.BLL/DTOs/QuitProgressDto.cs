namespace HabitFlow.BLL.DTOs
{
    public class QuitProgressDto
    {
        public Guid HabitId { get; set; }

        public int CleanDays { get; set; }

        public int RelapsesCount { get; set; }

        public int DefeatedCravings { get; set; }

        public double AverageCravingLevel { get; set; }
    }
}