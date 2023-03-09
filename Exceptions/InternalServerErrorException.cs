using System;
using System.Net;

namespace ServerViTrader.Exceptions
{
    class InternalServerErrorException : HttpException
    {
        public InternalServerErrorException()
        {
            StatusCode = HttpStatusCode.InternalServerError;
        }

        public InternalServerErrorException(string message) : base(message)
        {
            StatusCode = HttpStatusCode.InternalServerError;
        }

        public InternalServerErrorException(string message, Exception innerException) : base(message, innerException)
        {
            StatusCode = HttpStatusCode.InternalServerError;
        }
    }
}
