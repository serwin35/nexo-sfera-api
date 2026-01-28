namespace GeneratorDanych
{
	public sealed class PostepViewModel : PropertyChangedNotifier
	{
		private int _biezacyProcent;
		private bool _czyMaProcent;
		private string _opis;

		public int BiezacyProcent
		{
			get => _biezacyProcent;
			set
			{
				_biezacyProcent = value;
				OnPropertyChanged(nameof(BiezacyProcent));
			}
		}

		public bool CzyMaProcent
		{
			get => _czyMaProcent;
			set
			{
				_czyMaProcent = value;
				OnPropertyChanged(nameof(CzyMaProcent));
			}
		}

		public string Opis
		{
			get => _opis;
			set
			{
				_opis = value;
				OnPropertyChanged(nameof(Opis));
			}
		}

		public void RozpocznijOperacjeBezProcentow(string opisOperacji)
		{
			CzyMaProcent = false;
			BiezacyProcent = 0;
			Opis = opisOperacji;
		}

		public void Zatrzymaj()
		{
			CzyMaProcent = true;
			BiezacyProcent = 0;
			Opis = string.Empty;
		}
	}
}