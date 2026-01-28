using InsERT.Moria.Sfera;
using System;

namespace InwentaryzacjaPrzyklady
{
	public class PostepLadowaniaSfery : IPostepLadowaniaSfery
	{
		private const int StoProcent = 100;
		private string _opis;
		private int _step = 0;

		public void RaportujPostep(PostepLadowaniaSferyEventArgs args)
		{
			if (args.Opis != _opis)
			{
				_opis = args.Opis;
				++_step;
			}

			Console.Write($"\r{_step}. {args.Opis}: {args.BiezacyProcent} %");

			if (args.BiezacyProcent == StoProcent)
			{
				Console.WriteLine();
			}
		}
	}
}
