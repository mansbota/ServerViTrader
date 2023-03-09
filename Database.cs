using System;
using System.Text;
using MySql.Data.MySqlClient;
using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using ServerViTrader.Exceptions;
using ServerViTrader.DTOs;
using WebClient;
using ServerViTrader.Utils;
using System.Net;

namespace ServerViTrader
{
    static class Database
    {
        static readonly string connectionString;
        private const string usdCoinName = "Tether";

        static Database()
        {
            string fileName = "config.json";
            string jsonString = File.ReadAllText(fileName);

            using (JsonDocument doc = JsonDocument.Parse(jsonString))
            {
                JsonElement root = doc.RootElement;
                connectionString = root.GetProperty("ConnectionString").GetString();
            }
        }

        #region UTILS

        private static MySqlConnection Connect() 
        {
            var conn = new MySqlConnection(connectionString);
            conn.Open();
            return conn;
        }

        private static void Close(MySqlConnection conn)
        {
            conn.Close();
            conn.Dispose();
        }

        private static string GenerateSalt()
        {
            var salt = new byte[32];
            using (var random = new RNGCryptoServiceProvider())
            {
                random.GetNonZeroBytes(salt);
            }

            return Convert.ToBase64String(salt);
        }

        private static decimal GetUsdValue(string cryptoName)
        {
            string uri = "https://api.coingecko.com/api/v3/simple/price?ids=" + cryptoName + "&vs_currencies=USD";
            RestClient client = new RestClient(HTTP_VERB.GET, uri);

            RestResponse<string> response = client.MakeRequestText();
            if (response.Code != HttpStatusCode.OK)
            {
                throw new BadRequestException(response.Response);
            }

            decimal value;
            using (JsonDocument doc = JsonDocument.Parse(response.Response))
            {
                JsonElement root = doc.RootElement;
                JsonElement coin = root.GetProperty(cryptoName.ToLower());
                value = coin.GetProperty("usd").GetDecimal();
            }

            return value;
        }

        private static void SendMail(string address, string username)
        {
            MailClient client = new("smtp.gmail.com", 587);

            string body = "Please click on the following link to verify: "
                + Server.httpEndPoint + "validate/" + AES.Encrypt(username);

            using var reg = new ServerRegistry();
            string serverAddress = reg.GetEmail();
            string password = reg.GetPassword();
            
            client.SendEmail(address, serverAddress, password, body);
        }

        #endregion

        #region QUERIES

        private static T ScalarQuery<T>(string query, MySqlConnection conn, MySqlTransaction tran, params object[] placeHolderValues)
        {
            T result;

            using (var command = new MySqlCommand(query, conn, tran))
            {
                for (int i = 0; i < placeHolderValues.Length; i++)
                {
                    command.Parameters.AddWithValue("@value" + (i + 1).ToString(), placeHolderValues[i]);
                }

                command.Prepare();
                result = (T)command.ExecuteScalar();
            }

            return result;
        }

        private static int NonQuery(string query, MySqlConnection conn, MySqlTransaction tran, params object[] placeHolderValues)
        {
            int result;

            using (var command = new MySqlCommand(query, conn, tran))
            {
                for (int i = 0; i < placeHolderValues.Length; i++)
                {
                    command.Parameters.AddWithValue("@value" + (i + 1).ToString(), placeHolderValues[i]);
                }

                command.Prepare();
                result = command.ExecuteNonQuery();
            }

            return result;
        }

        private static List<T> Query<T>(
            string query, MySqlConnection conn, MySqlTransaction tran, params object[] placeHolderValues) where T : new()
        {
            List<T> objects = new();

            using (var command = new MySqlCommand(query, conn))
            {
                for (int i = 0; i < placeHolderValues.Length; i++)
                {
                    command.Parameters.AddWithValue("@value" + (i + 1).ToString(), placeHolderValues[i]);
                }

                command.Prepare();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        T obj = new();
                        foreach (var property in typeof(T).GetProperties())
                        {
                            try
                            {
                                property.SetValue(obj, reader[property.Name]);
                            }
                            catch (IndexOutOfRangeException) {}
                        }

                        objects.Add(obj);
                    }
                }
            }

            return objects;
        }

        #endregion

        #region USER

        public static UserDTO CreateUser(UserDTO userDto)
        {
            var conn = Connect();
            int? result = ScalarQuery<int?>(
                "select id from user where username = @value1", conn, null, userDto.username);

            if (result.HasValue)
            {
                Close(conn);
                throw new BadRequestException("Username already exists.");
            }

            result = ScalarQuery<int?>(
                "select id from user where email = @value1", conn, null, userDto.email);
            
            if (result.HasValue)
            {
                Close(conn);
                throw new BadRequestException("Email already in use.");
            }

            ulong? lastInsertId;
            var tran = conn.BeginTransaction();
            try
            {
                var query = "insert into user(username, password, email, dateCreated, salt, statusId) " +
                    "values(@value1, @value2, @value3, @value4, @value5, @value6)";

                using (var hasher = SHA256.Create())
                {
                    string salt = GenerateSalt();
                    userDto.password += salt;
                    userDto.password = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(userDto.password)));

                    result = NonQuery(query, conn, null,
                        userDto.username, userDto.password, userDto.email, DateTime.Now, salt, 1);
                }

                lastInsertId = ScalarQuery<ulong?>("select LAST_INSERT_ID()", conn, null);

                PositionDTO position = new();
                position.cryptoId = GetCryptos().Find(cry => cry.name.Equals(usdCoinName)).id;
                position.amount = 1000;

                CreatePositionTransactional((int)lastInsertId.Value, position, conn, tran);
                SendMail(userDto.email, userDto.username);

                tran.Commit();
            }
            catch(Exception)
            {
                tran.Rollback();
                Close(conn);

                throw;
            }

            Close(conn);
            return GetUserPublic((int)lastInsertId.Value);
        }

        public static UserDTO GetUserPublic(string username)
        {
            UserDTO user = GetUser(username);
            user.password = "";
            user.salt = "";

            return user;
        }

        public static UserDTO GetUserPublic(int id)
        {
            UserDTO user = GetUser(id);
            user.password = "";
            user.salt = "";

            return user;
        }

        public static UserDTO GetUser(string username)
        {
            var conn = Connect();
            List<UserDTO> users = Query<UserDTO>("select * from user where username = @value1", conn, null, username);
            Close(conn);

            if (users.Count == 0)
                throw new NotFoundException("User with specified username not found");

            return users[0];
        }

        public static UserDTO GetUser(int id)
        {
            var conn = Connect();
            List<UserDTO> users = Query<UserDTO>("select * from user where id = @value1", conn, null, id);
            Close(conn);

            if (users.Count == 0)
                throw new NotFoundException("User with specified id not found");

            return users[0];
        }

        public static UserDTO UpdateUser(int id, UserDTO user)
        {
            GetUser(id);

            var conn = Connect();
            int? result = ScalarQuery<int?>(
                "select id from user where username = @value1", conn, null, user.username);

            if (result.HasValue)
            {
                Close(conn);
                throw new BadRequestException("Username already exists.");
            }

            var query = "update user set username = @value1 where id = @value2";
            NonQuery(query, conn, null, user.username, id);
            Close(conn);

            return GetUserPublic(id);
        }

        public static void VerifyUserAuthentication(string username, string password)
        {
            UserDTO user = GetUser(username);
            var conn = Connect();

            int? result;
            using (var hasher = SHA256.Create())
            {
                password += user.salt;
                password = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(password)));

                result = ScalarQuery<int?>(
                    "select statusId from user where username = @value1 and password = @value2", conn, null, username, password);
            }

            Close(conn);

            if (!result.HasValue)
            {
                throw new UnauthorizedException("Incorrect credentials.");
            }

            if (GetStatuses().Find(s => s.id == result.Value).name.Equals("UNVERIFIED"))
            {
                throw new UnauthorizedException("Unverified account. Please check your e-mail.");
            }
        }

        public static void ValidateUser(string encryptedUsername)
        {
            string decryptedUsername;

            try
            {
                decryptedUsername = AES.Decrypt(encryptedUsername);
            }
            catch (CryptographicException)
            {
                throw new BadRequestException("Invalid link.");
            }

            UserDTO user = GetUser(decryptedUsername);
            List<StatusDTO> statuses = GetStatuses();
            StatusDTO currentStatus = statuses.Find(s => s.id == user.statusId);

            if (!currentStatus.name.Equals("UNVERIFIED"))
            {
                throw new BadRequestException("Only unverified users can be verified.");
            }

            user.statusId = statuses.Find(s => s.name.Equals("VERIFIED")).id;

            var conn = Connect();
            var query = "update user set statusId = @value1 where id = @value2";
            NonQuery(query, conn, null, user.statusId, user.id);

            Close(conn);
        }

        public static void DeleteUser(int id)
        {
            GetUser(id);

            var conn = Connect();
            var tran = conn.BeginTransaction();
            try
            {
                NonQuery("delete from position where userId = @value1", conn, tran, id);
                NonQuery("delete from trade where userId = @value1", conn, tran, id);

                List<StrategyDTO> strategies = GetStrategies(id);
                foreach (var strat in strategies)
                {
                    DeleteStrategyTransactional(strat.id, id, conn, tran);
                }

                NonQuery("delete from user where id = @value1", conn, tran, id);

                tran.Commit();
            }
            catch (Exception)
            {
                tran.Rollback();
                Close(conn);

                throw;
            }

            Close(conn);
        }

        public static List<StatusDTO> GetStatuses()
        {
            var conn = Connect();
            List<StatusDTO> statuses = Query<StatusDTO>("select * from status", conn, null);
            Close(conn);

            return statuses;
        }

        #endregion

        #region CRYPTO

        private static CryptoDTO GetCrypto(int id)
        {
            var conn = Connect();
            List<CryptoDTO> cryptos = Query<CryptoDTO>("select * from crypto where id = @value1", conn, null, id);
            Close(conn);

            if (cryptos.Count == 0)
                throw new NotFoundException("Crypto with specified id not found");

            return cryptos[0];
        }

        public static List<CryptoDTO> GetCryptos()
        {
            var conn = Connect();
            List<CryptoDTO> cryptos = Query<CryptoDTO>("select * from crypto", conn, null);
            Close(conn);

            return cryptos;
        }

        #endregion

        #region POSITION

        private static PositionDTO CreatePositionTransactional(int userId, PositionDTO positionDto, MySqlConnection conn, MySqlTransaction tran)
        {
            GetCrypto(positionDto.cryptoId);

            int? result = ScalarQuery<int?>("select id from `position` where userId = @value1 and cryptoId = @value2",
                conn, tran, userId, positionDto.cryptoId);

            if (result.HasValue)
                throw new BadRequestException("Position already exists for crypto and user.");

            NonQuery("insert into `position`(userId, cryptoId, amount) values(@value1, @value2, @value3)",
                conn, tran, userId, positionDto.cryptoId, positionDto.amount);

            ulong? lastInsertId = ScalarQuery<ulong?>("select LAST_INSERT_ID()", conn, tran);

            return GetPositionTransactional((int)lastInsertId.Value, userId, conn, tran);
        }

        private static PositionDTO GetPositionTransactional(int id, int userId, MySqlConnection conn, MySqlTransaction tran)
        {
            List<PositionDTO> positions = Query<PositionDTO>("select * from `position` where id = @value1 and userId = @value2",
                conn, tran, id, userId);

            if (positions.Count == 0)
                throw new NotFoundException("Position with specified id and userId not found");

            return positions[0];
        }

        private static PositionDTO GetPosition(int id, int userId)
        {
            var conn = Connect();
            List<PositionDTO> positions = Query<PositionDTO>("select * from `position` where id = @value1 and userId = @value2",
                conn, null, id, userId);
            Close(conn);

            if (positions.Count == 0)
                throw new NotFoundException("Position with specified id and userId not found");

            return positions[0];
        }

        public static List<PositionDTO> GetPositions(int userId)
        {
            var conn = Connect();
            List<PositionDTO> positions = Query<PositionDTO>("select * from `position` where userId = @value1", conn, null, userId);
            Close(conn);

            return positions;
        }

        private static PositionDTO UpdatePositionTransactional(int id, int userId, PositionDTO position, MySqlConnection conn, MySqlTransaction tran)
        {
            GetPosition(id, userId);

            NonQuery("update `position` set amount = @value1 where id = @value2 and userId = @value3",
                conn, tran, position.amount, id, userId);

            return GetPosition(id, userId);
        }

        public static PositionDTO UpdatePosition(int id, int userId, PositionDTO position)
        {
            GetPosition(id, userId);

            var conn = Connect();
            NonQuery("update `position` set amount = @value1 where id = @value2 and userId = @value3",
                conn, null, position.amount, id, userId);
            Close(conn);

            return GetPosition(id, userId);
        }

        private static void DeletePositionTransactional(int id, int userId, MySqlConnection conn, MySqlTransaction tran)
        {
            PositionDTO position = GetPosition(id, userId);
            CryptoDTO crypto = GetCrypto(position.cryptoId);

            if (crypto.name.Equals(usdCoinName))
                throw new BadRequestException("Can't delete that position.");

            NonQuery("delete from `position` where id = @value1 and userId = @value2", conn, tran, id, userId);
        }

        public static void DeletePosition(int id, int userId)
        {
            PositionDTO position = GetPosition(id, userId);
            CryptoDTO crypto = GetCrypto(position.cryptoId);

            if (crypto.name.Equals(usdCoinName))
                throw new BadRequestException("Can't delete that position.");

            var conn = Connect();
            NonQuery("delete from `position` where id = @value1 and userId = @value2", conn, null, id, userId);
            Close(conn);
        }

        #endregion

        #region TRADE

        public static TradeDTO CreateTrade(int userId, TradeDTO trade)
        {
            if (trade.amount < 10)
                throw new BadRequestException("Trade USD value must be greater than 10 USD.");

            GetUser(userId);
           
            CryptoDTO usd = GetCryptos().Find(cry => cry.name.Equals(usdCoinName));
            List<PositionDTO> positions = GetPositions(userId);

            PositionDTO userUsdPos = positions.Find(pos => pos.cryptoId == usd.id);
            PositionDTO userOtherPos = positions.Find(pos => pos.cryptoId == trade.cryptoId);

            TradeTypeDTO tradeType = GetTradeType(trade.tradeTypeId);
            decimal otherValue = GetUsdValue(GetCrypto(trade.cryptoId).name);

            var conn = Connect();
            var tran = conn.BeginTransaction();
            try
            {
                if (tradeType.name.Equals("BUY"))
                {
                    Buy(userUsdPos, userOtherPos, otherValue, trade, userId, conn, tran);
                }
                else if (tradeType.name.Equals("SELL"))
                {
                    Sell(userUsdPos, userOtherPos, otherValue, trade, userId, conn, tran);
                }

                NonQuery("insert into trade(userId, cryptoId, tradeTime, amount, tradeTypeId) values(@value1, @value2, @value3, @value4, @value5)",
                    conn, tran, userId, trade.cryptoId, DateTime.Now, trade.amount, tradeType.id);
            }
            catch (Exception)
            {
                tran.Rollback();
                Close(conn);

                throw;
            }

            tran.Commit();
            ulong? lastInsertId = ScalarQuery<ulong?>("select LAST_INSERT_ID()", conn, null);

            Close(conn);
            return GetTrade((int)lastInsertId.Value, userId);
        }

        private static void Buy(PositionDTO userUsdPos, PositionDTO userOtherPos, decimal otherValue, TradeDTO trade, int id,
            MySqlConnection conn, MySqlTransaction tran)
        {
            if (userUsdPos.amount < trade.amount)
            {
                throw new BadRequestException("Not enough USD.");
            }
            else if (userOtherPos == null)
            {
                userOtherPos = new PositionDTO
                {
                    amount = trade.amount / otherValue,
                    cryptoId = trade.cryptoId
                };

                CreatePositionTransactional(id, userOtherPos, conn, tran);
            }
            else
            {
                userOtherPos.amount += trade.amount / otherValue;
                UpdatePositionTransactional(userOtherPos.id, id, userOtherPos, conn, tran);
            }

            userUsdPos.amount -= trade.amount;
            UpdatePositionTransactional(userUsdPos.id, id, userUsdPos, conn, tran);
        }

        private static void Sell(PositionDTO userUsdPos, PositionDTO userOtherPos, decimal otherValue, TradeDTO trade, int id,
            MySqlConnection conn, MySqlTransaction tran)
        {
            if (userOtherPos == null || userOtherPos.amount < trade.amount / otherValue)
            {
                throw new BadRequestException("Can't sell more than you own.");
            }
            else if (userUsdPos.amount - trade.amount / otherValue < 2)
            {
                DeletePositionTransactional(userOtherPos.id, id, conn, tran);
            }
            else
            {
                userOtherPos.amount -= trade.amount / otherValue;
                UpdatePositionTransactional(userOtherPos.id, id, userOtherPos, conn, tran);
            }

            userUsdPos.amount += trade.amount;
            UpdatePositionTransactional(userUsdPos.id, id, userUsdPos, conn, tran);
        }

        private static TradeDTO GetTrade(int id, int userId)
        {
            var conn = Connect();
            List<TradeDTO> trades = Query<TradeDTO>("select * from trade where id = @value1 and userId = @value2",
                conn, null, id, userId);
            Close(conn);

            if (trades.Count == 0)
                throw new NotFoundException("Trade with specified id and userId not found");

            return trades[0];
        }

        public static List<TradeDTO> GetTrades(int userId)
        {
            var conn = Connect();
            List<TradeDTO> trades = Query<TradeDTO>("select * from trade where userId = @value1", conn, null, userId);
            Close(conn);

            return trades;
        }

        public static void DeleteTrade(int id, int userId)
        {
            GetTrade(id, userId);

            var conn = Connect();
            NonQuery("delete from trade where id = @value1 and userId = @value2", conn, null, id, userId);
            Close(conn);
        }

        public static List<TradeTypeDTO> GetTradeTypes()
        {
            var conn = Connect();
            List<TradeTypeDTO> tradeTypes = Query<TradeTypeDTO>("select * from tradeType", conn, null);
            Close(conn);

            return tradeTypes;
        }

        private static TradeTypeDTO GetTradeType(int id)
        {
            var conn = Connect();
            List<TradeTypeDTO> types = Query<TradeTypeDTO>("select * from tradeType where id = @value1", conn, null, id);
            Close(conn);

            if (types.Count == 0)
                throw new NotFoundException("Trade type with specified id not found");

            return types[0];
        }

        #endregion

        #region STRATEGY

        public static StrategyDTO CreateStrategy(int userId, StrategyDTO strategy)
        {
            GetUser(userId);

            var conn = Connect();
            int? result = ScalarQuery<int?>(
                "select id from strategy where name = @value1", conn, null, strategy.name);

            if (result.HasValue)
            {
                Close(conn);
                throw new BadRequestException("Name already exists.");
            }

            var tran = conn.BeginTransaction();
            ulong? insertedStratId;
            try
            {
                var query = "insert into strategy(name, userId) values(@value1, @value2)";
                NonQuery(query, conn, tran, strategy.name, userId);

                insertedStratId = ScalarQuery<ulong?>("select LAST_INSERT_ID()", conn, tran);
                
                foreach (var trigger in strategy.triggers)
                {
                    trigger.strategyId = (int)insertedStratId;
                    CreateTriggerTransactional(trigger, conn, tran);
                }
                
                tran.Commit();
                Close(conn);
            }
            catch (Exception)
            {
                tran.Rollback();
                Close(conn);

                throw;
            }

            return GetStrategy((int)insertedStratId.Value, userId);
        }

        private static StrategyDTO GetStrategy(int id, int userId)
        {
            var conn = Connect();
            List<StrategyDTO> strategies = Query<StrategyDTO>("select * from strategy where id = @value1 and userId = @value2",
                conn, null, id, userId);
            Close(conn);

            if (strategies.Count == 0)
                throw new NotFoundException("Strategy with specified id and userId not found");

            StrategyDTO strategy = strategies[0];
            strategy.triggers = GetTriggers(strategy.id);

            return strategy;
        }

        public static List<StrategyDTO> GetStrategies(int userId)
        {
            var conn = Connect();
            List<StrategyDTO> strategies = Query<StrategyDTO>("select * from strategy where userId = @value1", conn, null, userId);
            Close(conn);

            foreach (var strat in strategies)
            {
                strat.triggers = GetTriggers(strat.id);
            }

            return strategies;
        }

        public static StrategyDTO UpdateStrategy(int id, int userId, StrategyDTO strategy)
        {
            GetStrategy(id, userId);

            var conn = Connect();
            int? result = ScalarQuery<int?>(
                "select id from strategy where name = @value1", conn, null, strategy.name);

            if (result.HasValue)
            {
                if (id != result.Value)
                {
                    Close(conn);
                    throw new BadRequestException("Name already exists.");
                }
            }

            var tran = conn.BeginTransaction();
            try
            {
                NonQuery("update strategy set name = @value1 where id = @value2", conn, tran, strategy.name, id);
                NonQuery("delete from `trigger` where strategyId = @value1", conn, tran, id);

                foreach (var trigger in strategy.triggers)
                {
                    trigger.strategyId = id;
                    CreateTriggerTransactional(trigger, conn, tran);
                }

                tran.Commit();
            }
            catch (Exception)
            {
                tran.Rollback();
                Close(conn);

                throw;
            }

            Close(conn);
            return GetStrategy(id, userId);
        }

        public static void DeleteStrategy(int id, int userId)
        {
            GetStrategy(id, userId);

            var conn = Connect();
            var tran = conn.BeginTransaction();
            try
            {
                NonQuery("delete from `trigger` where strategyId = @value1", conn, tran, id);
                NonQuery("delete from strategy where id = @value1 and userId = @value2", conn, tran, id, userId);

                tran.Commit();
            }
            catch (Exception)
            {
                tran.Rollback();
                Close(conn);

                throw;
            }

            Close(conn);
        }

        public static void DeleteStrategyTransactional(int id, int userId, MySqlConnection conn, MySqlTransaction tran)
        {
            GetStrategy(id, userId);
            NonQuery("delete from `trigger` where strategyId = @value1", conn, tran, id);
            NonQuery("delete from strategy where id = @value1 and userId = @value2", conn, tran, id, userId);
        }

        #endregion

        #region TRIGGER

        private static TriggerDTO CreateTriggerTransactional(TriggerDTO trigger, MySqlConnection conn, MySqlTransaction tran)
        {
            GetIndicator(trigger.indicatorId);
            GetTriggerType(trigger.triggerTypeId);

            int? result = ScalarQuery<int?>(
                "select id from `trigger` where strategyId = @value1 and indicatorId = @value2", conn, tran, trigger.strategyId, trigger.indicatorId);

            if (result.HasValue)
                throw new BadRequestException("Trigger with specified indicatorId already exists for specified strategyId");

            NonQuery(
                "insert into `trigger`(strategyId, indicatorId, indicatorValue, triggerTypeId) values(@value1, @value2, @value3, @value4)", conn, tran, trigger.strategyId, trigger.indicatorId, trigger.indicatorValue, trigger.triggerTypeId);

            ulong? lastInsert = ScalarQuery<ulong?>("select LAST_INSERT_ID()", conn, tran);

            return GetTriggerTransactional((int)lastInsert, conn, tran);
        }

        private static TriggerDTO GetTriggerTransactional(int id, MySqlConnection conn, MySqlTransaction tran)
        {
            List<TriggerDTO> triggers = Query<TriggerDTO>("select * from `trigger` where id = @value1",
                conn, tran, id);

            if (triggers.Count == 0)
                throw new NotFoundException("Trigger with specified id not found");

            return triggers[0];
        }

        private static List<TriggerDTO> GetTriggers(int strategyId)
        {
            var conn = Connect();
            List<TriggerDTO> triggers = Query<TriggerDTO>("select * from `trigger` where strategyId = @value1",
                conn, null, strategyId);
            Close(conn);

            return triggers;
        }

        public static List<TriggerTypeDTO> GetTriggerTypes()
        {
            var conn = Connect();
            List<TriggerTypeDTO> triggerTypes = Query<TriggerTypeDTO>("select * from triggerType", conn, null);
            Close(conn);

            return triggerTypes;
        }

        private static TriggerTypeDTO GetTriggerType(int id)
        {
            var conn = Connect();
            List<TriggerTypeDTO> types = Query<TriggerTypeDTO>("select * from triggerType where id = @value1", conn, null, id);
            Close(conn);

            if (types.Count == 0)
                throw new NotFoundException("Trigger type with specified id not found");

            return types[0];
        }

        #endregion

        #region INDICATOR

        public static List<IndicatorDTO> GetIndicators()
        {
            var conn = Connect();
            List<IndicatorDTO> indicators = Query<IndicatorDTO>("select * from indicator", conn, null);
            Close(conn);

            return indicators;
        }

        private static IndicatorDTO GetIndicator(int id)
        {
            var conn = Connect();
            List<IndicatorDTO> indicators = Query<IndicatorDTO>("select * from indicator where id = @value1", conn, null, id);
            Close(conn);

            if (indicators.Count == 0)
                throw new NotFoundException("Indicator with specified id not found");

            return indicators[0];
        }

        #endregion
    }
}