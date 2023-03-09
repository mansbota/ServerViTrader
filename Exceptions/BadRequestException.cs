using System;
using System.Net;

namespace ServerViTrader.Exceptions
{
    class BadRequestException : HttpException
    {
        public BadRequestException()
        {
            StatusCode = HttpStatusCode.BadRequest;
        }

        public BadRequestException(string message) : base(message)
        {
            StatusCode = HttpStatusCode.BadRequest;
        }

        public BadRequestException(string message, Exception innerException) : base(message, innerException)
        {
            StatusCode = HttpStatusCode.BadRequest;
        }
    }
}
