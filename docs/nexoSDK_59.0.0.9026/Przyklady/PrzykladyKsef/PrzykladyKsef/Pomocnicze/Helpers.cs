using System;
using System.Text;
using System.Linq;

namespace PrzykladyKsef
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

        internal static void WypiszBledy(this InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy obiektBiznesowy)
        {
            WypiszBledy((InsERT.Mox.BusinessObjects.IBusinessObject)obiektBiznesowy);
            var uow = ((InsERT.Mox.BusinessObjects.IGetUnitOfWork)obiektBiznesowy).UnitOfWork;
            foreach (var innyObiektBiznesowy in uow.Participants.OfType<InsERT.Mox.BusinessObjects.IBusinessObject>().Where(bo => bo != obiektBiznesowy))
            {
                WypiszBledy(innyObiektBiznesowy);
            }
        }

        internal static void WypiszBledy(this InsERT.Mox.BusinessObjects.IBusinessObject obiektBiznesowy)
        {
            foreach (var encjaZBledami in obiektBiznesowy.InvalidData)
            {
                foreach (var bladNaCalejEncji in encjaZBledami.Errors)
                {
                    Console.Error.WriteLine(bladNaCalejEncji);
                    Console.Error.WriteLine(" na encjach:" + encjaZBledami.GetType().Name);
                    Console.Error.WriteLine();
                }
                foreach (var bladNaKonkretnychPolach in encjaZBledami.MemberErrors)
                {
                    Console.Error.WriteLine(bladNaKonkretnychPolach.Key);
                    Console.Error.WriteLine(" na polach:");
                    Console.Error.WriteLine(string.Join(", ", bladNaKonkretnychPolach.Select(b => encjaZBledami.GetType().Name + "." + b)));
                    Console.Error.WriteLine();
                }
            }
        }
    }
}
