using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Services;
using HabitFlow.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HabitFlow.Tests
{
    public class QuitCoachServiceTests
    {
        [Fact]
        public async Task GetResponseAsync_ReturnsFallback_WhenApiKeyIsMissing()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.CravingSupport,
                UserMessage = "Допоможи",
            });

            Assert.False(string.IsNullOrWhiteSpace(result.Message));
            Assert.NotEmpty(result.SuggestedActions);
            Assert.False(string.IsNullOrWhiteSpace(result.MotivationalNote));
            Assert.Equal(QuitCoachMode.CravingSupport, result.Mode);
        }

        [Fact]
        public async Task GetResponseAsync_FallbackCravingSupport_HasThreeActions()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.CravingSupport,
                CurrentIntensity = 8,
                TriggerDescription = "Стрес на роботі",
            });

            Assert.Equal(3, result.SuggestedActions.Count);
            Assert.Equal(QuitCoachMode.CravingSupport, result.Mode);
        }

        [Fact]
        public async Task GetResponseAsync_FallbackAfterRelapse_HasThreeActions()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.AfterRelapse,
            });

            Assert.Equal(3, result.SuggestedActions.Count);
            Assert.Equal(QuitCoachMode.AfterRelapse, result.Mode);
            Assert.False(string.IsNullOrWhiteSpace(result.MotivationalNote));
        }

        [Fact]
        public async Task GetResponseAsync_FallbackPrevention_HasThreeActions()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.Prevention,
            });

            Assert.Equal(3, result.SuggestedActions.Count);
            Assert.Equal(QuitCoachMode.Prevention, result.Mode);
        }

        [Fact]
        public async Task GetResponseAsync_FallbackCravingSupport_MentionsCleanDays_WhenAnalyticsProvided()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.CravingSupport,
                Analytics = new QuitAnalyticsViewModel
                {
                    CleanDays = 14,
                    RelapseCount = 1,
                    WonCravingsCount = 5,
                    TotalCravingsCount = 6,
                    AverageCravingIntensity = 6.5,
                    RelapseRisk = 30,
                    MostDangerousTime = "Вечір",
                },
            });

            Assert.Contains("14", result.Message);
        }

        [Fact]
        public async Task GetResponseAsync_FallbackCravingSupport_MentionsTriggerTip_WhenTopTriggerExists()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.CravingSupport,
                Analytics = new QuitAnalyticsViewModel
                {
                    CleanDays = 3,
                    MostDangerousTriggers = new List<QuitTriggerStatsDto>
                    {
                        new()
                        {
                            TriggerType = TriggerType.Stress,
                            TriggerName = "Стрес",
                            Count = 5,
                            RelapseCount = 3,
                            AverageIntensity = 7.5,
                            RiskPercent = 60,
                        },
                    },
                },
            });

            Assert.Contains(result.SuggestedActions, a => a.Contains("Стрес"));
        }

        [Fact]
        public async Task GetResponseAsync_FallbackCravingSupport_HighIntensity_HasBreathingAction()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.CravingSupport,
                CurrentIntensity = 9,
            });

            Assert.Contains(result.SuggestedActions, a =>
                a.Contains("таймер") || a.Contains("дихай") || a.Contains("хвилин"));
        }

        [Fact]
        public async Task GetResponseAsync_FallbackPrevention_MentionsDangerousTime_WhenNoTriggers()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.Prevention,
                Analytics = new QuitAnalyticsViewModel
                {
                    CleanDays = 5,
                    MostDangerousTime = "Ранок",
                    MostDangerousTriggers = new List<QuitTriggerStatsDto>(),
                },
            });

            Assert.Contains(result.SuggestedActions, a => a.Contains("Ранок"));
        }

        [Fact]
        public async Task GetResponseAsync_FallbackAfterRelapse_MessageDoesNotContainNegativeWords()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.AfterRelapse,
            });

            Assert.DoesNotContain("провал", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("слабк", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("знову", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetResponseAsync_FallbackCravingSupport_NoAnalytics_ReturnsDefaultTip()
        {
            var service = CreateService(apiKey: string.Empty);

            var result = await service.GetResponseAsync(Guid.NewGuid(), new QuitCoachRequest
            {
                Mode = QuitCoachMode.CravingSupport,
                Analytics = null,
            });

            Assert.Equal(3, result.SuggestedActions.Count);
            Assert.False(string.IsNullOrWhiteSpace(result.MotivationalNote));
        }

        private static QuitCoachService CreateService(string apiKey)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Gemini:ApiKey"] = apiKey,
                    ["Gemini:Model"] = "gemini-test",
                })
                .Build();

            return new QuitCoachService(
                new HttpClient(),
                configuration,
                NullLogger<QuitCoachService>.Instance);
        }
    }
}