using InsERT.Moria.Sfera;

namespace GeneratorDanych
{
	public sealed class PostepLadowaniaSfery : IPostepLadowaniaSfery
	{
		private readonly PostepViewModel _postep;

		public PostepLadowaniaSfery(PostepViewModel postep)
		{
			_postep = postep;
			_postep.CzyMaProcent = true;
			_postep.BiezacyProcent = 0;
			_postep.Opis = string.Empty;
		}

		public void RaportujPostep(PostepLadowaniaSferyEventArgs args)
		{
			_postep.BiezacyProcent = args.BiezacyProcent;
			_postep.Opis = args.Opis;

			Interaction.DoEvents();
		}
	}
}