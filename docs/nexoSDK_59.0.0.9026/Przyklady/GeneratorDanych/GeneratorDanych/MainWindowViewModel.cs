using GeneratorDanych.OperacjeSferyczne;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GeneratorDanych
{
	public sealed class MainWindowViewModel : PropertyChangedNotifier
	{
		private string _bazaDanych;
		private bool _czySferaJestUruchomiona;
		private bool _czyTrwaDodawanieTowarow;
		private bool _czyTrwaDodawanieZamowien;
		private bool _czyTrwaUruchamianieSfery;
		private bool _edytowalneDanePolaczenia;
		private GeneratorTowarow _generatorTowarow;
		private GeneratorZamowienOdKlientow _generatorZamowien;
		private bool _gotowyDoLogowania;
		private string _hasloNexo;
		private string _hasloSql;
		private double _ileCzasuNaTowar;
		private double _ileCzasuNaZamowienie;
		private int _ileTowarowNaMinute;
		private int _ileTowarowZmierzono;
		private int _ileZamowienNaMinute;
		private int _ileZamowienZmierzono;
		private string _loginNexo;
		private string _loginSQL;
		private string _serwerSQL;
		private bool _uwierzytelnienieWindows;
		private IEnumerable<string> _uzytkownicy;

		public MainWindowViewModel()
		{
			PostepOperacji = new PostepViewModel()
			{
				BiezacyProcent = 0,
				Opis = string.Empty
			};
			LogOperacji = new LogOperacji();
		}

		public string BazaDanych
		{
			get => _bazaDanych;
			set
			{
				_bazaDanych = value;
				OnPropertyChanged(nameof(BazaDanych));
			}
		}

		public bool CzyMoznaDodacTowary => CzySferaJestUruchomiona && !CzyTrwaDodawanieZamowien && !CzyTrwaDodawanieTowarow;
		public bool CzyMoznaDodacZamowienia => CzySferaJestUruchomiona && !CzyTrwaDodawanieZamowien && !CzyTrwaDodawanieTowarow;

		public bool CzySferaJestUruchomiona
		{
			get => _czySferaJestUruchomiona;
			set
			{
				_czySferaJestUruchomiona = value;
				OnPropertyChanged(nameof(CzySferaJestUruchomiona));
				OnPropertyChanged(nameof(CzyMoznaDodacZamowienia));
				OnPropertyChanged(nameof(CzyMoznaDodacTowary));
			}
		}

		public bool CzyTrwaDodawanieTowarow
		{
			get => _czyTrwaDodawanieTowarow;
			set
			{
				_czyTrwaDodawanieTowarow = value;
				OnPropertyChanged(nameof(CzyTrwaDodawanieTowarow));
				OnPropertyChanged(nameof(CzyMoznaDodacZamowienia));
				OnPropertyChanged(nameof(CzyMoznaDodacTowary));
				OnPropertyChanged(nameof(CzyTrwaOperacja));
			}
		}

		public bool CzyTrwaDodawanieZamowien
		{
			get => _czyTrwaDodawanieZamowien;
			set
			{
				_czyTrwaDodawanieZamowien = value;
				OnPropertyChanged(nameof(CzyTrwaDodawanieZamowien));
				OnPropertyChanged(nameof(CzyMoznaDodacZamowienia));
				OnPropertyChanged(nameof(CzyMoznaDodacTowary));
				OnPropertyChanged(nameof(CzyTrwaOperacja));
			}
		}

		public bool CzyTrwaOperacja => CzyTrwaUruchamianieSfery || CzyTrwaDodawanieTowarow || CzyTrwaDodawanieZamowien;

		public bool CzyTrwaUruchamianieSfery
		{
			get => _czyTrwaUruchamianieSfery;
			set
			{
				_czyTrwaUruchamianieSfery = value;
				OnPropertyChanged(nameof(CzyTrwaUruchamianieSfery));
				OnPropertyChanged(nameof(CzyTrwaOperacja));
			}
		}

		public bool EdytowalneDanePolaczenia
		{
			get => _edytowalneDanePolaczenia;
			set
			{
				_edytowalneDanePolaczenia = value;
				OnPropertyChanged(nameof(EdytowalneDanePolaczenia));
			}
		}

		public bool GotowyDoLogowania
		{
			get => _gotowyDoLogowania;
			set
			{
				_gotowyDoLogowania = value;
				OnPropertyChanged(nameof(GotowyDoLogowania));
			}
		}

		public string HasloNexo
		{
			get => _hasloNexo;
			set
			{
				_hasloNexo = value;
				OnPropertyChanged(nameof(HasloNexo));
			}
		}

		public string HasloSql
		{
			get => _hasloSql;
			set
			{
				_hasloSql = value;
				OnPropertyChanged(nameof(HasloSql));
			}
		}

		public double IleCzasuNaTowar
		{
			get => _ileCzasuNaTowar;
			set
			{
				_ileCzasuNaTowar = value;
				OnPropertyChanged(nameof(IleCzasuNaTowar));
			}
		}

		public double IleCzasuNaZamowienie
		{
			get => _ileCzasuNaZamowienie;
			set
			{
				_ileCzasuNaZamowienie = value;
				OnPropertyChanged(nameof(IleCzasuNaZamowienie));
			}
		}

		public int IleTowarowNaMinute
		{
			get => _ileTowarowNaMinute;
			set
			{
				_ileTowarowNaMinute = value;
				OnPropertyChanged(nameof(IleTowarowNaMinute));
			}
		}

		public int IleTowarowZmierzono
		{
			get => _ileTowarowZmierzono;
			set
			{
				_ileTowarowZmierzono = value;
				OnPropertyChanged(nameof(IleTowarowZmierzono));
			}
		}

		public int IleZamowienNaMinute
		{
			get => _ileZamowienNaMinute;
			set
			{
				_ileZamowienNaMinute = value;
				OnPropertyChanged(nameof(IleZamowienNaMinute));
			}
		}

		public int IleZamowienZmierzono
		{
			get => _ileZamowienZmierzono;
			set
			{
				_ileZamowienZmierzono = value;
				OnPropertyChanged(nameof(IleZamowienZmierzono));
			}
		}

		public string LoginNexo
		{
			get => _loginNexo;
			set
			{
				_loginNexo = value;
				OnPropertyChanged(nameof(LoginNexo));
			}
		}

		public string LoginSql
		{
			get => _loginSQL;
			set
			{
				_loginSQL = value;
				OnPropertyChanged(nameof(LoginSql));
			}
		}

		public LogOperacji LogOperacji { get; }
		public PostepViewModel PostepOperacji { get; }

		public string SerwerSql
		{
			get => _serwerSQL;
			set
			{
				_serwerSQL = value;
				OnPropertyChanged(nameof(SerwerSql));
			}
		}

		public bool UwierzytelnienieWindows
		{
			get => _uwierzytelnienieWindows;
			set
			{
				_uwierzytelnienieWindows = value;
				OnPropertyChanged(nameof(UwierzytelnienieWindows));
			}
		}

		public IEnumerable<string> Uzytkownicy
		{
			get => _uzytkownicy;
			set
			{
				_uzytkownicy = value;
				OnPropertyChanged(nameof(Uzytkownicy));
			}
		}

		public string WersjaDotnet => Environment.Version?.ToString();
		public string WersjaSfery => DanePolaczenia.WersjaSfery.ToString(4);

		internal Action<Exception> ExceptionHandler { get; set; }

		internal Uchwyt UchwytDoSfery { get; private set; }

		internal async Task DodajTowary(int ileTowarow)
		{
			if (_generatorTowarow == null)
			{
				_generatorTowarow = new GeneratorTowarow(UchwytDoSfery);
			}
			CzyTrwaDodawanieTowarow = true;
			PostepOperacji.RozpocznijOperacjeBezProcentow("Trwa dodawanie towarów");
			await _generatorTowarow.DodajTowary(ileTowarow, LogOperacji);

			var (IleOperacji, SredniCzasOperacji, IleOperacjiNaMinute) = _generatorTowarow.PobierzStatystyki();
			IleTowarowNaMinute = IleOperacjiNaMinute;
			IleCzasuNaTowar = SredniCzasOperacji;
			IleTowarowZmierzono = IleOperacji;

			CzyTrwaDodawanieTowarow = false;
			PostepOperacji.Zatrzymaj();
		}

		internal async Task DodajZamowienia(int ileZamowien)
		{
			if (_generatorZamowien == null)
			{
				_generatorZamowien = new GeneratorZamowienOdKlientow(UchwytDoSfery);
			}
			CzyTrwaDodawanieZamowien = true;
			PostepOperacji.RozpocznijOperacjeBezProcentow("Trwa dodawanie zamówień");
			await _generatorZamowien.DodajZamowienia(ileZamowien, LogOperacji);

			var (IleOperacji, SredniCzasOperacji, IleOperacjiNaMinute) = _generatorZamowien.PobierzStatystyki();
			IleZamowienNaMinute = IleOperacjiNaMinute;
			IleCzasuNaZamowienie = SredniCzasOperacji;
			IleZamowienZmierzono = IleOperacji;

			CzyTrwaDodawanieZamowien = false;
			PostepOperacji.Zatrzymaj();
		}

		internal async Task ExportStatistics()
		{
			await Task.Run(() =>
			{
				var builder = new StringBuilder();
				builder
					.AppendLine("Statystyki generatora danych")
					.AppendLine($"Wersja Sfery: {WersjaSfery}")
					.AppendLine($"Wersja .NET: {WersjaDotnet}")					
					.AppendLine()
					.AppendLine($"\tZamówienia\tTowary")
					.AppendLine($"Ile pomiarów:\t{IleZamowienZmierzono}\t{IleTowarowZmierzono}")
					.AppendLine($"Średnio na operację:\t{IleCzasuNaZamowienie}\t{IleCzasuNaTowar}")
					.AppendLine($"Ile operacji na minutę:\t{IleZamowienNaMinute}\t{IleTowarowNaMinute}");

				File.WriteAllText("statystyki.txt", builder.ToString());
				var processStartInfo = new ProcessStartInfo("notepad.exe", "statystyki.txt")
				{
					UseShellExecute = false
				};
				Process.Start(processStartInfo);
			});
		}

		internal async Task PobierzUzytkownikow(DanePolaczenia danePolaczenia)
		{
			var uzytkownicy = await PobieranieDanych.PobierzUzytkownikow(danePolaczenia);

			Uzytkownicy = uzytkownicy;
			GotowyDoLogowania = true;
			LogOperacji.Zaloguj("Pobrano listę użytkowników.");
		}

		internal void PolaczZeSfera(DaneDoUruchomieniaSfery daneDoUruchomieniaSfery)
		{
			// Uwaga: tworzenie uchwytu musi się odbywać w wątku UI.
			bool edytowalneDanePolaczeniaBackup = EdytowalneDanePolaczenia;
			try
			{
				if (UchwytDoSfery == null)
				{
					GotowyDoLogowania = false;
					EdytowalneDanePolaczenia = false;
					CzyTrwaUruchamianieSfery = true;

					var postep = new PostepLadowaniaSfery(PostepOperacji);
					
					var stopwatch = Stopwatch.StartNew();
					UchwytDoSfery = Uchwyty.UtworzNowy(daneDoUruchomieniaSfery, postep);
					stopwatch.Stop();
					
					CzySferaJestUruchomiona = true;
					
					LogOperacji.Zaloguj($"Utworzono uchwyt sferyczny w czasie {stopwatch.Elapsed}.");
				}
			}
			catch (Exception e)
			{
				ExceptionHandler?.Invoke(e);
				GotowyDoLogowania = true;
				EdytowalneDanePolaczenia = edytowalneDanePolaczeniaBackup;
			}
			finally
			{
				CzyTrwaUruchamianieSfery = false;
			}
		}
	}
}