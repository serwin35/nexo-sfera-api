using System;
using System.Linq;
using System.Text;

namespace GeneratorDanych.OperacjeSferyczne
{
	public static class Rozszerzenia
	{
		internal static string PobierzBledy(this InsERT.Mox.ObiektyBiznesowe.IObiektBiznesowy obiektBiznesowy)
		{
			var builder = new StringBuilder();
			string bledy = PobierzBledy((InsERT.Mox.BusinessObjects.IBusinessObject)obiektBiznesowy);
			if (!string.IsNullOrEmpty(bledy))
			{
				builder.AppendLine(bledy);
			}
			var uow = ((InsERT.Mox.BusinessObjects.IGetUnitOfWork)obiektBiznesowy).UnitOfWork;
			foreach (var innyObiektBiznesowy in uow.Participants.OfType<InsERT.Mox.BusinessObjects.IBusinessObject>().Where(bo => bo != obiektBiznesowy))
			{
				string bledyInnegoObiektu = PobierzBledy(innyObiektBiznesowy);
				if (!string.IsNullOrEmpty(bledyInnegoObiektu))
				{
					builder.AppendLine(bledyInnegoObiektu);
				}
			}
			return builder.ToString();
		}

		internal static string PobierzBledy(this InsERT.Mox.BusinessObjects.IBusinessObject obiektBiznesowy)
		{
			var builder = new StringBuilder();
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