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

        Task SendHabitInvitationEmailAsync(
    string toEmail,
    string userName,
    string inviterName,
    string habitName,
    string acceptLink,
    string declineLink);
    }
}