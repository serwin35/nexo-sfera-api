using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealizacjeDokumentow
{
    internal static class Helpers
    {
        internal static string GetExceptionMessage(Exception exception)
        {
            var builder = new StringBuilder();

            _ = builder.AppendLine($"{exception.GetType().FullName} -- {exception.Message}")
                .AppendLine(exception.StackTrace);

            if (exception.InnerException != null)
            {
                string innerExceptionMessage = GetExceptionMessage(exception.InnerException);
                _ = builder.AppendLine("---- InnerException: ")
                    .Append(innerExceptionMessage);
            }

            return builder.ToString();
        }
    }
}
