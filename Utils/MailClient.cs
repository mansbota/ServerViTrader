using System.Net;
using System.Net.Mail;

namespace ServerViTrader.Utils
{
    class MailClient
    {
        SmtpClient client;
        string smtpServer;
        int smtpPort;

        public MailClient(string server, int port)
        {
            smtpServer = server;
            smtpPort = port;
            client = new SmtpClient(smtpServer, smtpPort);
        }

        public void SendEmail(string targetAddress, string fromAddress, string pass, string body)
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(fromAddress, pass);
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.EnableSsl = true;

            client.Send(fromAddress, targetAddress,
                "Please click on the link to activate your account", body);
        }
    }
}
