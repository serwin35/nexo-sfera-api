using InsERT.Mox.BusinessObjects;
using InsERT.Mox.ObiektyBiznesowe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomatyzacjaPrzyklady.Rozszerzenia
{
    public static class Rozszerzenia
    {
        internal static string PodajBledy(this IObiektBiznesowy obiektBiznesowy)
        {
            StringBuilder sbMsg = new StringBuilder();
            sbMsg.Append(PodajBledy((IBusinessObject)obiektBiznesowy));
            var uow = ((IGetUnitOfWork)obiektBiznesowy).UnitOfWork;
            foreach (var innyObiektBiznesowy in uow.Participants.OfType<IBusinessObject>().Where(bo => bo != obiektBiznesowy))
            {
                sbMsg.Append(PodajBledy(innyObiektBiznesowy));
            }
            return sbMsg.ToString();
        }



        internal static string PodajBledy(this IBusinessObject obiektBiznesowy)
        {
            StringBuilder result = new StringBuilder();
            foreach (var encjaZBledami in obiektBiznesowy.InvalidData)
            {
                foreach (var bladNaCalejEncji in encjaZBledami.Errors)
                {
                    result.AppendLine(bladNaCalejEncji.ToString());
                    result.AppendLine(" na encjach:" + encjaZBledami.GetType().Name);
                    result.AppendLine();
                }
                foreach (var bladNaKonkretnychPolach in encjaZBledami.MemberErrors)
                {
                    result.AppendLine(bladNaKonkretnychPolach.Key.ToString());
                    result.AppendLine(" na polach:");
                    result.AppendLine(string.Join(", ", bladNaKonkretnychPolach.Select(b => encjaZBledami.GetType().Name + "." + b)));
                    result.AppendLine();
                }
            }
            return result.ToString();
        }
    }
}
