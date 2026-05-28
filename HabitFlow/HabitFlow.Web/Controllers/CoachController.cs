using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HabitFlow.BLL.DTOs;
using HabitFlow.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    [Route("Coach")]
    public class CoachController : Controller
    {
        private readonly ICoachService coachService;
        private readonly IHabitService habitService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CoachController> _logger;

        public CoachController(
            ICoachService coachService,
            IHabitService habitService,
            IConfiguration configuration,
            ILogger<CoachController> logger)
        {
            this.coachService = coachService;
            this.habitService = habitService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserIdValue();

            if (userId == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            var habits = await this.habitService.GetAllHabitsAsync(userId.Value);
            var firstHabit = habits.FirstOrDefault(h => h.IsActive) ?? habits.FirstOrDefault();

            if (firstHabit == null)
            {
                this.TempData["Error"] = "Щоб відкрити AI-агента, спочатку створи хоча б одну звичку.";
                return this.RedirectToAction("Create", "Habit");
            }

            return this.RedirectToAction("Session", "Coach", new { habitId = firstHabit.Id });
        }

        [HttpGet("Session/{habitId:guid}")]
        public async Task<IActionResult> Session(Guid habitId)
        {
            var userId = GetUserIdValue();
            if (userId == null) return RedirectToAction("Login", "Auth");
            var sessionType = await coachService.DetectSessionTypeAsync(habitId, userId.Value);
            var session = await coachService.GetSessionQuestionsAsync(habitId, userId.Value, sessionType);
            ViewBag.HabitId = habitId;
            return View("Session", session);
        }

        [HttpGet("Session/{habitId:guid}/{sessionType}")]
        public async Task<IActionResult> SessionByType(Guid habitId, CoachSessionType sessionType)
        {
            var userId = GetUserIdValue();
            if (userId == null) return RedirectToAction("Login", "Auth");
            var session = await coachService.GetSessionQuestionsAsync(habitId, userId.Value, sessionType);
            ViewBag.HabitId = habitId;
            return View("Session", session);
        }

        [HttpPost("Advice")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Advice([FromBody] CoachAdviceRequest request)
        {
            var userId = GetUserIdValue();
            if (userId == null) return Json(new { error = "Не авторизовано" });
            if (request?.HabitId == Guid.Empty) return BadRequest("HabitId is required");
            var result = await coachService.GetAdviceAsync(userId.Value, request!);
            return Json(result);
        }

        [HttpPost("Chat")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Chat([FromBody] CoachAdviceRequest request)
        {
            var userId = GetUserIdValue();
            if (userId == null) return Json(new { error = "Не авторизовано" });
            request.SessionType = CoachSessionType.FreeChat;
            var result = await coachService.GetAdviceAsync(userId.Value, request);
            return Json(result);
        }

        [HttpPost("Summary")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Summary([FromBody] CoachSummaryRequest request)
        {
            var userId = GetUserIdValue();
            if (userId == null) return Json(new { error = "Не авторизовано" });
            if (request?.HabitId == Guid.Empty) return BadRequest("HabitId is required");
            var result = await coachService.GetSessionSummaryAsync(userId.Value, request!);
            return Json(result);
        }

        [HttpGet("GetUserId")]
        public IActionResult GetUserId()
        {
            var userId = GetUserIdValue();
            if (userId == null) return Json(new { userId = (string?)null });
            return Json(new { userId = userId.Value.ToString() });
        }

        [HttpGet("VoiceStream/{habitId:guid}")]
        public async Task VoiceStream(Guid habitId)
        {
            _logger.LogInformation(
                "VoiceStream: запит. IsWebSocket={IsWs}, HabitId={HabitId}",
                HttpContext.WebSockets.IsWebSocketRequest, habitId);

            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                HttpContext.Response.StatusCode = 400;
                return;
            }

            var userId = GetUserIdValue();
            if (userId == null)
            {
                var qs = HttpContext.Request.Query["userId"].ToString();
                _logger.LogInformation("VoiceStream: userId з query={U}", qs);
                if (Guid.TryParse(qs, out var parsed)) userId = parsed;
            }

            if (userId == null)
            {
                _logger.LogWarning("VoiceStream: userId не знайдено → 401");
                HttpContext.Response.StatusCode = 401;
                return;
            }
            _logger.LogInformation("VoiceStream: userId={U}", userId);

            var browserWs = await HttpContext.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation("VoiceStream: browserWs прийнято");

            var apiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("VoiceStream: ApiKey відсутній!");
                await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                    "API key missing", CancellationToken.None);
                return;
            }

            var voiceModel = _configuration["Gemini:VoiceModel"]
                ?? "models/gemini-2.5-flash-native-audio-latest";
            _logger.LogInformation("VoiceStream: voiceModel={M}", voiceModel);

            var geminiUri = new Uri(
                "wss://generativelanguage.googleapis.com/ws/" +
                "google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent" +
                $"?key={apiKey}");

            using var geminiWs = new ClientWebSocket();
            try
            {
                await geminiWs.ConnectAsync(geminiUri, CancellationToken.None);
                _logger.LogInformation("VoiceStream: Gemini WS відкрито. Стан={S}", geminiWs.State);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VoiceStream: помилка підключення до Gemini");
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
                        parts = new[] { new {
                            text =
                                "Ти — персональний агент з формування звичок у застосунку HabitFlow. " +
                                "Завжди говори тільки українською мовою. " +
                                "Будь теплим, конкретним і підтримуючим. " +
                                "Допомагай аналізувати прогрес, долати перешкоди і будувати стійкі звички."
                        }}
                    }
                }
            });

            try
            {
                await geminiWs.SendAsync(Encoding.UTF8.GetBytes(setup),
                    WebSocketMessageType.Text, true, CancellationToken.None);
                _logger.LogInformation("VoiceStream: setup надіслано");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VoiceStream: помилка надсилання setup");
                await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                    "Setup send failed", CancellationToken.None);
                return;
            }

            try
            {
                var fullMessage = await ReceiveFullMessageAsync(geminiWs, CancellationToken.None,
                    TimeSpan.FromSeconds(15));

                if (fullMessage == null)
                {
                    _logger.LogError("VoiceStream: Gemini відхилив setup або таймаут. " +
                        "State={S}, CS={CS}, Desc='{D}'",
                        geminiWs.State, geminiWs.CloseStatus, geminiWs.CloseStatusDescription);
                    await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                        "Gemini rejected", CancellationToken.None);
                    return;
                }

                var preview = Encoding.UTF8.GetString(fullMessage);
                _logger.LogInformation("VoiceStream: setupComplete отримано, {N}б: '{P}'",
                    fullMessage.Length,
                    preview.Length > 200 ? preview[..200] : preview);

                if (browserWs.State == WebSocketState.Open)
                    await browserWs.SendAsync(fullMessage,
                        WebSocketMessageType.Text, true, CancellationToken.None);

                _logger.LogInformation("VoiceStream: setupComplete переслано браузеру, старт проксі");
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("VoiceStream: таймаут setupComplete");
                await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                    "Setup timeout", CancellationToken.None);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VoiceStream: помилка читання setupComplete");
                await browserWs.CloseAsync(WebSocketCloseStatus.InternalServerError,
                    "Setup read failed", CancellationToken.None);
                return;
            }

            var cts = new CancellationTokenSource();
            try
            {
                await Task.WhenAny(
                    ProxyBrowserToGemini(browserWs, geminiWs, cts.Token),
                    ProxyGeminiToBrowser(geminiWs, browserWs, cts.Token));
            }
            finally
            {
                cts.Cancel();
                _logger.LogInformation("VoiceStream: сесія завершена");
                if (browserWs.State == WebSocketState.Open)
                {
                    try
                    {
                        await browserWs.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "done", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "VoiceStream: помилка закриття browserWs");
                    }
                }
            }
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
                    var payload = await ReceiveFullMessageAsync(browser, ct);
                    if (payload == null)
                    {
                        _logger.LogInformation("ProxyB→G: браузер закрив з'єднання");
                        if (gemini.State == WebSocketState.Open)
                            await gemini.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                "browser closed", CancellationToken.None);
                        break;
                    }

                    n++;
                    if (n <= 5 || n % 30 == 0)
                        _logger.LogInformation("ProxyB→G: #{N}, {B}б", n, payload.Length);

                    if (gemini.State == WebSocketState.Open)
                        await gemini.SendAsync(payload,
                            WebSocketMessageType.Text, true, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex) { _logger.LogWarning("ProxyB→G: {M}", ex.Message); }
            catch (Exception ex) { _logger.LogError(ex, "ProxyB→G: помилка"); }
            _logger.LogInformation("ProxyB→G: завершено, {N} повідомлень", n);
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
                    var payload = await ReceiveFullMessageAsync(gemini, ct);
                    if (payload == null)
                    {
                        _logger.LogInformation("ProxyG→B: Gemini закрив з'єднання. CS={CS}, D='{D}'",
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
                        _logger.LogInformation("ProxyG→B: #{N}, {B}б, preview='{P}'", n, payload.Length, preview);
                    }

                    if (browser.State == WebSocketState.Open)
                        await browser.SendAsync(payload,
                            WebSocketMessageType.Text, true, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex) { _logger.LogWarning("ProxyG→B: {M}", ex.Message); }
            catch (Exception ex) { _logger.LogError(ex, "ProxyG→B: помилка"); }
            _logger.LogInformation("ProxyG→B: завершено, {N} повідомлень", n);
        }

        private Guid? GetUserIdValue()
        {
            var value = HttpContext.Session.GetString("UserId");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}