namespace HabitFlow.BLL.DTOs
{
    public enum CoachSessionType
    {
        Onboarding,     
        WeeklyCheckIn,  
        AfterStreakBreak, 
        MilestoneReached, 
        FreeChat,       
    }

    public class CoachQuestion
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty; // підказка під полем
        public bool IsRequired { get; set; } = true;
    }

    public class CoachAnswer
    {
        public string QuestionId { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
    }
    public class CoachSessionRequest
    {
        public Guid HabitId { get; set; }
        public CoachSessionType SessionType { get; set; }
    }

    public class CoachSessionResponse
    {
        public CoachSessionType SessionType { get; set; }
        public string SessionTitle { get; set; } = string.Empty;
        public string SessionEmoji { get; set; } = string.Empty;
        public List<CoachQuestion> Questions { get; set; } = new();

        public CoachContext Context { get; set; } = new();
    }

    public class CoachAdviceRequest
    {
        public Guid HabitId { get; set; }
        public CoachSessionType SessionType { get; set; }
        public List<CoachAnswer> Answers { get; set; } = new();
        public List<CoachMessage> History { get; set; } = new(); 
        public string? UserMessage { get; set; }        
    }

    public class CoachAdviceResponse
    {
        public string Advice { get; set; } = string.Empty;
        public List<string> ActionItems { get; set; } = new(); 
        public string Motivation { get; set; } = string.Empty;
        public bool IsStreaming { get; set; } = false;
    }
    public class CoachMessage
    {
        public string Role { get; set; } = string.Empty; 
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }

    public class CoachContext
    {
        public string HabitName { get; set; } = string.Empty;
        public int DaysSinceStart { get; set; }
        public int CurrentStreak { get; set; }
        public int MaxStreak { get; set; }
        public double ConsistencyRate { get; set; }
        public int TotalCompleted { get; set; }
        public double BreakRisk { get; set; }
        public bool IsStreakActive { get; set; }
        public string MostRiskyDay { get; set; } = string.Empty;
        public string OptimalDayToAct { get; set; } = string.Empty;
        public double MarkovP00 { get; set; } 
        public double MarkovP10 { get; set; } 
        public double HabitStrengthScore { get; set; }
        public List<WeekdayStats> WeekdayStats { get; set; } = new();
    }
}