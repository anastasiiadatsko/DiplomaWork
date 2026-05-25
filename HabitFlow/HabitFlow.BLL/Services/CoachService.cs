using System.Text;
using System.Text.Json;
using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HabitFlow.BLL.Services
{
    public class CoachService : ICoachService
    {
        private readonly IHabitRepository habitRepository;
        private readonly IHabitLogRepository habitLogRepository;
        private readonly IAnalyticsService analyticsService;
        private readonly HttpClient httpClient;
        private readonly ILogger<CoachService> logger;
        private readonly string geminiApiKey;
        private readonly string geminiModel;

        private const string GeminiUrl =
            "https://generativelanguage.googleapis.com/v1/models/{0}:generateContent?key={1}";

        public CoachService(
            IHabitRepository habitRepository,
            IHabitLogRepository habitLogRepository,
            IAnalyticsService analyticsService,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CoachService> logger)
        {
            this.habitRepository = habitRepository;
            this.habitLogRepository = habitLogRepository;
            this.analyticsService = analyticsService;
            this.httpClient = httpClient;
            this.logger = logger;
            this.geminiApiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
            this.geminiModel = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        }

        // ═══════════════════════════════════════════════════════════
        //  DetectSessionTypeAsync
        // ═══════════════════════════════════════════════════════════

        public async Task<CoachSessionType> DetectSessionTypeAsync(Guid habitId, Guid userId)
        {
            var habit = await this.habitRepository.GetByIdAsync(habitId);
            if (habit == null) return CoachSessionType.FreeChat;

            var allLogs = await this.habitLogRepository.GetByHabitIdAsync(habitId);
            var completed = allLogs
                .Where(l => l.Status == LogStatus.Completed)
                .Select(l => l.ScheduledDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var today = DateTime.Today;
            var days = (today - habit.StartDate.Date).Days + 1;
            var total = completed.Count;

            if (days <= 3 || total == 0)
                return CoachSessionType.Onboarding;

            if (new[] { 7, 14, 21, 30, 66 }.Contains(total))
                return CoachSessionType.MilestoneReached;

            if (!completed.Contains(today.AddDays(-1)) &&
                !completed.Contains(today.AddDays(-2)) &&
                total > 3)
                return CoachSessionType.AfterStreakBreak;

            if (today.DayOfWeek == DayOfWeek.Monday && days > 7)
                return CoachSessionType.WeeklyCheckIn;

            return CoachSessionType.FreeChat;
        }

        // ═══════════════════════════════════════════════════════════
        //  GetSessionQuestionsAsync
        // ═══════════════════════════════════════════════════════════

        public async Task<CoachSessionResponse> GetSessionQuestionsAsync(
            Guid habitId, Guid userId, CoachSessionType sessionType)
        {
            var vm = await this.analyticsService.GetHabitAnalyticsAsync(habitId, userId);
            var ctx = this.BuildContext(vm);

            var (title, emoji, questions) = sessionType switch
            {
                CoachSessionType.Onboarding => this.OnboardingQ(ctx),
                CoachSessionType.WeeklyCheckIn => this.WeeklyQ(ctx),
                CoachSessionType.AfterStreakBreak => this.AfterBreakQ(ctx),
                CoachSessionType.MilestoneReached => this.MilestoneQ(ctx),
                _ => this.FreeChatQ(ctx),
            };

            return new CoachSessionResponse
            {
                SessionType = sessionType,
                SessionTitle = title,
                SessionEmoji = emoji,
                Questions = questions,
                Context = ctx,
            };
        }

        // ── Question sets ────────────────────────────────────────────

        private (string, string, List<CoachQuestion>) OnboardingQ(CoachContext c) => (
            "Знайомство зі звичкою", "👋",
            new List<CoachQuestion>
            {
                new() { Id = "motivation", Text = $"Чому «{c.HabitName}» важлива для тебе?",
                        Hint = "Що спонукало тебе почати?" },
                new() { Id = "best_time",  Text = "О котрій годині тобі найзручніше це робити?",
                        Hint = "Вранці, вдень, ввечері?" },
                new() { Id = "obstacles",  Text = "Що зазвичай заважає тобі дотримуватись звичок?",
                        Hint = "Брак часу, забуваєш, немає настрою..." },
                new() { Id = "minimum",    Text = "Який мінімальний варіант ти можеш зробити у важкий день?",
                        Hint = "2 хвилини замість 10", IsRequired = false },
            });

        private (string, string, List<CoachQuestion>) WeeklyQ(CoachContext c) => (
            "Тижневий огляд", "📋",
            new List<CoachQuestion>
            {
                new() { Id = "feeling",     Text = "Як ти себе почуваєш щодо цієї звички цього тижня?",
                        Hint = "Легко, важко, монотонно?" },
                new() { Id = "obstacle",    Text = "Що найбільше заважало цього тижня?",
                        Hint = "Будь-які перешкоди?", IsRequired = false },
                new() { Id = "what_worked", Text = "Що спрацювало добре?",
                        Hint = "Що допомогло виконати звичку?", IsRequired = false },
                new() { Id = "plan_risky",  Text = $"{c.MostRiskyDay} — твій найскладніший день. Як плануєш?",
                        Hint = "Нагадування, мінімальний варіант?" },
            });

        private (string, string, List<CoachQuestion>) AfterBreakQ(CoachContext c) => (
            "Повернення після перерви", "🔄",
            new List<CoachQuestion>
            {
                new() { Id = "what_happened", Text = "Що трапилось — чому з'явилась пауза?",
                        Hint = "Не для осуду, а щоб зрозуміти" },
                new() { Id = "feelings",      Text = "Як ти себе почуваєш після перерви?",
                        Hint = "Засмучена, байдуже, хочеш повернутись?" },
                new() { Id = "restart",       Text = "Що зробиш сьогодні щоб повернутись?",
                        Hint = "Навіть мінімальне — краще ніж нічого" },
            });

        private (string, string, List<CoachQuestion>) MilestoneQ(CoachContext c)
        {
            var msg = c.TotalCompleted switch
            {
                7 => "Перший тиждень!",
                14 => "Два тижні!",
                21 => "21 день!",
                30 => "Місяць!",
                66 => "Звичка сформована!",
                _ => $"{c.TotalCompleted} виконань!",
            };

            return ($"Досягнення: {msg}", "🏆",
                new List<CoachQuestion>
                {
                    new() { Id = "feeling", Text = $"{c.TotalCompleted} виконань — як це для тебе?",
                            Hint = "Поділись відчуттями" },
                    new() { Id = "changed", Text = "Що змінилось з початку?",
                            Hint = "У поведінці, самопочутті", IsRequired = false },
                    new() { Id = "next",    Text = "Яка твоя наступна ціль?",
                            Hint = c.TotalCompleted < 66
                                ? $"До 66 залишилось {66 - c.TotalCompleted}"
                                : "Нова звичка?",
                            IsRequired = false },
                });
        }

        private (string, string, List<CoachQuestion>) FreeChatQ(CoachContext c) => (
            $"Коуч: «{c.HabitName}»", "🤖",
            new List<CoachQuestion>
            {
                new() { Id = "question", Text = "Що хочеш обговорити?",
                        Hint = "Запитай про звичку або попроси пораду" },
            });

        // ═══════════════════════════════════════════════════════════
        //  GetAdviceAsync
        // ═══════════════════════════════════════════════════════════

        public async Task<CoachAdviceResponse> GetAdviceAsync(
            Guid userId, CoachAdviceRequest request)
        {
            var vm = await this.analyticsService.GetHabitAnalyticsAsync(request.HabitId, userId);
            var ctx = this.BuildContext(vm);

            if (string.IsNullOrEmpty(this.geminiApiKey))
            {
                this.logger.LogWarning("Gemini:ApiKey не налаштовано");
                return this.FallbackAdvice(request.SessionType, ctx);
            }

            var prompt = this.BuildAdvicePrompt(ctx, request);
            var contents = this.BuildContents(request.History.TakeLast(8), prompt);

            var body = new Dictionary<string, object>
            {
                ["contents"] = contents.Cast<object>().ToArray(),
                ["generationConfig"] = new Dictionary<string, object>
                {
                    ["temperature"] = 0.85,
                    ["maxOutputTokens"] = 2048,
                    ["topP"] = 0.95,
                },
            };

            var text = await this.CallGeminiAsync(body);
            if (text == null)
                return this.FallbackAdvice(request.SessionType, ctx);

            return this.ParseAdviceResponse(text);
        }

        // ═══════════════════════════════════════════════════════════
        //  GetSessionSummaryAsync
        // ═══════════════════════════════════════════════════════════

        public async Task<CoachSummaryResponse> GetSessionSummaryAsync(
            Guid userId, CoachSummaryRequest request)
        {
            var vm = await this.analyticsService.GetHabitAnalyticsAsync(request.HabitId, userId);
            var ctx = this.BuildContext(vm);

            if (string.IsNullOrEmpty(this.geminiApiKey))
                return this.FallbackSummary(ctx, request);

            var prompt = this.BuildSummaryPrompt(ctx, request);
            var contents = this.BuildContents(Enumerable.Empty<CoachMessage>(), prompt);

            var body = new Dictionary<string, object>
            {
                ["contents"] = contents.Cast<object>().ToArray(),
                ["generationConfig"] = new Dictionary<string, object>
                {
                    ["temperature"] = 0.7,
                    ["maxOutputTokens"] = 1024,
                    ["topP"] = 0.9,
                },
            };

            var text = await this.CallGeminiAsync(body);
            if (text == null)
                return this.FallbackSummary(ctx, request);

            return this.ParseSummaryResponse(text);
        }

        // ═══════════════════════════════════════════════════════════
        //  Prompts
        // ═══════════════════════════════════════════════════════════

        private string SystemPrompt() =>
            "Ти — HabitCoach, коуч із звичок. Говориш українською, на 'ти'. " +
            "Твій стиль: прямий, підтримуючий, без зайвих слів, іноді з легким гумором. " +
            "ВАЖЛИВО: ти завжди на боці користувача і хочеш щоб він досяг мети. " +
            "Якщо людина хоче кинути звичку або втратила мотивацію — НЕ кажи 'кинь' або 'не роби'. " +
            "Замість цього: визнай що важко, і запропонуй мінімальний крок або нагадай про її прогрес з цифрами. " +
            "ЗАБОРОНЕНО: 'Я тут щоб допомогти', 'Зрозуміло що', 'Якщо щось не відповідає', " +
            "'демонструє стабільність', 'варто подумати', 'інколи буває'. " +
            "Якщо тебе питають 'ти робот?' — відповідай чесно і коротко, наприклад: 'Так, але розумний.' " +
            "Якщо критикують — не вибачайся занадто. " +
            "Відповідай коротко, по суті, можна з іронією. " +
            "Для порад про звичку — використовуй ТІЛЬКИ конкретні цифри з даних, без загальних фраз. " +
            "ФОРМАТ лише для першої поради:\n" +
            "ПОРАДА: [конкретне спостереження з цифрами]\n" +
            "ДІЯ 1: [дія]\n" +
            "ДІЯ 2: [дія]\n" +
            "ДІЯ 3: [дія]\n" +
            "МОТИВАЦІЯ: [одне речення без пафосу]\n" +
            "Для чату — відповідай як людина, без формату. Коротко.";

        private string BuildAdvicePrompt(CoachContext c, CoachAdviceRequest req)
        {
            var sb = new StringBuilder();

            if (req.SessionType == CoachSessionType.FreeChat
                && !string.IsNullOrWhiteSpace(req.UserMessage))
            {
                sb.AppendLine($"Питання користувача: {req.UserMessage}");
                sb.AppendLine("Відповідай ТІЛЬКИ на це питання. " +
                              "Якщо питання не про звичку — просто дай пряму відповідь.");
                sb.AppendLine();
            }

            sb.AppendLine($"[Контекст звички '{c.HabitName}': " +
                          $"серія {c.CurrentStreak}/рекорд {c.MaxStreak}, " +
                          $"{c.TotalCompleted} виконань за {c.DaysSinceStart} днів ({c.ConsistencyRate}%), " +
                          $"найслабший день {c.MostRiskyDay}, найсильніший {c.OptimalDayToAct}, " +
                          $"ризик пропуску завтра {c.BreakRisk}%, " +
                          $"по днях: {string.Join(", ", c.WeekdayStats.Select(w => $"{w.Day}={w.Rate}%"))}]");

            sb.AppendLine($"Тип сесії: {req.SessionType}");

            foreach (var a in req.Answers.Where(x => !string.IsNullOrWhiteSpace(x.Answer)))
                sb.AppendLine($"Відповідь користувача на '{a.QuestionId}': {a.Answer}");

            if (req.SessionType != CoachSessionType.FreeChat
                && !string.IsNullOrWhiteSpace(req.UserMessage))
                sb.AppendLine($"Питання: {req.UserMessage}");

            return sb.ToString();
        }

        private string BuildSummaryPrompt(CoachContext c, CoachSummaryRequest req)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Склади СТРУКТУРОВАНИЙ ПІДСУМОК коучинг-сесії. " +
                          "Відповідай ТІЛЬКИ у форматі нижче.");
            sb.AppendLine();
            sb.AppendLine($"[Звичка: '{c.HabitName}', серія {c.CurrentStreak}, " +
                          $"{c.TotalCompleted} виконань, {c.ConsistencyRate}% дотримання]");
            sb.AppendLine();

            if (req.Answers.Any(a => !string.IsNullOrWhiteSpace(a.Answer)))
            {
                sb.AppendLine("ВІДПОВІДІ КОРИСТУВАЧА:");
                foreach (var a in req.Answers.Where(x => !string.IsNullOrWhiteSpace(x.Answer)))
                    sb.AppendLine($"  {a.QuestionId}: {a.Answer}");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(req.AdviceText))
            {
                sb.AppendLine($"ПОРАДА ЩО БУЛА НАДАНА: {req.AdviceText}");
                sb.AppendLine();
            }

            var chatTurns = req.History.Skip(2).TakeLast(10).ToList();
            if (chatTurns.Any())
            {
                sb.AppendLine("РОЗМОВА В ЧАТІ:");
                foreach (var m in chatTurns)
                    sb.AppendLine($"  [{(m.Role == "user" ? "Користувач" : "Коуч")}]: {m.Content}");
                sb.AppendLine();
            }

            sb.AppendLine("ФОРМАТ ВІДПОВІДІ (строго дотримуйся):");
            sb.AppendLine("ОГЛЯД: [2-3 речення що відбулось на сесії]");
            sb.AppendLine("ІНСАЙТ 1: [ключове спостереження]");
            sb.AppendLine("ІНСАЙТ 2: [ключове спостереження]");
            sb.AppendLine("ІНСАЙТ 3: [ключове спостереження]");
            sb.AppendLine("ДІЯ 1: [конкретна дія на цьому тижні]");
            sb.AppendLine("ДІЯ 2: [конкретна дія на цьому тижні]");
            sb.AppendLine("ДІЯ 3: [конкретна дія на цьому тижні]");
            sb.AppendLine("ЗАКРИТТЯ: [одне заохочувальне речення без пафосу]");

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════
        //  Gemini helpers
        // ═══════════════════════════════════════════════════════════

        private List<Dictionary<string, object>> BuildContents(
            IEnumerable<CoachMessage> history, string userPrompt)
        {
            var contents = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["role"]  = "user",
                    ["parts"] = new[] { new Dictionary<string, string>
                        { ["text"] = "СИСТЕМНІ ІНСТРУКЦІЇ:\n" + this.SystemPrompt() } },
                },
                new()
                {
                    ["role"]  = "model",
                    ["parts"] = new[] { new Dictionary<string, string>
                        { ["text"] = "Зрозуміла. Буду дотримуватись всіх інструкцій." } },
                },
            };

            foreach (var m in history)
            {
                contents.Add(new Dictionary<string, object>
                {
                    ["role"] = m.Role == "coach" ? "model" : "user",
                    ["parts"] = new[] { new Dictionary<string, string>
                        { ["text"] = m.Content } },
                });
            }

            contents.Add(new Dictionary<string, object>
            {
                ["role"] = "user",
                ["parts"] = new[] { new Dictionary<string, string>
                    { ["text"] = userPrompt } },
            });

            return contents;
        }

        private async Task<string?> CallGeminiAsync(Dictionary<string, object> body)
        {
            var url = string.Format(GeminiUrl, this.geminiModel, this.geminiApiKey);
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var resp = await this.httpClient.PostAsync(url, content);
                var raw = await resp.Content.ReadAsStringAsync();

                this.logger.LogInformation("Gemini status={S}", resp.StatusCode);

                if (!resp.IsSuccessStatusCode)
                {
                    this.logger.LogError("Gemini error {S}: {B}",
                        resp.StatusCode, raw.Length > 400 ? raw[..400] : raw);
                    return null;
                }

                var text = this.ExtractText(raw);
                if (string.IsNullOrWhiteSpace(text))
                {
                    this.logger.LogWarning("Gemini: порожня відповідь");
                    return null;
                }

                this.logger.LogInformation("Gemini OK: {P}",
                    text.Length > 80 ? text[..80] : text);
                return text;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Gemini виняток");
                return null;
            }
        }

        private string ExtractText(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var cands = doc.RootElement.GetProperty("candidates");
                if (cands.GetArrayLength() == 0) return string.Empty;

                var first = cands[0];
                if (first.TryGetProperty("finishReason", out var fr))
                {
                    var r = fr.GetString();
                    if (r is "SAFETY" or "RECITATION" or "OTHER")
                    {
                        this.logger.LogWarning("Gemini finishReason={R}", r);
                        return string.Empty;
                    }
                }

                return first
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "ExtractText failed");
                return string.Empty;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Parsers
        // ═══════════════════════════════════════════════════════════

        private CoachAdviceResponse ParseAdviceResponse(string text)
        {
            var advice = string.Empty;
            var actions = new List<string>();
            var motiv = string.Empty;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();

                if (line.StartsWith("ПОРАДА:", StringComparison.OrdinalIgnoreCase))
                    advice = line[7..].Trim();
                else if (line.StartsWith("ДІЯ 1:", StringComparison.OrdinalIgnoreCase))
                    actions.Add(line[6..].Trim());
                else if (line.StartsWith("ДІЯ 2:", StringComparison.OrdinalIgnoreCase))
                    actions.Add(line[6..].Trim());
                else if (line.StartsWith("ДІЯ 3:", StringComparison.OrdinalIgnoreCase))
                    actions.Add(line[6..].Trim());
                else if (line.StartsWith("МОТИВАЦІЯ:", StringComparison.OrdinalIgnoreCase))
                    motiv = line[10..].Trim();
            }

            if (string.IsNullOrWhiteSpace(advice))
                advice = text.Trim()[..Math.Min(500, text.Trim().Length)];

            return new CoachAdviceResponse
            {
                Advice = advice,
                ActionItems = actions,
                Motivation = motiv,
            };
        }

        private CoachSummaryResponse ParseSummaryResponse(string text)
        {
            var overview = string.Empty;
            var insights = new List<string>();
            var actions = new List<string>();
            var closing = string.Empty;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();

                if (line.StartsWith("ОГЛЯД:", StringComparison.OrdinalIgnoreCase))
                    overview = line[6..].Trim();
                else if (line.StartsWith("ІНСАЙТ 1:", StringComparison.OrdinalIgnoreCase))
                    insights.Add(line[9..].Trim());
                else if (line.StartsWith("ІНСАЙТ 2:", StringComparison.OrdinalIgnoreCase))
                    insights.Add(line[9..].Trim());
                else if (line.StartsWith("ІНСАЙТ 3:", StringComparison.OrdinalIgnoreCase))
                    insights.Add(line[9..].Trim());
                else if (line.StartsWith("ДІЯ 1:", StringComparison.OrdinalIgnoreCase))
                    actions.Add(line[6..].Trim());
                else if (line.StartsWith("ДІЯ 2:", StringComparison.OrdinalIgnoreCase))
                    actions.Add(line[6..].Trim());
                else if (line.StartsWith("ДІЯ 3:", StringComparison.OrdinalIgnoreCase))
                    actions.Add(line[6..].Trim());
                else if (line.StartsWith("ЗАКРИТТЯ:", StringComparison.OrdinalIgnoreCase))
                    closing = line[9..].Trim();
            }

            if (string.IsNullOrWhiteSpace(overview))
                overview = text.Trim()[..Math.Min(300, text.Trim().Length)];

            return new CoachSummaryResponse
            {
                Overview = overview,
                KeyInsights = insights,
                ActionPlan = actions,
                ClosingNote = closing,
                SessionDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Fallbacks
        // ═══════════════════════════════════════════════════════════

        private CoachAdviceResponse FallbackAdvice(CoachSessionType type, CoachContext c)
        {
            this.logger.LogWarning("FallbackAdvice для {T}", type);

            return new CoachAdviceResponse
            {
                Advice = type switch
                {
                    CoachSessionType.AfterStreakBreak =>
                        $"Рекорд {c.MaxStreak} днів показує що ти можеш. " +
                        $"Після перерви ймовірність повернутись — {c.MarkovP10}%.",
                    CoachSessionType.MilestoneReached =>
                        $"{c.TotalCompleted} виконань = " +
                        $"{Math.Round(c.TotalCompleted / 66.0 * 100)}% до автоматичної звички. " +
                        $"Слабке місце: {c.MostRiskyDay}.",
                    _ =>
                        $"За {c.DaysSinceStart} днів: {c.TotalCompleted} виконань " +
                        $"({c.ConsistencyRate}%). " +
                        $"Після виконання є {c.MarkovP00}% шанс виконати знову.",
                },
                ActionItems = new List<string>
                {
                    $"Нагадування на {c.MostRiskyDay} за 1 год до звичного часу",
                    "Підготуй мінімальний варіант для важких днів",
                    $"Найсильніший день — {c.OptimalDayToAct}: використай його",
                },
                Motivation = $"Consistency {c.ConsistencyRate}% — вже вище середнього.",
            };
        }

        private CoachSummaryResponse FallbackSummary(CoachContext c, CoachSummaryRequest req)
        {
            this.logger.LogWarning("FallbackSummary для {H}", c.HabitName);

            return new CoachSummaryResponse
            {
                Overview =
                    $"Сесія про звичку «{c.HabitName}». " +
                    $"Поточна серія: {c.CurrentStreak} днів, рекорд: {c.MaxStreak}. " +
                    $"Загальний прогрес: {c.TotalCompleted} виконань ({c.ConsistencyRate}%).",
                KeyInsights = new List<string>
                {
                    $"Найслабший день — {c.MostRiskyDay}: потребує уваги.",
                    $"Найсильніший день — {c.OptimalDayToAct}: використовуй для складніших завдань.",
                    $"Ризик пропуску завтра: {c.BreakRisk}%.",
                },
                ActionPlan = req.ActionItems.Any()
                    ? req.ActionItems
                    : new List<string>
                    {
                        $"Нагадування на {c.MostRiskyDay} за годину до звичного часу.",
                        "Мінімальний варіант звички для важких днів.",
                        $"Зберегти серію до {c.CurrentStreak + 7} днів.",
                    },
                ClosingNote =
                    $"Ти вже {c.TotalCompleted} разів довела собі що можеш. Продовжуй.",
                SessionDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  BuildContext
        // ═══════════════════════════════════════════════════════════

        private CoachContext BuildContext(AnalyticsViewModel vm) => new()
        {
            HabitName = vm.HabitName,
            DaysSinceStart = vm.DaysSinceStart,
            CurrentStreak = vm.CurrentStreak,
            MaxStreak = vm.MaxStreak,
            ConsistencyRate = vm.ConsistencyRate,
            TotalCompleted = vm.TotalCompleted,
            BreakRisk = vm.BreakRisk,
            IsStreakActive = vm.IsStreakActive,
            MostRiskyDay = vm.MostRiskyDay,
            OptimalDayToAct = vm.OptimalDayToAct,
            MarkovP00 = vm.MarkovP00,
            MarkovP10 = vm.MarkovP10,
            HabitStrengthScore = vm.HabitStrengthScore,
            WeekdayStats = vm.WeekdayStats,
        };
    }
}