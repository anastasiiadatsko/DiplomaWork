namespace HabitFlow.BLL.Interfaces
{
    public interface IEmailService
    {
        Task SendConfirmationEmailAsync(
            string toEmail,
            string userName,
            string confirmationLink);

        Task SendPasswordResetEmailAsync(
            string toEmail,
            string userName,
            string resetLink);
    }
}