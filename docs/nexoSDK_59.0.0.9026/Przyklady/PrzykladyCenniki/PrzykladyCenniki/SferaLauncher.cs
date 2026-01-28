using InsERT.Moria.Sfera;
using System;

namespace PrzykladyCenniki
{
    public class SferaLauncher
    {
        private string _serwer;
        private string _baza;
        private string _login;
        private string _haslo;
        private string _uzytkownikSerwera;
        private string _hasloSerwera;

        public SferaLauncher(string serwer, string baza, string login, string haslo, string uzytkownikSerwera, string hasloSerwera)
        {
            _serwer = serwer ?? throw new ArgumentNullException(nameof(serwer));
            _baza = baza ?? throw new ArgumentNullException(nameof(baza));
            _login = login ?? throw new ArgumentNullException(nameof(login));
            _haslo = haslo ?? throw new ArgumentNullException(nameof(haslo));
            _uzytkownikSerwera = uzytkownikSerwera ?? throw new ArgumentNullException(nameof(uzytkownikSerwera));
            _hasloSerwera = hasloSerwera ?? throw new ArgumentNullException(nameof(hasloSerwera));
        }

        public Uchwyt UruchomSfere()
        {
            DanePolaczenia danePolaczenia = DanePolaczenia.Jawne(_serwer, _baza, uzytkownikSerwera: _uzytkownikSerwera, hasloUzytkownikaSerwera: _hasloSerwera);
            MenedzerPolaczen mp = new MenedzerPolaczen();
            Uchwyt sfera = mp.Polacz(danePolaczenia, InsERT.Mox.Product.ProductId.Subiekt);
            if (!sfera.ZalogujOperatora(_login, _haslo))
                throw new ArgumentException("Nieprawidłowa nazwa lub hasło użytkownika.");
            return sfera;
        }
    }
}
