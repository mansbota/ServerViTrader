using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using ServerViTrader.DTOs;
using ServerViTrader.Exceptions;
using System.Collections.Specialized;
using System.Web;
using ServerViTrader.Utils;

namespace ServerViTrader
{
    class Server
    {
        HttpListener httpListener;
        public static string httpEndPoint;
        
        public Server()
        {
            using var reg = new ServerRegistry();

            try
            {
                LoadServerSettings(reg);
            }
            catch (SystemException)
            {
                Console.WriteLine("Server settings missing.");

                EditServerSettings();
                LoadServerSettings(reg);
            }
        }

        private void LoadServerSettings(ServerRegistry reg)
        {
            httpEndPoint = "http://" + reg.GetAddress() + ":" + reg.GetPort() + "/";
        }

        public void LaunchServer()
        {
            Thread httpThread = new(LaunchHTTPServer);
        
            httpThread.Start();

            Console.WriteLine("Server running. \nPress enter to exit.");
            Console.ReadLine();

            TerminateServer(httpThread);
        }

        private void TerminateServer(Thread httpThread)
        {
            httpListener.Stop();

            httpThread.Join();
        }

        public static void EditServerSettings()
        {
            Console.Write("Enter server address: ");
            string address = Console.ReadLine();

            Console.Write("Enter server port: ");
            string port = Console.ReadLine();

            Console.Write("Enter server email: ");
            string email = Console.ReadLine();

            Console.Write("Enter server password: ");
            string password = Console.ReadLine();

            Console.Write("Enter AES password: ");
            string aesPassword = Console.ReadLine();
            AES.UpdateKey(aesPassword);

            using var reg = new ServerRegistry();
            reg.SetAddress(address);
            reg.SetPort(int.Parse(port));
            reg.SetEmail(email);
            reg.SetPassword(password);
        }

        private void LaunchHTTPServer()
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add(httpEndPoint);
            httpListener.Start();

            try
            {
                while (true)
                {
                    HttpListenerContext context = httpListener.GetContext();

                    Thread processHttpThread = new(() => ProcessHTTPRequest(context));
                    processHttpThread.Start();
                }
            }
            catch (HttpListenerException)
            {
                Console.WriteLine("Closed HTTP Server");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Terminated HTTP Server.\n" + ex.Message);
            }
        }

        static void ProcessHTTPRequest(HttpListenerContext context)
        {
            try
            {
                if (context.Request.RawUrl == "/")
                {
                    throw new BadRequestException("Empty request raw URL.");
                }

                string[] request = context.Request.RawUrl.Split(new char[] {'/', '?'})
                    .Where(s => !String.IsNullOrWhiteSpace(s)).ToArray();

                switch (context.Request.HttpMethod)
                {
                    case "GET":
                        HandleGETRequest(context, request);
                        break;
                    case "PUT":
                        HandlePUTRequest(context, request);
                        break;
                    case "POST":
                        HandlePOSTRequest(context, request);
                        break;
                    case "DELETE":
                        HandleDELETERequest(context, request);
                        break;
                    default:
                        throw new Exceptions.NotImplementedException(
                            "Unsupported HTTP method.");
                }
            }
            catch (HttpException ex)
            {
                Respond(context, ex.StatusCode, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception caught: {ex.Message} \n {ex.StackTrace}");

                Respond(context, HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        #region REQUEST_HANDLERS

        static void HandleGETRequest(HttpListenerContext context, string[] request)
        {
            string resource = request[0];

            switch (resource)
            {
                case "cryptos":
                    GetCryptos(context, request);
                    break;
                case "positions":
                    GetPositions(context, request);
                    break;
                case "trades":
                    GetTrades(context, request);
                    break;
                case "strategies":
                    GetStrategies(context, request);
                    break;
                case "statuses":
                    GetStatuses(context, request);
                    break;
                case "indicators":
                    GetIndicators(context, request);
                    break;
                case "trigger-types":
                    GetTriggerTypes(context, request);
                    break;
                case "trade-types":
                    GetTradeTypes(context, request);
                    break;
                case "validate":
                    ValidateUser(context);
                    break;
                default:
                    throw new BadRequestException("Invalid resource.");
            }
        }

        static void HandlePOSTRequest(HttpListenerContext context, string[] request)
        {
            string resource = request[0];

            switch (resource)
            {
                case "login":
                    Login(context, request);
                    break;
                case "user":
                    CreateUser(context, request);
                    break;
                case "trade":
                    CreateTrade(context, request);
                    break;
                case "strategy":
                    CreateStrategy(context, request);
                    break;
                default:
                    throw new BadRequestException("Invalid resource.");
            }
        }

        static void HandlePUTRequest(HttpListenerContext context, string[] request)
        {
            string resource = request[0];

            switch (resource)
            {
                case "user":
                    UpdateUser(context, request);
                    break;
                case "position":
                    UpdatePosition(context, request);
                    break;
                case "strategy":
                    UpdateStrategy(context, request);
                    break;
                default:
                    throw new BadRequestException("Invalid resource.");
            }
        }

        static void HandleDELETERequest(HttpListenerContext context, string[] request)
        {
            string resource = request[0];

            switch (resource)
            {
                case "user":
                    DeleteUser(context, request);
                    break;
                case "position":
                    DeletePosition(context, request);
                    break;
                case "trade":
                    DeleteTrade(context, request);
                    break;
                case "strategy":
                    DeleteStrategy(context, request);
                    break;
                default:
                    throw new BadRequestException("Invalid resource.");
            }
        }

        #endregion

        //login
        static void Login(HttpListenerContext context, string[] request)
        {
            if (request.Length != 1)
                throw new BadRequestException("Invalid URL.");

            string username = Authenticate(context);
            Respond(context, HttpStatusCode.OK, Serialize(Database.GetUserPublic(username)));
        }

        //validate/encrypted-username
        static void ValidateUser(HttpListenerContext context)
        {
            string encUsername = context.Request.RawUrl[10..];

            Database.ValidateUser(encUsername);
            Respond(context, HttpStatusCode.OK, "User validated");
        }

        #region POSITION

        //positions?userId=1
        public static void GetPositions(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                Respond(context, HttpStatusCode.OK, Serialize(Database.GetPositions(userId)));
            }
        }

        //<position>
        //  <amount>12.3</amount>
        //</position>
        //position?id=1&userId=1
        public static void UpdatePosition(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                PositionDTO position = Deserialize<PositionDTO>(context.Request.InputStream);
                position = Database.UpdatePosition(GetParsedArgument("id", collection), userId, position);

                Respond(context, HttpStatusCode.OK, Serialize(position));
            }
        }

        //position?id=1&userId=1
        public static void DeletePosition(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                Database.DeletePosition(GetParsedArgument("id", collection), userId);

                Respond(context, HttpStatusCode.NoContent);
            }
        }

        #endregion

        #region TRADE

        //trades?userId=1
        public static void GetTrades(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                Respond(context, HttpStatusCode.OK, Serialize(Database.GetTrades(userId)));
            }
        }

        //<trade>
        //  <cryptoId>2</cryptoId>
        //  <amount>20</amount>
        //  <tradeTypeId>1</tradeTypeId>
        //</trade>
        //trade?userId=1
        public static void CreateTrade(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                TradeDTO trade = Deserialize<TradeDTO>(context.Request.InputStream);

                Respond(context, HttpStatusCode.Created, Serialize(Database.CreateTrade(userId, trade)));
            }
        }

        //trade?id=1&userId=1
        public static void DeleteTrade(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                Database.DeleteTrade(GetParsedArgument("id", collection), userId);

                Respond(context, HttpStatusCode.NoContent);
            }
        }

        //trade-types
        public static void GetTradeTypes(HttpListenerContext context, string[] request)
        {
            if (request.Length != 1)
                throw new BadRequestException("Invalid URL.");

            Respond(context, HttpStatusCode.OK, Serialize(Database.GetTradeTypes()));
        }

        #endregion

        #region USER

        //<user>
        //  <username>fgrgic</username>
        //  <password>fgrgic123</password>
        //  <email>fgrgic@gmail.com</email>
        //</user>
        //user
        public static void CreateUser(HttpListenerContext context, string[] request)
        {
            if (request.Length != 1)
                throw new BadRequestException("Invalid URL.");

            UserDTO userDto = Deserialize<UserDTO>(context.Request.InputStream);
            Respond(context, HttpStatusCode.Created, Serialize(Database.CreateUser(userDto)));
        }

        //<user>
        //  <username>fgrgic</username>
        //  <password>fgrgic123</password>
        //  <email>fgrgic@gmail.com</email>
        //</user>
        //user?id=1
        public static void UpdateUser(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("id", collection);

            if (IsAuthorized(userId, username))
            {
                UserDTO user = Deserialize<UserDTO>(context.Request.InputStream);

                Respond(context, HttpStatusCode.OK, Serialize(Database.UpdateUser(userId, user)));
            }
        }

        //user?id=1
        public static void DeleteUser(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("id", collection);

            if (IsAuthorized(userId, username))
            {
                Database.DeleteUser(userId);

                Respond(context, HttpStatusCode.NoContent);
            }
        }

        //statuses
        public static void GetStatuses(HttpListenerContext context, string[] request)
        {
            if (request.Length != 1)
                throw new BadRequestException("Invalid URL.");

            Respond(context, HttpStatusCode.OK, Serialize(Database.GetStatuses()));
        }

        #endregion

        #region CRYPTO

        //cryptos
        public static void GetCryptos(HttpListenerContext context, string[] request)
        {
            if (request.Length != 1)
                throw new BadRequestException("Invalid URL.");

            Respond(context, HttpStatusCode.OK, Serialize(Database.GetCryptos()));
        }

        #endregion

        #region STRATEGY

        //strategies?userId=1
        public static void GetStrategies(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                Respond(context, HttpStatusCode.OK, Serialize(Database.GetStrategies(userId)));
            }
        }

        //<strategy>
        //  <name>rsi_strat></name>
        //  <triggers>
            //  <trigger>
            //      <indicatorId>1</indicatorId>
            //      <indicatorValue>30</indicatorValue>
            //      <triggerTypeId>1</triggerTypeId>
            //  </trigger>
        //  </triggers>
        //</strategy>
        //strategy?userId=1
        public static void CreateStrategy(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                StrategyDTO strategy = Deserialize<StrategyDTO>(context.Request.InputStream);

                Respond(context, HttpStatusCode.Created, Serialize(Database.CreateStrategy(userId, strategy)));
            }
        }

        //<strategy>
        //  <name>rsi_strat></name>
        //  <triggers>
        //  <trigger>
        //      <indicatorId>1</indicatorId>
        //      <indicatorValue>30</indicatorValue>
        //      <triggerTypeId>1</triggerTypeId>
        //  </trigger>
        //  </triggers>
        //</strategy>
        //strategy?id=1&userId=1
        public static void UpdateStrategy(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                StrategyDTO strategy = Deserialize<StrategyDTO>(context.Request.InputStream);

                Respond(context, HttpStatusCode.OK, Serialize(Database.UpdateStrategy(GetParsedArgument("id", collection), userId, strategy)));
            }
        }

        //strategy?id=1&userId=1
        public static void DeleteStrategy(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
                throw new BadRequestException("Invalid URL.");

            NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

            string username = Authenticate(context);
            int userId = GetParsedArgument("userId", collection);

            if (IsAuthorized(userId, username))
            {
                Database.DeleteStrategy(GetParsedArgument("id", collection), userId);

                Respond(context, HttpStatusCode.NoContent);
            }
        }

        //indicators
        public static void GetIndicators(HttpListenerContext context, string[] request)
        {
            if (request.Length != 1)
                throw new BadRequestException("Invalid URL.");

            Respond(context, HttpStatusCode.OK, Serialize(Database.GetIndicators()));
        }

        //trigger-types
        public static void GetTriggerTypes(HttpListenerContext context, string[] request)
        {
            if (request.Length != 1)
                throw new BadRequestException("Invalid URL.");

            Respond(context, HttpStatusCode.OK, Serialize(Database.GetTriggerTypes()));
        }

        #endregion

        #region UTILS

        private static int GetParsedArgument(string argument, NameValueCollection collection)
        {
            int userId;

            try
            {
                userId = int.Parse(collection.Get(argument));
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is FormatException || ex is OverflowException)
            {
                throw new BadRequestException("Incorrect URL format.");
            }

            return userId;
        }

        public static (string, string) GetCredentials(HttpListenerContext context)
        {
            string header = context.Request.Headers["Authorization"];

            if (header != null && header.StartsWith("Basic"))
            {
                string encodedCredentials = header["Basic ".Length..].Trim();
                string credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));

                int separatorIndex = credentials.IndexOf(':');

                return (credentials.Substring(0, separatorIndex), credentials[(separatorIndex + 1)..]);
            }
            else
            {
                throw new BadRequestException("Authorization header format incorrect.");
            }
        }

        public static string Authenticate(HttpListenerContext context)
        {
            (string username, string password) = GetCredentials(context);
            Database.VerifyUserAuthentication(username, password);

            return username;
        }

        public static bool IsAuthorized(int userId, string authUsername)
        {
            UserDTO user = Database.GetUser(authUsername);
            if (userId != user.id)
            {
                throw new UnauthorizedException("Unauthorized.");
            }

            return true;
        }

        public static void Respond(HttpListenerContext context, HttpStatusCode status, string msg = "")
        {
            int byteLen = Encoding.UTF8.GetByteCount(msg);

            context.Response.StatusCode = (int)status;
            context.Response.ContentLength64 = byteLen;

            using (Stream stream = context.Response.OutputStream)
            {
                stream.Write(Encoding.UTF8.GetBytes(msg), 0, byteLen);
            }
        }

        public static string Serialize<T>(T obj)
        {
            XmlSerializer serializer = new(typeof(T));
            
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, obj);
                return writer.ToString();
            }
        }

        public static T Deserialize<T>(Stream stream) where T : IValid
        {
            XmlSerializer serializer = new(typeof(T));
            T obj = (T)serializer.Deserialize(stream);

            obj.Validate();
            return obj;
        }

        #endregion
    }
}