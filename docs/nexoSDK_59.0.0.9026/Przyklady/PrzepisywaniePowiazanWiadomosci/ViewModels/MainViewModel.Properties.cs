using System.Collections.Generic;
using System.Collections.ObjectModel;
using PrzepisywaniePowiazanWiadomosci.Models;

namespace PrzepisywaniePowiazanWiadomosci.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private ObservableCollection<UproszczoneKontoPocztowe> _kontaPocztowe;
        private UproszczoneKontoPocztowe _kontoZrodlowe;
        private UproszczoneKontoPocztowe _kontoDocelowe;
        private string _login;
        private string _serwer;
        private string _baza;
        private bool _autentykacjaWindows;
        private string _nazwaUzytkownikaSerwera;
        private bool _polaczonoZeSfera;
        private double _progresPrzepisywaniaPowiazan;
        private bool _trwaPrzepisywaniePowiazan;
        private IEnumerable<UproszczonaWiadomoscPocztowa> _przetworzoneWiadomosciPocztowe;

        public ObservableCollection<UproszczoneKontoPocztowe> KontaPocztowe
        {
            get => _kontaPocztowe;
            set
            {
                _kontaPocztowe = value;
                OnPropertyChanged(nameof(KontaPocztowe));
            }
        }

        public UproszczoneKontoPocztowe KontoZrodlowe
        {
            get => _kontoZrodlowe;
            set
            {
                _kontoZrodlowe = value;
                OnPropertyChanged(nameof(KontoZrodlowe));
            }
        }

        public UproszczoneKontoPocztowe KontoDocelowe
        {
            get => _kontoDocelowe;
            set
            {
                _kontoDocelowe = value;
                OnPropertyChanged(nameof(KontoDocelowe));
            }
        }

        public string Login
        {
            get => _login;
            set
            {
                _login = value;
                OnPropertyChanged(nameof(Login));
            }
        }

        public string Serwer
        {
            get => _serwer;
            set
            {
                _serwer = value;
                OnPropertyChanged(nameof(Serwer));
            }
        }

        public string Baza
        {
            get => _baza;
            set
            {
                _baza = value;
                OnPropertyChanged(nameof(Baza));
            }
        }

        public bool AutentykacjaWindows
        {
            get => _autentykacjaWindows;
            set
            {
                _autentykacjaWindows = value;
                OnPropertyChanged(nameof(AutentykacjaWindows));
            }
        }

        public string NazwaUzytkownikaSerwera
        {
            get => _nazwaUzytkownikaSerwera;
            set
            {
                _nazwaUzytkownikaSerwera = value;
                OnPropertyChanged(nameof(NazwaUzytkownikaSerwera));
            }
        }

        public bool PolaczonoZeSfera
        {
            get => _polaczonoZeSfera;
            set
            {
                _polaczonoZeSfera = value;
                OnPropertyChanged(nameof(PolaczonoZeSfera));
            }
        }

        public double ProgresPrzepisywaniaPowiazan
        {
            get => _progresPrzepisywaniaPowiazan;
            set
            {
                _progresPrzepisywaniaPowiazan = value;
                OnPropertyChanged(nameof(ProgresPrzepisywaniaPowiazan));
            }
        }

        public bool TrwaPrzepisywaniePowiazan
        {
            get => _trwaPrzepisywaniePowiazan;
            set
            {
                _trwaPrzepisywaniePowiazan = value;
                OnPropertyChanged(nameof(TrwaPrzepisywaniePowiazan));
            }
        }

        public IEnumerable<UproszczonaWiadomoscPocztowa> PrzetworzoneWiadomosciPocztowe
        {
            get => _przetworzoneWiadomosciPocztowe;
            set
            {
                _przetworzoneWiadomosciPocztowe = value;
                OnPropertyChanged(nameof(PrzetworzoneWiadomosciPocztowe));
            }
        }
    }
}
