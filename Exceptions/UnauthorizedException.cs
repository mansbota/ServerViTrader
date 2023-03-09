using System;
using System.Net;

namespace ServerViTrader.Exceptions
{
    class UnauthorizedException : HttpException
    {
        public UnauthorizedException()
        {
            StatusCode = HttpStatusCode.Unauthorized;
        }

        public UnauthorizedException(string message) : base(message)
        {
            StatusCode = HttpStatusCode.Unauthorized;
        }

        public UnauthorizedException(string message, Exception innerException) : base(message, innerException)
        {
            StatusCode = HttpStatusCode.Unauthorized;
        }
    }
}
