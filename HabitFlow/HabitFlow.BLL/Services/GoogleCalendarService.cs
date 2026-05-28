using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HabitFlow.BLL.Interfaces;
using HabitFlow.Domain.Enums;
using HabitFlow.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace HabitFlow.BLL.Services
{
    public class GoogleCalendarService : IGoogleCalendarService
    {
        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string CalendarEventsEndpoint = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

        private readonly IConfiguration configuration;
        private readonly IUserRepository userRepository;
        private readonly HttpClient httpClient;

        public GoogleCalendarService(
            IConfiguration configuration,
            IUserRepository userRepository,
            HttpClient httpClient)
        {
            this.configuration = configuration;
            this.userRepository = userRepository;
            this.httpClient = httpClient;
        }

        public string BuildAuthorizationUrl(Guid userId)
        {
            var clientId = this.configuration["GoogleCalendar:ClientId"];
            var redirectUri = this.configuration["GoogleCalendar:RedirectUri"];

            var scope = "https://www.googleapis.com/auth/calendar.events";

            var query = new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = scope,
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["state"] = userId.ToString(),
            };

            var queryString = string.Join("&", query.Select(x =>
                $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value ?? string.Empty)}"));

            return $"{AuthorizationEndpoint}?{queryString}";
        }

        public async Task<bool> HandleCallbackAsync(Guid userId, string code)
        {
            var user = await this.userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            var clientId = this.configuration["GoogleCalendar:ClientId"];
            var clientSecret = this.configuration["GoogleCalendar:ClientSecret"];
            var redirectUri = this.configuration["GoogleCalendar:RedirectUri"];

            var form = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId ?? string.Empty,
                ["client_secret"] = clientSecret ?? string.Empty,
                ["redirect_uri"] = redirectUri ?? string.Empty,
                ["grant_type"] = "authorization_code",
            };

            var response = await this.httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var accessToken = root.GetProperty("access_token").GetString();

            string? refreshToken = null;
            if (root.TryGetProperty("refresh_token", out var refreshTokenElement))
            {
                refreshToken = refreshTokenElement.GetString();
            }

            var expiresIn = root.GetProperty("expires_in").GetInt32();

            user.GoogleCalendarAccessToken = accessToken;
            user.GoogleCalendarRefreshToken = refreshToken ?? user.GoogleCalendarRefreshToken;
            user.GoogleCalendarTokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60);
            user.IsGoogleCalendarConnected = true;

            await this.userRepository.UpdateAsync(user);

            return true;
        }

        public async Task<bool> DisconnectAsync(Guid userId)
        {
            var user = await this.userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.IsGoogleCalendarConnected = false;
            user.GoogleCalendarAccessToken = null;
            user.GoogleCalendarRefreshToken = null;
            user.GoogleCalendarTokenExpiresAt = null;

            await this.userRepository.UpdateAsync(user);

            return true;
        }

        public async Task<bool> CreateHabitReminderEventAsync(
    Guid userId,
    string habitName,
    DateTime date,
    TimeOnly reminderTime,
    FrequencyType frequencyType,
    List<DayOfWeek> targetDays)
        {
            var user = await this.userRepository.GetByIdAsync(userId);
            if (user == null || !user.IsGoogleCalendarConnected)
            {
                return false;
            }

            var accessToken = await this.GetValidAccessTokenAsync(user);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return false;
            }

            var startDateTime = date.Date
                .AddHours(reminderTime.Hour)
                .AddMinutes(reminderTime.Minute);

            var endDateTime = startDateTime.AddMinutes(15);
            var recurrence = this.BuildRecurrence(frequencyType, targetDays);

            var calendarEvent = new
            {
                summary = $"HabitFlow: {habitName}",
                description = "Нагадування виконати звичку у HabitFlow.",
                start = new
                {
                    dateTime = startDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "Europe/Kyiv",
                },
                end = new
                {
                    dateTime = endDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "Europe/Kyiv",
                },
                recurrence = recurrence,
                reminders = new
                {
                    useDefault = false,
                    overrides = new[]
                    {
            new
            {
                method = "popup",
                minutes = 10,
            },
        },
                },
            };

            var json = JsonSerializer.Serialize(calendarEvent);
            var request = new HttpRequestMessage(HttpMethod.Post, CalendarEventsEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await this.httpClient.SendAsync(request);

            return response.IsSuccessStatusCode;
        }

        public string BuildGoogleCalendarTemplateUrl(
    string habitName,
    string? description,
    DateTime date,
    TimeOnly reminderTime,
    FrequencyType frequencyType,
    List<DayOfWeek> targetDays)
        {
            var startDateTime = date.Date
                .AddHours(reminderTime.Hour)
                .AddMinutes(reminderTime.Minute);

            var endDateTime = startDateTime.AddMinutes(15);

            var recurrence = this.BuildRecurrence(frequencyType, targetDays)
                .FirstOrDefault();

            var query = new Dictionary<string, string?>
            {
                ["action"] = "TEMPLATE",
                ["text"] = $"HabitFlow: {habitName}",
                ["details"] = string.IsNullOrWhiteSpace(description)
                    ? "Нагадування виконати звичку у HabitFlow."
                    : description,
                ["dates"] = $"{startDateTime:yyyyMMddTHHmmss}/{endDateTime:yyyyMMddTHHmmss}",
                ["ctz"] = "Europe/Kyiv",
            };

            if (!string.IsNullOrWhiteSpace(recurrence))
            {
                query["recur"] = recurrence;
            }

            var queryString = string.Join("&", query.Select(x =>
                $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value ?? string.Empty)}"));

            return $"https://calendar.google.com/calendar/render?{queryString}";
        }

        private string[] BuildRecurrence(
    FrequencyType frequencyType,
    List<DayOfWeek> targetDays)
        {
            if (frequencyType == FrequencyType.Daily)
            {
                return new[] { "RRULE:FREQ=DAILY" };
            }

            if (frequencyType == FrequencyType.Weekly)
            {
                return new[] { "RRULE:FREQ=WEEKLY" };
            }

            if (frequencyType == FrequencyType.SpecificDays && targetDays.Any())
            {
                var days = string.Join(",", targetDays.Select(this.ToGoogleCalendarDay));
                return new[] { $"RRULE:FREQ=WEEKLY;BYDAY={days}" };
            }

            return Array.Empty<string>();
        }

        private string ToGoogleCalendarDay(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "MO",
                DayOfWeek.Tuesday => "TU",
                DayOfWeek.Wednesday => "WE",
                DayOfWeek.Thursday => "TH",
                DayOfWeek.Friday => "FR",
                DayOfWeek.Saturday => "SA",
                DayOfWeek.Sunday => "SU",
                _ => "MO",
            };
        }

        private async Task<string?> GetValidAccessTokenAsync(HabitFlow.Domain.Entities.User user)
        {
            if (!string.IsNullOrWhiteSpace(user.GoogleCalendarAccessToken) &&
                user.GoogleCalendarTokenExpiresAt.HasValue &&
                user.GoogleCalendarTokenExpiresAt.Value > DateTime.UtcNow)
            {
                return user.GoogleCalendarAccessToken;
            }

            if (string.IsNullOrWhiteSpace(user.GoogleCalendarRefreshToken))
            {
                return null;
            }

            var clientId = this.configuration["GoogleCalendar:ClientId"];
            var clientSecret = this.configuration["GoogleCalendar:ClientSecret"];

            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId ?? string.Empty,
                ["client_secret"] = clientSecret ?? string.Empty,
                ["refresh_token"] = user.GoogleCalendarRefreshToken,
                ["grant_type"] = "refresh_token",
            };

            var response = await this.httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();

            user.GoogleCalendarAccessToken = accessToken;
            user.GoogleCalendarTokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60);

            await this.userRepository.UpdateAsync(user);

            return accessToken;
        }
    }
}