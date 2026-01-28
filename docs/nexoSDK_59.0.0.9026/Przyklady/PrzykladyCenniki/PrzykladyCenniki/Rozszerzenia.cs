using InsERT.Mox.BusinessObjects;
using InsERT.Mox.ObiektyBiznesowe;
using System;
using System.Linq;

namespace PrzykladyCenniki
{
    public static class Rozszerzenia
    {
        internal static void WypiszBledy(this IObiektBiznesowy obiektBiznesowy)
        {
            WypiszBledy((IBusinessObject)obiektBiznesowy);
            var uow = ((IGetUnitOfWork)obiektBiznesowy).UnitOfWork;
            foreach (var innyObiektBiznesowy in uow.Participants.OfType<IBusinessObject>().Where(bo => bo != obiektBiznesowy))
            {
                WypiszBledy(innyObiektBiznesowy);
            }
        }

        internal static void WypiszBledy(this IBusinessObject obiektBiznesowy)
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
