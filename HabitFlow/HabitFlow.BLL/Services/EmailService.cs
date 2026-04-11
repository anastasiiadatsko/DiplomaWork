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
                Subject = "Підтвердження реєстрації — HabitFlow",
                Body = BuildEmailBody(userName, confirmationLink),
                IsBodyHtml = true,
            };

            mailMessage.To.Add(toEmail);

            // 🔥 Anti-spam headers
            mailMessage.Headers.Add("X-Priority", "1");
            mailMessage.Headers.Add("X-MSMail-Priority", "High");
            mailMessage.Headers.Add("Importance", "high");

            await smtpClient.SendMailAsync(mailMessage);

            logger.LogInformation("Email sent to {Email}", toEmail);
        }

        private string BuildEmailBody(string userName, string confirmationLink)
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

<!-- HEADER -->
<tr>
<td style='background:linear-gradient(135deg,#16a34a,#15803d); padding:30px; text-align:center;'>
<span style='color:#fff; font-size:24px; font-weight:bold;'>Habit<span style='opacity:0.7;'>Flow</span></span>
</td>
</tr>

<!-- BODY -->
<tr>
<td style='padding:30px;'>

<h2 style='margin:0 0 10px; color:#111;'>Привіт, {userName}! 👋</h2>

<p style='color:#555; line-height:1.6;'>
Дякуємо за реєстрацію в HabitFlow.<br/>
Підтвердь свою пошту, щоб почати працювати зі звичками.
</p>

<div style='text-align:center; margin:30px 0;'>
<a href='{confirmationLink}'
style='background:#16a34a; color:#fff; padding:14px 28px; border-radius:10px; text-decoration:none; font-weight:bold; display:inline-block;'>
Підтвердити email
</a>
</div>

<p style='font-size:12px; color:#999; text-align:center;'>
Якщо ти не реєструвався — просто проігноруй цей лист.
</p>

</td>
</tr>

<!-- FOOTER -->
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