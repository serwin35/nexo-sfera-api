using InsERT.Moria.Sfera;
using System;
using System.Linq;
using System.Windows;

namespace GeneratorDanych
{
	public partial class MainWindow : Window
	{
		private readonly DanePolaczenia _daneOdebraneZInsLauncher;

		public MainWindow()
		{
			InitializeComponent();

			WlaczObslugeWyjatkow();

			ViewModel = new MainWindowViewModel
			{
				ExceptionHandler = HandleException
			};

			DataContext = ViewModel;
			_daneOdebraneZInsLauncher = OdbierzDanePolaczeniaZInsLauncher();

			InicjalizujDanePolaczenia();

			ViewModel.LogOperacji.Operacje.CollectionChanged += Operacje_CollectionChanged;
		}

		public MainWindowViewModel ViewModel { get; }

		private static void HandleException(Exception e)
		{
			_ = MessageBox.Show(e.ToString());
		}

		private static DanePolaczenia OdbierzDanePolaczeniaZInsLauncher()
		{
			var commandLineArguments = Environment.GetCommandLineArgs();
			if (commandLineArguments != null && commandLineArguments.Contains(@"/UruchomionePrzezInsLauncher"))
			{
				//pobieramy parametry podane przez Launcher
				return DanePolaczenia.Odbierz();
			}

			return null;
		}

		private async void ButtonAddDocuments_1_Click(object sender, RoutedEventArgs e)
		{
			await ViewModel.DodajZamowienia(1);
		}

		private async void ButtonAddDocuments_10_Click(object sender, RoutedEventArgs e)
		{
			await ViewModel.DodajZamowienia(10);
		}

		private async void ButtonAddDocuments_100_Click(object sender, RoutedEventArgs e)
		{
			await ViewModel.DodajZamowienia(100);
		}

		private async void ButtonAddItems_1_Click(object sender, RoutedEventArgs e)
		{
			await ViewModel.DodajTowary(1);
		}

		private async void ButtonAddItems_10_Click(object sender, RoutedEventArgs e)
		{
			await ViewModel.DodajTowary(10);
		}

		private async void ButtonAddItems_100_Click(object sender, RoutedEventArgs e)
		{
			await ViewModel.DodajTowary(100);
		}

		private void ButtonClearLog_Click(object sender, RoutedEventArgs e)
		{
			ViewModel.LogOperacji.Operacje.Clear();
		}

		private async void ButtonGetUsers_Click(object sender, RoutedEventArgs e)
		{
			var danePolaczenia = PobierzDanePolaczenia();

			await ViewModel.PobierzUzytkownikow(danePolaczenia);
			ViewModel.LoginNexo = ViewModel.Uzytkownicy.LastOrDefault();
		}

		private void ButtonLogin_Click(object sender, RoutedEventArgs e)
		{
			var danePolaczenia = PobierzDanePolaczenia();

			var daneDoUruchomieniaSfery = new DaneDoUruchomieniaSfery()
			{
				DanePolaczenia = danePolaczenia,
				LoginNexo = ViewModel.LoginNexo,
				HasloNexo = PasswordBoxNexo.Password,
				Produkt = InsERT.Mox.Product.ProductId.Subiekt
			};

			ViewModel.PolaczZeSfera(daneDoUruchomieniaSfery);
		}

		private void InicjalizujDanePolaczenia()
		{
			if (_daneOdebraneZInsLauncher == null)
			{
				var domyslneUstawienia = DomyslneUstawienia.Pobierz();

				ViewModel.SerwerSql = domyslneUstawienia.SerwerSql;
				ViewModel.BazaDanych = domyslneUstawienia.BazaDanych;
				if (domyslneUstawienia.UwierzytelnienieWindows)
				{
					ViewModel.UwierzytelnienieWindows = true;
				}
				else
				{
					ViewModel.UwierzytelnienieWindows = false;
					ViewModel.LoginSql = domyslneUstawienia.LoginSql;
					ViewModel.HasloSql = domyslneUstawienia.HasloSql;
				}

				ViewModel.EdytowalneDanePolaczenia = true;
			}
			else
			{
				ViewModel.EdytowalneDanePolaczenia = false;
				Loaded += MainWindow_Loaded;
			}
		}

		private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			if (_daneOdebraneZInsLauncher != null)
			{
				await ViewModel.PobierzUzytkownikow(_daneOdebraneZInsLauncher);
				ViewModel.LoginNexo = ViewModel.Uzytkownicy.LastOrDefault();
			}

			Loaded -= MainWindow_Loaded;
		}

		private void Operacje_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			ScrollOperations.ScrollToEnd();
		}

		private DanePolaczenia PobierzDanePolaczenia()
		{
			return _daneOdebraneZInsLauncher ?? UtworzDanePolaczenia();
		}

		private DanePolaczenia UtworzDanePolaczenia()
		{
			if (ViewModel.UwierzytelnienieWindows)
			{
				return DanePolaczenia.Jawne(ViewModel.SerwerSql, ViewModel.BazaDanych, ViewModel.UwierzytelnienieWindows);
			}
			return DanePolaczenia.Jawne(ViewModel.SerwerSql, ViewModel.BazaDanych, ViewModel.UwierzytelnienieWindows, ViewModel.LoginSql, PasswordBoxSqlServer.Password);
		}

		private void WlaczObslugeWyjatkow()
		{
			Dispatcher.UnhandledException += (sender, e) =>
			{
				HandleException(e.Exception);
				e.Handled = true;
			};
			AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
			{
				HandleException((Exception)e.ExceptionObject);
			};
		}

		private async void ButtonExportStats_Click(object sender, RoutedEventArgs e)
		{
			await ViewModel.ExportStatistics();
        }
    }
}