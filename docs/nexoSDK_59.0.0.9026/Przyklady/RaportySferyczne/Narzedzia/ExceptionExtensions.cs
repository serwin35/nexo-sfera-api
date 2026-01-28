using System;

namespace RaportySferyczne.Narzedzia
{
    internal static class ExceptionExtensions
    {
        public static Exception MostInnerException(this Exception exception)
        {
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return exception;
        }
    }
}
