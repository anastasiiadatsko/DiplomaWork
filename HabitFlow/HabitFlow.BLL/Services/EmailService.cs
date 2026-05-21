using System.Net;
using System.Net.Mail;
using HabitFlow.BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HabitFlow.BLL.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<EmailService> logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task SendConfirmationEmailAsync(
            string toEmail,
            string userName,
            string confirmationLink)
        {
            var subject = "Підтвердження реєстрації — HabitFlow";
            var body = BuildConfirmationEmailBody(userName, confirmationLink);

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(
            string toEmail,
            string userName,
            string resetLink)
        {
            var subject = "Відновлення паролю — HabitFlow";
            var body = BuildPasswordResetEmailBody(userName, resetLink);

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendHabitInvitationEmailAsync(
    string toEmail,
    string userName,
    string inviterName,
    string habitName,
    string acceptLink,
    string declineLink)
        {
            var subject = "Запрошення до спільної звички — HabitFlow";
            var body = BuildHabitInvitationEmailBody(
                userName,
                inviterName,
                habitName,
                acceptLink,
                declineLink);

            await SendEmailAsync(toEmail, subject, body);
        }

        // 🔥 ОДИН метод для відправки (best practice)
        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var from = configuration["Email:From"]!;
            var password = configuration["Email:Password"]!;
            var displayName = configuration["Email:DisplayName"]!;

            using var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(from, password),
                EnableSsl = true,
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(from, displayName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };

            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);

            logger.LogInformation("Email sent to {Email}", toEmail);
        }

        // ✅ ПІДТВЕРДЖЕННЯ
        private string BuildConfirmationEmailBody(string userName, string link)
        {
            return BuildTemplate(
                userName,
                "Підтверди свою пошту",
                "Дякуємо за реєстрацію в HabitFlow. Натисни кнопку нижче, щоб підтвердити email.",
                link,
                "Підтвердити email"
            );
        }

        // 🔐 RESET PASSWORD
        private string BuildPasswordResetEmailBody(string userName, string link)
        {
            return BuildTemplate(
                userName,
                "Відновлення паролю",
                "Ти запросив відновлення паролю. Натисни кнопку нижче, щоб створити новий пароль.",
                link,
                "Змінити пароль"
            );
        }

        private string BuildHabitInvitationEmailBody(
    string userName,
    string inviterName,
    string habitName,
    string acceptLink,
    string declineLink)
        {
            return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>HabitFlow</title>
</head>

<body style='margin:0; padding:0; background:#f3f4f6; font-family:Arial,sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 0;'>
<tr>
<td align='center'>

<table width='520' cellpadding='0' cellspacing='0' style='background:#ffffff; border-radius:16px; overflow:hidden;'>

<tr>
<td style='background:linear-gradient(135deg,#16a34a,#15803d); padding:30px; text-align:center;'>
<span style='color:#fff; font-size:24px; font-weight:bold;'>Habit<span style='opacity:0.7;'>Flow</span></span>
</td>
</tr>

<tr>
<td style='padding:30px;'>

<h2 style='margin:0 0 10px; color:#111;'>Привіт, {userName}! 👋</h2>

<h3 style='margin:0 0 15px; color:#16a34a;'>Запрошення до спільної звички</h3>

<p style='color:#555; line-height:1.6;'>
{inviterName} запрошує тебе разом проходити звичку:
</p>

<div style='background:#f0fdf4; border:1px solid #bbf7d0; border-radius:12px; padding:16px; margin:20px 0; text-align:center;'>
<strong style='font-size:18px; color:#15803d;'>{habitName}</strong>
</div>

<p style='color:#555; line-height:1.6;'>
Ти можеш прийняти або відхилити запрошення.
</p>

<div style='text-align:center; margin:30px 0;'>
<a href='{acceptLink}'
style='background:#16a34a; color:#fff; padding:14px 24px; border-radius:10px; text-decoration:none; font-weight:bold; display:inline-block; margin-right:8px;'>
Прийняти
</a>

<a href='{declineLink}'
style='background:#ef4444; color:#fff; padding:14px 24px; border-radius:10px; text-decoration:none; font-weight:bold; display:inline-block;'>
Відхилити
</a>
</div>

<p style='font-size:12px; color:#999; text-align:center;'>
Якщо ти не очікував/не очікувала це запрошення — просто проігноруй лист.
</p>

</td>
</tr>

<tr>
<td style='background:#f9fafb; text-align:center; padding:15px; font-size:12px; color:#999;'>
© 2026 HabitFlow
</td>
</tr>

</table>

</td>
</tr>
</table>

</body>
</html>";
        }

        // 🔥 ОДИН ШАБЛОН (reuse — як у топ проектах)
        private string BuildTemplate(
            string userName,
            string title,
            string text,
            string link,
            string buttonText)
        {
            return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>HabitFlow</title>
</head>

<body style='margin:0; padding:0; background:#f3f4f6; font-family:Arial,sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0' style='padding:40px 0;'>
<tr>
<td align='center'>

<table width='520' cellpadding='0' cellspacing='0' style='background:#ffffff; border-radius:16px; overflow:hidden;'>

<tr>
<td style='background:linear-gradient(135deg,#16a34a,#15803d); padding:30px; text-align:center;'>
<span style='color:#fff; font-size:24px; font-weight:bold;'>Habit<span style='opacity:0.7;'>Flow</span></span>
</td>
</tr>

<tr>
<td style='padding:30px;'>

<h2 style='margin:0 0 10px; color:#111;'>Привіт, {userName}! 👋</h2>

<h3 style='margin:0 0 15px; color:#16a34a;'>{title}</h3>

<p style='color:#555; line-height:1.6;'>{text}</p>

<div style='text-align:center; margin:30px 0;'>
<a href='{link}'
style='background:#16a34a; color:#fff; padding:14px 28px; border-radius:10px; text-decoration:none; font-weight:bold; display:inline-block;'>
{buttonText}
</a>
</div>

<p style='font-size:12px; color:#999; text-align:center;'>
Якщо це були не ви — просто проігноруйте цей лист.
</p>

</td>
</tr>

<tr>
<td style='background:#f9fafb; text-align:center; padding:15px; font-size:12px; color:#999;'>
© 2026 HabitFlow
</td>
</tr>

</table>

</td>
</tr>
</table>

</body>
</html>";
        }
    }
}