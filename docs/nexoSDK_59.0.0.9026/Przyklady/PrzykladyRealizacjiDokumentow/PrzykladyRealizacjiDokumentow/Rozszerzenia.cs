using System;
using System.Linq;

namespace RealizacjeDokumentow
{
    public static class Rozszerzenia
    {
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
