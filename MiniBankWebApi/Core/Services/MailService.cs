using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MiniBankWebApi.Core.Services
{
    public class MailService : IMailService
    {
        private readonly IConfiguration config;
        private readonly ILogger<MailService> logger;

        public MailService(IConfiguration config, ILogger<MailService> logger)
        {
            this.config = config;
            this.logger = logger;
        }

        public async Task SendOtpAsync(string toEmail, string otpCode, int expireMinutes)
        {
            var isEnabled = config.GetValue<bool>("MailSettings:Enabled");
            if (!isEnabled)
            {
                logger.LogWarning("MailSettings is disabled. OTP for {Email} is {OtpCode}", toEmail, otpCode);
                return;
            }

            var senderMail = config["MailSettings:Mail"]!;
            var senderName = config["MailSettings:DisplayName"]!;
            var password = config["MailSettings:Password"]!;
            var host = config["MailSettings:Host"]!;
            var port = config.GetValue<int>("MailSettings:Port");

            var message = new MimeMessage();
            message.Sender = new MailboxAddress(senderName, senderMail);
            message.From.Add(new MailboxAddress(senderName, senderMail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "MiniBank - OTP verification";
            message.Body = new BodyBuilder
            {
                HtmlBody = $"<p>Your OTP code is <b>{otpCode}</b>.</p>" +
                           $"<p>This code expires in {expireMinutes} minutes.</p>"
            }.ToMessageBody();

            try
            {
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(senderMail, password);
                await smtp.SendAsync(message);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cannot send OTP mail to {Email}", toEmail);
                throw new InvalidOperationException("Cannot send OTP mail, please try again");
            }
        }
    }
}
