using System.Text;
using System.Text.Json;
using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HabitFlow.Domain.Enums;

namespace HabitFlow.BLL.Services
{
    public class QuitCoachService : IQuitCoachService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<QuitCoachService> logger;
        private readonly string geminiApiKey;
        private readonly string geminiModel;

        private const string GeminiUrl =
            "https://generativelanguage.googleapis.com/v1/models/{0}:generateContent?key={1}";

        public QuitCoachService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<QuitCoachService> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
            this.geminiApiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
            this.geminiModel = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        }

        public async Task<QuitCoachResponse> GetResponseAsync(Guid userId, QuitCoachRequest request)
        {
            if (string.IsNullOrEmpty(this.geminiApiKey))
            {
                this.logger.LogWarning("Gemini:ApiKey не налаштовано — fallback для QuitCoach");
                return this.FallbackResponse(request);
            }

            var prompt = this.BuildPrompt(request);
            var contents = this.BuildContents(request.History.TakeLast(8), prompt);

            var body = new Dictionary<string, object>
            {
                ["contents"] = contents.Cast<object>().ToArray(),
                ["generationConfig"] = new Dictionary<string, object>
                {
                    ["temperature"] = 0.80,
                    ["maxOutputTokens"] = 1024,
                    ["topP"] = 0.95,
                },
            };

            var text = await this.CallGeminiAsync(body);
            if (text == null)
                return this.FallbackResponse(request);

            return this.ParseResponse(text, request.Mode);
        }

        private string SystemPrompt() =>
            "Ти — QuitCoach, AI-агент підтримки в режимі кидання шкідливої звички. " +
            "Говориш українською, на 'ти'. Тон: теплий, спокійний, уважний, без пафосу і моралізаторства. " +
            "Твоя роль — бути поруч у складний момент, допомогти пережити потяг, м'яко повернути після зриву " +
            "і підготувати план профілактики. " +
            "Ти НЕ лікар, НЕ терапевт і НЕ даєш медичних порад. Якщо людина згадує небезпеку для себе або інших, " +
            "м'яко порадь звернутися до близької людини або місцевої екстреної допомоги. " +
            "ЗАБОРОНЕНО: осуджувати, соромити, лякати, називати людину слабкою, казати 'провал', " +
            "'ти знову', 'просто не роби', 'візьми себе в руки'. " +
            "ПІДТРИМКА: спершу визнай стан людини одним коротким реченням, потім дай конкретний наступний крок. " +
            "Якщо потяг активний — дій швидко: короткі речення, заземлення, дистанція від тригера, план на 10-15 хвилин. " +
            "Якщо стався зрив — не драматизуй: відділи зрив від особистості, запропонуй відновлення без самопокарання. " +
            "Якщо профілактика — допоможи знайти тригер, ранній сигнал, заміну і план 'якщо-то'. " +
            "Використовуй цифри з аналітики тільки як підтримку, а не як вирок. " +
            "ФОРМАТ для першої відповіді або структурованої підтримки:\n" +
            "ВІДПОВІДЬ: [1-2 речення теплої підтримки]\n" +
            "ДІЯ 1: [негайна дія]\n" +
            "ДІЯ 2: [дія]\n" +
            "ДІЯ 3: [дія]\n" +
            "НОТАТКА: [одне підтримуюче речення]\n" +
            "Для звичайного чату після цього — відповідай як жива підтримуюча людина, коротко, без службового формату.";

        private string BuildPrompt(QuitCoachRequest request)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Режим агента: {this.GetModeName(request.Mode)}");

            if (request.Analytics != null)
            {
                var a = request.Analytics;
                sb.AppendLine($"[Контекст користувача: {a.CleanDays} чистих днів, " +
                              $"{a.RelapseCount} зривів, " +
                              $"пережито без зриву потягів: {a.WonCravingsCount}/{a.TotalCravingsCount}, " +
                              $"середня сила потягу: {a.AverageCravingIntensity}/10, " +
                              $"ризик зриву: {a.RelapseRisk}%, " +
                              $"найризикованіший час: {a.MostDangerousTime}]");

                if (a.MostDangerousTriggers.Any())
                {
                    var triggers = string.Join(", ", a.MostDangerousTriggers.Select(t =>
                        $"{t.TriggerName}: {t.Count} разів, ризик {t.RiskPercent}%, сила {t.AverageIntensity}/10"));
                    sb.AppendLine($"[Тригери: {triggers}]");
                }

                if (!string.IsNullOrWhiteSpace(a.MainInsight))
                    sb.AppendLine($"[Інсайт системи: {a.MainInsight}]");

                if (!string.IsNullOrWhiteSpace(a.ActionTip))
                    sb.AppendLine($"[Поточна порада системи: {a.ActionTip}]");
            }

            switch (request.Mode)
            {
                case QuitCoachMode.CravingSupport:
                    if (request.CurrentIntensity.HasValue)
                        sb.AppendLine($"Зараз потяг силою {request.CurrentIntensity}/10.");
                    if (!string.IsNullOrWhiteSpace(request.TriggerDescription))
                        sb.AppendLine($"Тригер: {request.TriggerDescription}");
                    sb.AppendLine("Людина зараз переживає потяг. Дай підтримку і план на наступні 10-15 хвилин.");
                    sb.AppendLine("Почни з короткого визнання: це важко, але хвиля мине. Потім дай 3 прості дії.");
                    break;

                case QuitCoachMode.AfterRelapse:
                    sb.AppendLine("Щойно стався зрив. Людині потрібна підтримка без осуду та план повернення.");
                    sb.AppendLine("Не аналізуй жорстко. Спершу стабілізуй, потім запропонуй один маленький крок повернення.");
                    break;

                case QuitCoachMode.Prevention:
                    sb.AppendLine("Профілактична розмова. Допоможи підготуватись до ризикових ситуацій.");
                    sb.AppendLine("Склади короткий план: ранній сигнал, дія-замінник, людина/місце підтримки.");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(request.UserMessage))
                sb.AppendLine($"Повідомлення: {request.UserMessage}");
            else
                sb.AppendLine("Користувач не написав повідомлення. Дай проактивну підтримку для поточного режиму.");

            return sb.ToString();
        }

        private string GetModeName(QuitCoachMode mode) => mode switch
        {
            QuitCoachMode.CravingSupport => "Підтримка під час потягу",
            QuitCoachMode.AfterRelapse => "Аналіз після зриву",
            QuitCoachMode.Prevention => "Профілактика",
            _ => "Загальна підтримка",
        };

        private List<Dictionary<string, object>> BuildContents(
            IEnumerable<QuitCoachMessage> history, string userPrompt)
        {
            var contents = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["role"] = "user",
                    ["parts"] = new[] { new Dictionary<string, string>
                        { ["text"] = "СИСТЕМНІ ІНСТРУКЦІЇ:\n" + this.SystemPrompt() } },
                },
                new()
                {
                    ["role"] = "model",
                    ["parts"] = new[] { new Dictionary<string, string>
                        { ["text"] = "Зрозуміла. Буду підтримувати людину згідно з інструкціями." } },
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

                this.logger.LogInformation("QuitCoach Gemini status={S}", resp.StatusCode);

                if (!resp.IsSuccessStatusCode)
                {
                    this.logger.LogError("QuitCoach Gemini error {S}: {B}",
                        resp.StatusCode, raw.Length > 400 ? raw[..400] : raw);
                    return null;
                }

                var text = this.ExtractText(raw);
                if (string.IsNullOrWhiteSpace(text))
                {
                    this.logger.LogWarning("QuitCoach Gemini: порожня відповідь");
                    return null;
                }

                return text;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "QuitCoach Gemini виняток");
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
                        this.logger.LogWarning("QuitCoach Gemini finishReason={R}", r);
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
                this.logger.LogError(ex, "QuitCoach ExtractText failed");
                return string.Empty;
            }
        }

        private QuitCoachResponse ParseResponse(string text, QuitCoachMode mode)
        {
            var message = string.Empty;
            var actions = new List<string>();
            var note = string.Empty;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();

                if (line.StartsWith("ВІДПОВІДЬ:", StringComparison.OrdinalIgnoreCase))
                    message = line[10..].Trim();
                else if (line.StartsWith("ДІЯ 1:", StringComparison.OrdinalIgnoreCase))
                    actions.Add(line[6..].Trim());
                else if (line.StartsWith("ДІЯ 2:", StringComparison.OrdinalIgnoreCase))
                    actions.Add(line[6..].Trim());
                else if (line.StartsWith("ДІЯ 3:", StringComparison.OrdinalIgnoreCase))
                    actions.Add(line[6..].Trim());
                else if (line.StartsWith("НОТАТКА:", StringComparison.OrdinalIgnoreCase))
                    note = line[8..].Trim();
            }

            if (string.IsNullOrWhiteSpace(message))
                message = text.Trim()[..Math.Min(500, text.Trim().Length)];

            if (!actions.Any())
                actions = this.DefaultActions(mode);

            if (string.IsNullOrWhiteSpace(note))
                note = this.DefaultNote(mode);

            return new QuitCoachResponse
            {
                Message = message,
                SuggestedActions = actions,
                MotivationalNote = note,
                Mode = mode,
            };
        }

        private QuitCoachResponse FallbackResponse(QuitCoachRequest request)
        {
            var analytics = request.Analytics;
            var progress = analytics != null && analytics.CleanDays > 0
                ? $" Ти вже маєш {analytics.CleanDays} чистих днів — це реальний прогрес."
                : string.Empty;
            var triggerTip = this.BuildTriggerTip(analytics);

            return request.Mode switch
            {
                QuitCoachMode.CravingSupport => new QuitCoachResponse
                {
                    Message = $"Зараз може бути дуже напружено, але потяг — це хвиля: він підіймається і спадає.{progress}",
                    SuggestedActions = new List<string>
                    {
                        "Відійди від тригера хоча б на 2 метри або зміни кімнату",
                        request.CurrentIntensity >= 7
                            ? "Постав таймер на 10 хвилин і дихай повільно: 4 секунди вдих, 6 секунд видих"
                            : "Випий води і зроби 10 повільних видихів",
                        triggerTip,
                    },
                    MotivationalNote = "Тобі не треба перемагати весь день одразу — тільки наступні 10 хвилин.",
                    Mode = QuitCoachMode.CravingSupport,
                },
                QuitCoachMode.AfterRelapse => new QuitCoachResponse
                {
                    Message = "Зрив не визначає тебе і не стирає весь попередній шлях. Важливо, що ти вже повернулась до підтримки.",
                    SuggestedActions = new List<string>
                    {
                        "Запиши тільки факти: час, місце, тригер, без самокритики",
                        "Прибери або віддали те, що може запустити продовження зриву",
                        "Обери один маленький крок повернення на сьогодні: вода, душ, прогулянка або повідомлення близькій людині",
                    },
                    MotivationalNote = "Повернення після зриву — це частина навички, яку ти зараз тренуєш.",
                    Mode = QuitCoachMode.AfterRelapse,
                },
                _ => new QuitCoachResponse
                {
                    Message = "Профілактика працює найкраще, коли план дуже простий і його легко виконати втомленою людиною.",
                    SuggestedActions = new List<string>
                    {
                        "Визнач один ранній сигнал потягу: думка, місце, час або емоція",
                        triggerTip,
                        "Склади правило: якщо з'являється потяг, то я одразу роблю одну заміну протягом 5 хвилин",
                    },
                    MotivationalNote = "Добрий план не вимагає сили волі на максимумі — він підхоплює тебе раніше.",
                    Mode = QuitCoachMode.Prevention,
                },
            };
        }

        private List<string> DefaultActions(QuitCoachMode mode) => mode switch
        {
            QuitCoachMode.AfterRelapse => new List<string>
            {
                "Назви факт без оцінки: що сталося, де і коли",
                "Прибери найближчий тригер або відійди від нього",
                "Зроби один маленький крок повернення протягом 5 хвилин",
            },
            QuitCoachMode.Prevention => new List<string>
            {
                "Запиши один найчастіший тригер",
                "Підготуй просту заміну на 5 хвилин",
                "Домовся з собою про правило 'якщо-то'",
            },
            _ => new List<string>
            {
                "Зміни місце або відійди від тригера",
                "Постав таймер на 10 хвилин",
                "Зроби 10 повільних видихів або випий води",
            },
        };

        private string DefaultNote(QuitCoachMode mode) => mode switch
        {
            QuitCoachMode.AfterRelapse =>
                "Один епізод не визначає тебе — важить наступний крок.",
            QuitCoachMode.Prevention =>
                "План має бути простим настільки, щоб спрацювати у важкий день.",
            _ =>
                "Потяг не триває вічно; зараз твоє завдання — перечекати хвилю.",
        };

        private string BuildTriggerTip(QuitAnalyticsViewModel? analytics)
        {
            var topTrigger = analytics?.MostDangerousTriggers.FirstOrDefault();
            if (topTrigger != null)
            {
                return $"Для тригера '{topTrigger.TriggerName}' підготуй заміну: вода, 5 хвилин ходьби або повідомлення людині підтримки";
            }

            if (!string.IsNullOrWhiteSpace(analytics?.MostDangerousTime)
                && analytics.MostDangerousTime != "Недостатньо даних")
            {
                return $"У період '{analytics.MostDangerousTime}' заздалегідь заплануй коротку дію-заміну";
            }

            return "Підготуй одну заміну, яку легко зробити одразу: вода, душ, коротка прогулянка або дихання";
        }
    }
}