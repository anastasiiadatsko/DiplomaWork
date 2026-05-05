using System.ComponentModel.DataAnnotations;

namespace HabitFlow.BLL.DTOs
{
    public class BalanceWheelDto
    {
        [Range(1, 10)]
        public int Health { get; set; }

        [Range(1, 10)]
        public int Career { get; set; }

        [Range(1, 10)]
        public int Finance { get; set; }

        [Range(1, 10)]
        public int Relationships { get; set; }

        [Range(1, 10)]
        public int SelfDevelopment { get; set; }

        [Range(1, 10)]
        public int Rest { get; set; }

        [Range(1, 10)]
        public int EmotionalState { get; set; }

        [Range(1, 10)]
        public int Environment { get; set; }
    }
}