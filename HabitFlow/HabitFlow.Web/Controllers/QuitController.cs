using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Entities;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Controllers
{
    public class QuitController : Controller
    {
        private readonly IQuitAnalyticsService quitAnalyticsService;
        private readonly IQuitCoachService quitCoachService;
        private readonly ITriggerLogRepository triggerLogRepository;
        private readonly IHabitRepository habitRepository;
        private readonly IConfiguration configuration;
        private readonly ILogger<QuitController> logger;

        public QuitController(
            IQuitAnalyticsService quitAnalyticsService,
            IQuitCoachService quitCoachService,
            ITriggerLogRepository triggerLogRepository,
            IHabitRepository habitRepository,
            IConfiguration configuration,
            ILogger<QuitController> logger)
        {
            this.quitAnalyticsService = quitAnalyticsService;
            this.quitCoachService = quitCoachService;
            this.triggerLogRepository = triggerLogRepository;
            this.habitRepository = habitRepository;
            this.configuration = configuration;
            this.logger = logger;
        }

        private Guid? CurrentUserId
        {
            get
            {
                var value = this.HttpContext.Session.GetString("UserId");
                return Guid.TryParse(value, out var id) ? id : null;
            }
        }

        public async Task<IActionResult> Index()
        {
            var userId = this.CurrentUserId;
            if (userId == null)
                return this.RedirectToAction("Login", "Auth");

            var vm = await this.quitAnalyticsService.GetAnalyticsAsync(userId.Value);
            return this.View(vm);
        }

        public async Task<IActionResult> Crisis()
        {
            var userId = this.CurrentUserId;
            if (userId == null)
                return this.RedirectToAction("Login", "Auth");

            var analytics = await this.quitAnalyticsService.GetAnalyticsAsync(userId.Value);
            this.ViewBag.Analytics = analytics;

            return this.View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogTrigger(
            int intensity,
            TriggerType triggerType,
            bool didRelapse,
            string? note,
            string? location)
        {
            var userId = this.CurrentUserId;
            if (userId == null)
                return this.RedirectToAction("Login", "Auth");

            var habit = (await this.habitRepository.GetByUserIdAsync(userId.Value))
                .FirstOrDefault(h => h.Mode == HabitMode.Quit && h.IsActive);

            if (habit == null)
                return this.RedirectToAction("Index", "Habit");

            var log = new TriggerLog
            {
                Id = Guid.NewGuid(),
                HabitId = habit.Id,
                UserId = userId.Value,
                CravingLevel = Math.Clamp(intensity, 1, 10),
                TriggerType = triggerType,
                DidRelapse = didRelapse,
                Resisted = !didRelapse,
                Note = note,
                Location = location,
                OccurredAt = DateTime.UtcNow,
            };

            await this.triggerLogRepository.AddAsync(log);

            if (didRelapse)
                return this.RedirectToAction(nameof(this.Crisis), new { mode = "afterRelapse" });

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Coach([FromBody] QuitCoachRequest? request)
        {
            var userId = this.CurrentUserId;
            if (userId == null)
                return this.Unauthorized();

            request ??= new QuitCoachRequest
            {
                Mode = QuitCoachMode.CravingSupport,
                History = new List<QuitCoachMessage>(),
            };

            request.History ??= new List<QuitCoachMessage>();
            request.Analytics = await this.quitAnalyticsService.GetAnalyticsAsync(userId.Value);

            var response = await this.quitCoachService.GetResponseAsync(userId.Value, request);
            return this.Json(response);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLog(Guid id)
        {
            var userId = this.CurrentUserId;
            if (userId == null)
                return this.RedirectToAction("Login", "Auth");

            var log = await this.triggerLogRepository.GetByIdAsync(id);
            if (log == null || log.UserId != userId.Value)
                return this.NotFound();

            await this.triggerLogRepository.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("Quit/VoiceStream")]
        public async Task VoiceStream()
        {
            this.logger.LogInformation("QuitVoiceStream: запит. IsWebSocket={IsWs}",
                HttpContext.WebSockets.IsWebSocketRequest);

            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                HttpContext.Response.StatusCode = 400;
                return;
            }

            var userId = this.CurrentUserId;
            if (userId == null)
            {
                var qs = HttpContext.Request.Query["userId"].ToString();
                this.logger.LogInformation("QuitVoiceStream: userId з query={U}", qs);
                if (Guid.TryParse(qs, out var parsed))
                    userId = parsed;
            }

            if (userId == null)
            {
                this.logger.LogWarning("QuitVoiceStream: userId не знайдено → 401");
                HttpContext.Response.StatusCode = 401;
                return;
            }

            this.logger.LogInformation("QuitVoiceStream: userId={U}", userId);

            var browserWs = await HttpContext.WebSockets.AcceptWebSocketAsync();
            this.logger.LogInformation("QuitVoiceStream: browserWs прийнято");

            var apiKey = this.configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                this.logger.LogError("QuitVoiceStream: ApiKey відсутній!");
                await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                    "API key missing", CancellationToken.None);
                return;
            }

            var analytics = await this.quitAnalyticsService.GetAnalyticsAsync(userId.Value);
            var systemPrompt = this.BuildQuitVoiceSystemPrompt(analytics);

            var voiceModel = this.configuration["Gemini:VoiceModel"]
                ?? "models/gemini-2.5-flash-native-audio-latest";

            this.logger.LogInformation("QuitVoiceStream: voiceModel={M}", voiceModel);

            var geminiUri = new Uri(
                "wss://generativelanguage.googleapis.com/ws/" +
                "google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent" +
                $"?key={apiKey}");

            using var geminiWs = new ClientWebSocket();
            try
            {
                await geminiWs.ConnectAsync(geminiUri, CancellationToken.None);
                this.logger.LogInformation("QuitVoiceStream: Gemini WS відкрито. Стан={S}", geminiWs.State);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "QuitVoiceStream: помилка підключення до Gemini");
                await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                    "Gemini connect failed", CancellationToken.None);
                return;
            }

            var setup = JsonSerializer.Serialize(new
            {
                setup = new
                {
                    model = voiceModel,
                    generation_config = new
                    {
                        response_modalities = new[] { "AUDIO" },
                        speech_config = new
                        {
                            voice_config = new
                            {
                                prebuilt_voice_config = new { voice_name = "Aoede" }
                            }
                        }
                    },
                    output_audio_transcription = new { },
                    input_audio_transcription = new { },
                    system_instruction = new
                    {
                        parts = new[] { new { text = systemPrompt } }
                    }
                }
            });

            try
            {
                await geminiWs.SendAsync(Encoding.UTF8.GetBytes(setup),
                    WebSocketMessageType.Text, true, CancellationToken.None);
                this.logger.LogInformation("QuitVoiceStream: setup надіслано");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "QuitVoiceStream: помилка надсилання setup");
                await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                    "Setup send failed", CancellationToken.None);
                return;
            }

            try
            {
                var fullMessage = await this.ReceiveFullMessageAsync(geminiWs, CancellationToken.None,
                    TimeSpan.FromSeconds(15));

                if (fullMessage == null)
                {
                    this.logger.LogError("QuitVoiceStream: Gemini відхилив setup або таймаут.");
                    await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                        "Gemini rejected", CancellationToken.None);
                    return;
                }

                var preview = Encoding.UTF8.GetString(fullMessage);
                this.logger.LogInformation("QuitVoiceStream: setupComplete отримано, {N}б: '{P}'",
                    fullMessage.Length,
                    preview.Length > 200 ? preview[..200] : preview);

                if (browserWs.State == WebSocketState.Open)
                    await browserWs.SendAsync(fullMessage,
                        WebSocketMessageType.Text, true, CancellationToken.None);

                this.logger.LogInformation("QuitVoiceStream: setupComplete переслано браузеру, старт проксі");
            }
            catch (OperationCanceledException)
            {
                this.logger.LogError("QuitVoiceStream: таймаут setupComplete");
                await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                    "Setup timeout", CancellationToken.None);
                return;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "QuitVoiceStream: помилка читання setupComplete");
                await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                    "Setup read failed", CancellationToken.None);
                return;
            }

            var cts = new CancellationTokenSource();
            try
            {
                await Task.WhenAny(
                    this.ProxyBrowserToGemini(browserWs, geminiWs, cts.Token),
                    this.ProxyGeminiToBrowser(geminiWs, browserWs, cts.Token));
            }
            finally
            {
                cts.Cancel();
                this.logger.LogInformation("QuitVoiceStream: сесія завершена");
                if (browserWs.State == WebSocketState.Open)
                {
                    try
                    {
                        await browserWs.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "done", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "QuitVoiceStream: помилка закриття browserWs");
                    }
                }
            }
        }

        private string BuildQuitVoiceSystemPrompt(QuitAnalyticsViewModel a)
        {
            var sb = new StringBuilder();

            sb.AppendLine(
                "Ти — QuitCoach, голосовий AI-агент підтримки при кидані шкідливої звички у застосунку HabitFlow. " +
                "Завжди говори тільки українською мовою, на 'ти'. " +
                "Тон: теплий, спокійний, уважний — як надійний друг поруч у складний момент. " +
                "Без осуду, без пафосу, без моралізаторства. " +
                "Ти НЕ лікар і НЕ терапевт. Якщо є небезпека для здоров'я — м'яко направ до фахівця або близьких. " +
                "ЗАБОРОНЕНО казати: 'провал', 'слабкість', 'ти знову', 'просто не роби', 'візьми себе в руки'. " +
                "Якщо людина переживає потяг — дій швидко: короткі речення, заземлення, план на 10-15 хвилин. " +
                "Якщо стався зрив — не драматизуй: відділи зрив від особистості, запропонуй один крок повернення. " +
                "Якщо профілактика — допоможи знайти тригер і план 'якщо-то'.");

            sb.AppendLine();
            sb.AppendLine("[Дані користувача:]");
            sb.AppendLine($"Чистих днів: {a.CleanDays}");
            sb.AppendLine($"Зривів: {a.RelapseCount}");
            sb.AppendLine($"Пережито потягів без зриву: {a.WonCravingsCount} з {a.TotalCravingsCount}");
            sb.AppendLine($"Середня сила потягу: {a.AverageCravingIntensity}/10");
            sb.AppendLine($"Ризик зриву: {a.RelapseRisk}%");
            sb.AppendLine($"Найнебезпечніший час: {a.MostDangerousTime}");

            if (a.MostDangerousTriggers.Any())
            {
                var triggers = string.Join(", ", a.MostDangerousTriggers.Select(t =>
                    $"{t.TriggerName} ({t.RiskPercent}% зривів)"));
                sb.AppendLine($"Головні тригери: {triggers}");
            }

            if (!string.IsNullOrWhiteSpace(a.MainInsight))
                sb.AppendLine($"Інсайт системи: {a.MainInsight}");

            sb.AppendLine();
            sb.AppendLine(
                "Використовуй ці дані як контекст підтримки — не як вирок. " +
                "Якщо є чисті дні — згадай їх як реальний прогрес. " +
                "Починай розмову з теплого привітання і питання як людина почувається зараз.");

            return sb.ToString();
        }

        private async Task<byte[]?> ReceiveFullMessageAsync(
            WebSocket ws,
            CancellationToken ct,
            TimeSpan? timeout = null)
        {
            using var ms = new MemoryStream();
            var buf = new byte[65536];

            using var linkedCts = timeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : null;

            if (timeout.HasValue)
                linkedCts!.CancelAfter(timeout.Value);

            var token = linkedCts?.Token ?? ct;

            WebSocketReceiveResult r;
            do
            {
                r = await ws.ReceiveAsync(buf, token);
                if (r.MessageType == WebSocketMessageType.Close)
                    return null;
                ms.Write(buf, 0, r.Count);
            }
            while (!r.EndOfMessage);

            if (ws.State != WebSocketState.Open)
                return null;

            return ms.ToArray();
        }

        private async Task ProxyBrowserToGemini(
            WebSocket browser, WebSocket gemini, CancellationToken ct)
        {
            var n = 0;
            try
            {
                while (!ct.IsCancellationRequested
                    && browser.State == WebSocketState.Open
                    && gemini.State == WebSocketState.Open)
                {
                    var payload = await this.ReceiveFullMessageAsync(browser, ct);
                    if (payload == null)
                    {
                        this.logger.LogInformation("ProxyB→G: браузер закрив з'єднання");
                        if (gemini.State == WebSocketState.Open)
                            await gemini.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                "browser closed", CancellationToken.None);
                        break;
                    }

                    n++;
                    if (n <= 5 || n % 30 == 0)
                        this.logger.LogInformation("ProxyB→G: #{N}, {B}б", n, payload.Length);

                    if (gemini.State == WebSocketState.Open)
                        await gemini.SendAsync(payload,
                            WebSocketMessageType.Text, true, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex) { this.logger.LogWarning("ProxyB→G: {M}", ex.Message); }
            catch (Exception ex) { this.logger.LogError(ex, "ProxyB→G: помилка"); }
            this.logger.LogInformation("ProxyB→G: завершено, {N} повідомлень", n);
        }

        private async Task ProxyGeminiToBrowser(
            WebSocket gemini, WebSocket browser, CancellationToken ct)
        {
            var n = 0;
            try
            {
                while (!ct.IsCancellationRequested
                    && gemini.State == WebSocketState.Open
                    && browser.State == WebSocketState.Open)
                {
                    var payload = await this.ReceiveFullMessageAsync(gemini, ct);
                    if (payload == null)
                    {
                        this.logger.LogInformation("ProxyG→B: Gemini закрив з'єднання. CS={CS}, D='{D}'",
                            gemini.CloseStatus, gemini.CloseStatusDescription);
                        if (browser.State == WebSocketState.Open)
                            await browser.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                "gemini closed", CancellationToken.None);
                        break;
                    }

                    n++;
                    if (n <= 5 || n % 20 == 0)
                    {
                        var preview = Encoding.UTF8.GetString(payload, 0, Math.Min(payload.Length, 200));
                        this.logger.LogInformation("ProxyG→B: #{N}, {B}б, preview='{P}'",
                            n, payload.Length, preview);
                    }

                    if (browser.State == WebSocketState.Open)
                        await browser.SendAsync(payload,
                            WebSocketMessageType.Text, true, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex) { this.logger.LogWarning("ProxyG→B: {M}", ex.Message); }
            catch (Exception ex) { this.logger.LogError(ex, "ProxyG→B: помилка"); }
            this.logger.LogInformation("ProxyG→B: завершено, {N} повідомлень", n);
        }
    }
}