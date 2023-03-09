using System;
using System.Net;

namespace ServerViTrader.Exceptions
{
    class NotImplementedException : HttpException
    {
        public NotImplementedException()
        {
            StatusCode = HttpStatusCode.NotImplemented;
        }

        public NotImplementedException(string message) : base(message)
        {
            StatusCode = HttpStatusCode.NotImplemented;
        }

        public NotImplementedException(string message, Exception innerException) : base(message, innerException)
        {
            StatusCode = HttpStatusCode.NotImplemented;
        }
    }
}
