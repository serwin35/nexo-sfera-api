using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PrzepisywaniePowiazanWiadomosci.Exceptions;
using PrzepisywaniePowiazanWiadomosci.Models;

namespace PrzepisywaniePowiazanWiadomosci.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly ManagerPowiazanWiadomosciPocztowych _manager;

        public MainViewModel()
        {
            UruchomSfereIPobierzKontaPocztoweCommand = new Command(ExecuteUruchomSfereIPobierzKontaPocztowe);
            PrzepiszPowiazaniaWiadomosciPocztowychCommand = new Command(ExecutePrzepiszPowiazaniaWiadomosciPocztowych);
            PokazWiadomoscPocztowaCommand = new Command(ExecutePokazWiadomoscPocztowa);
            ZamykanieOknaCommand = new Command(ExecuteZamykanieOkna);
            AutouzupelnianieCommand = new Command(ExecuteAutouzupelnianie);
            WyczyscAutouzupelnianieCommand = new Command(ExecuteWyczyscAutouzupelnianie);

            _manager = new ManagerPowiazanWiadomosciPocztowych();
        }

        public ICommand UruchomSfereIPobierzKontaPocztoweCommand { get; set; }
        private void ExecuteUruchomSfereIPobierzKontaPocztowe(object parameter)
        {
            var passwordBoxy = parameter as object[];
            var hasloNexoPasswordBox = passwordBoxy[0] as PasswordBox;
            var hasloSerwerPasswordBox = passwordBoxy[1] as PasswordBox;

            Thread okienkoLaczeniaZeSferaThread = null;
            LaczenieZeSferaWindow okienkoLaczeniaZeSfera = null;
            Dispatcher dispatcher = null;
            string komunikat = String.Empty;
            try
            {
                if (!_manager.UruchomionoSfere)
                {
                    okienkoLaczeniaZeSferaThread = new Thread(new ThreadStart(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                        dispatcher = Dispatcher.CurrentDispatcher;
                        okienkoLaczeniaZeSfera = new LaczenieZeSferaWindow();
                        okienkoLaczeniaZeSfera.Closed += (s, e) =>
                           dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);

                        okienkoLaczeniaZeSfera.Show();
                        Dispatcher.Run();
                    }));

                    okienkoLaczeniaZeSferaThread.SetApartmentState(ApartmentState.STA);
                    okienkoLaczeniaZeSferaThread.IsBackground = true;
                    okienkoLaczeniaZeSferaThread.Start();
                }

                var kontaPocztowe = _manager.PolaczSieZeSferaIPobierzDostepneKontaPocztowe(
                    Login,
                    hasloNexoPasswordBox?.Password,
                    Serwer,
                    Baza,
                    AutentykacjaWindows,
                    NazwaUzytkownikaSerwera,
                    hasloSerwerPasswordBox?.Password);

                KontaPocztowe = new ObservableCollection<UproszczoneKontoPocztowe>(kontaPocztowe);
                PolaczonoZeSfera = true;
                komunikat = "Połączono się ze Sferą i pobrano konta pocztowe.";
            }
            catch (SferaException ex)
            {
                PolaczonoZeSfera = false;
                komunikat = ex.Message;
            }
            finally
            {
                if (dispatcher != null)
                {
                    dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            okienkoLaczeniaZeSfera?.Close();
                        }),
                        DispatcherPriority.Background);
                }

                MessageBox.Show(komunikat);
            }
        }

        public ICommand PrzepiszPowiazaniaWiadomosciPocztowychCommand { get; set; }
        private void ExecutePrzepiszPowiazaniaWiadomosciPocztowych(object parameter)
        {
            Task.Factory.StartNew(() =>
            {
                PrzepiszPowiazaniaWiadomosciPocztowych();
            });
        }

        private void PrzepiszPowiazaniaWiadomosciPocztowych()
        {
            try
            {
                string komunikat = String.Empty;
                TrwaPrzepisywaniePowiazan = true;
                PrzetworzoneWiadomosciPocztowe = _manager.PrzepiszPowiazaniaWiadomosciPocztowych(KontoZrodlowe, KontoDocelowe);
                TrwaPrzepisywaniePowiazan = false;
                if (PrzetworzoneWiadomosciPocztowe.Count() != 0)
                {
                    MessageBox.Show($"Przepisano powiązania dla {PrzetworzoneWiadomosciPocztowe.Count()} wiadomości.");
                }
                else
                {
                    MessageBox.Show("Nie znaleziono wiadomości, dla których można przepisać powiązania.");
                }
            }
            catch (SferaException ex)
            {
                TrwaPrzepisywaniePowiazan = false;
                MessageBox.Show(ex.Message);
            }
        }

        public ICommand PokazWiadomoscPocztowaCommand { get; set; }
        private void ExecutePokazWiadomoscPocztowa(object parameter)
        {
            var wiadomoscPocztowa = parameter as UproszczonaWiadomoscPocztowa;
            _manager.PokazWiadomoscPocztowaWOknie(wiadomoscPocztowa);
        }

        public ICommand ZamykanieOknaCommand { get; set; }
        private void ExecuteZamykanieOkna(object parameter)
        {
            _manager.RozlaczSfere();
        }

        public ICommand AutouzupelnianieCommand { get; set; }
        private void ExecuteAutouzupelnianie(object parameter)
        {
            var passwordBox = parameter as PasswordBox;
            Login = "Szef";
            passwordBox.Password = "1";
            Serwer = "(local)";
            Baza = "Nexo_Test";
            AutentykacjaWindows = true;
        }

        public ICommand WyczyscAutouzupelnianieCommand { get; set; }
        private void ExecuteWyczyscAutouzupelnianie(object parameter)
        {
            var passwordBox = parameter as PasswordBox;
            Login = String.Empty;
            passwordBox.Password = String.Empty;
            Serwer = String.Empty;
            Baza = String.Empty;
            AutentykacjaWindows = false;
        }
    }
}
