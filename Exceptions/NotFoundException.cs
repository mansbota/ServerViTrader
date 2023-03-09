using System;
using System.Net;

namespace ServerViTrader.Exceptions
{
    class NotFoundException : HttpException
    {
        public NotFoundException()
        {
            StatusCode = HttpStatusCode.NotFound;
        }

        public NotFoundException(string message) : base(message)
        {
            StatusCode = HttpStatusCode.NotFound;
        }

        public NotFoundException(string message, Exception innerException) : base(message, innerException)
        {
            StatusCode = HttpStatusCode.NotFound;
        }
    }
}
