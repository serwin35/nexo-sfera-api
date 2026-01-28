using System.Linq;
using System.Text;

namespace ObslugaLogistyki.Pomocnicze
{
    public static class ObslugaBledow
    {
        internal static string WypiszBledy(this InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy obiektBiznesowy)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(WypiszBledy((InsERT.Mox.BusinessObjects.IBusinessObject)obiektBiznesowy));
            var uow = ((InsERT.Mox.BusinessObjects.IGetUnitOfWork)obiektBiznesowy).UnitOfWork;
            foreach (var innyObiektBiznesowy in uow.Participants.OfType<InsERT.Mox.BusinessObjects.IBusinessObject>().Where(bo => bo != obiektBiznesowy))
            {
                builder.AppendLine(WypiszBledy(innyObiektBiznesowy));
            }
            return builder.ToString();
        }

        internal static string WypiszBledy(this InsERT.Mox.BusinessObjects.IBusinessObject obiektBiznesowy)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var encjaZBledami in obiektBiznesowy.InvalidData)
            {
                foreach (var bladNaCalejEncji in encjaZBledami.Errors)
                {
                    builder.AppendLine(bladNaCalejEncji.ToString());
                    builder.AppendLine(" na encjach:" + encjaZBledami.GetType().Name);
                    builder.AppendLine();
                }
                foreach (var bladNaKonkretnychPolach in encjaZBledami.MemberErrors)
                {
                    builder.AppendLine(bladNaKonkretnychPolach.Key.ToString());
                    builder.AppendLine(" na polach:");
                    builder.AppendLine(string.Join(", ", bladNaKonkretnychPolach.Select(b => encjaZBledami.GetType().Name + "." + b)));
                    builder.AppendLine();
                }
            }
            return builder.ToString();
        }
    }

}
