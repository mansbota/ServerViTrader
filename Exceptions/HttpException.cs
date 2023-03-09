using System;
using System.Net;

namespace ServerViTrader.Exceptions
{
    class HttpException : Exception
    {
        public HttpStatusCode StatusCode { get; set; }

        public HttpException()
        {
        }

        public HttpException(string message) : base(message)
        {
        }

        public HttpException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
