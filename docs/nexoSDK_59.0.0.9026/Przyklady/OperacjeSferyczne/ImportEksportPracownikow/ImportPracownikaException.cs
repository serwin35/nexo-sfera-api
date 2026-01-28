using System;
using System.Runtime.Serialization;

namespace OperacjeSferyczne.ImportEksportPracownikow
{
    public class ImportPracownikaException : Exception
    {
        public ImportPracownikaException()
        {
        }

        public ImportPracownikaException(string message) : base(message)
        {
        }

        public ImportPracownikaException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
