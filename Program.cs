using System;

namespace ServerViTrader
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Server viTraderServer = new Server();

                while (true)
                {
                    string input;

                    do
                    {
                        Console.WriteLine("Enter S to start or E to edit server settings.");
                        input = Console.ReadLine();

                    } while (input.ToUpper() != "S" && input.ToUpper() != "E");

                    if (input.ToUpper() == "E")
                    {
                        Server.EditServerSettings();
                    }
                    else
                    {
                        viTraderServer.LaunchServer();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception caught: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
