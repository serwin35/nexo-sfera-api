using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;

namespace RozwiazanieWlasne
{
    public class SlownikiWlasneTester
    {
        public readonly ISlownikiWlasne _slownikiWlasne;

        public SlownikiWlasneTester(ISlownikiWlasne slownikiWlasne)
        {
            if (slownikiWlasne == null)
            {
                throw new ArgumentNullException(nameof(slownikiWlasne));
            }
            _slownikiWlasne = slownikiWlasne;
        }

        public void Test()
        {
            PobierzNazwyIOpisySlownikowWlasnych_Test();
            PobierzNazwyIOpisySlownikowWlasnychSql_Test();
            PobierzNazwyITypySlownikowSystemowych_Test();
        }

        private void PobierzNazwyIOpisySlownikowWlasnych_Test()
        {
            Console.WriteLine("         Słowniki własne");
            foreach (Tuple<int, string, string> slownikWlasnyInfo in _slownikiWlasne.PobierzNazwyIOpisySlownikowWlasnych())
            {
                int idSlownika = slownikWlasnyInfo.Item1;
                string nazwaSlownika = slownikWlasnyInfo.Item2;
                string opisSlownika = slownikWlasnyInfo.Item3;
                Console.WriteLine("             IdSlownika={0}, Nazwa={1}, Opis={2}", idSlownika, nazwaSlownika, opisSlownika);

                //definicje slownika wlasnego mozna pobrac przy uzyciu jego identyfikatora
                ISlownikoweZrodloDanych<int, PozycjaSlownikaWlasnego> slownikWlasny = _slownikiWlasne.PobierzDefinicjeSlownikaWlasnego(idSlownika);
                //...lub nazwy
                slownikWlasny = _slownikiWlasne.PobierzDefinicjeSlownikaWlasnego(nazwaSlownika);

                //pobieramy wszystkie wartosci slownika
                foreach (PozycjaSlownikaWlasnego item in slownikWlasny.UtworzZapytanieLinqTypowane())
                {
                }
                //pobieramy tylko widoczne w UI wartosci slownika
                foreach (PozycjaSlownikaWlasnego item in slownikWlasny.UtworzZapytanieFiltrowaneLinqTypowane())
                {
                }
            }
        }

        private void PobierzNazwyIOpisySlownikowWlasnychSql_Test()
        {
            Console.WriteLine("         Słowniki własne SQL");
            foreach (Tuple<int, string, string> slownikWlasnySqlInfo in _slownikiWlasne.PobierzNazwyIOpisySlownikowWlasnychSql())
            {
                int idSlownika = slownikWlasnySqlInfo.Item1;
                string nazwaSlownika = slownikWlasnySqlInfo.Item2;
                string opisSlownika = slownikWlasnySqlInfo.Item3;
                Console.WriteLine("             IdSlownika={0}, Nazwa={1}, Opis={2}", idSlownika, nazwaSlownika, opisSlownika);

                //definicje slownika wlasnego SQL mozna pobrac przy uzyciu jego identyfikatora
                ISlownikoweZrodloDanych slownikWlasnySql = _slownikiWlasne.PobierzDefinicjeSlownikaWlasnegoSql(idSlownika);
                //...lub nazwy
                slownikWlasnySql = _slownikiWlasne.PobierzDefinicjeSlownikaWlasnegoSql(nazwaSlownika);

                if (slownikWlasnySql.TypKlucza == typeof(int))
                {
                    ISlownikoweZrodloDanych<int> slownikWlasnySqlOKluczuTypuInt = (ISlownikoweZrodloDanych<int>)slownikWlasnySql;
                    //pobieramy wszystkie wartosci slownika
                    foreach (ElementSlownikowegoZrodlaDanych<int> item in slownikWlasnySqlOKluczuTypuInt.UtworzZapytanieLinq())
                    {
                    }
                    //pobieramy tylko widoczne w UI wartosci slownika
                    foreach (ElementSlownikowegoZrodlaDanych<int> item in slownikWlasnySqlOKluczuTypuInt.UtworzZapytanieFiltrowaneLinq())
                    {
                    }
                }
                else if (slownikWlasnySql.TypKlucza == typeof(Guid))
                {
                    ISlownikoweZrodloDanych<Guid> slownikWlasnySqlOKluczuTypuGuid = (ISlownikoweZrodloDanych<Guid>)slownikWlasnySql;
                    //pobieramy wszystkie wartosci slownika
                    foreach (ElementSlownikowegoZrodlaDanych<Guid> item in slownikWlasnySqlOKluczuTypuGuid.UtworzZapytanieLinq())
                    {
                    }
                    //pobieramy tylko widoczne w UI wartosci slownika
                    foreach (ElementSlownikowegoZrodlaDanych<Guid> item in slownikWlasnySqlOKluczuTypuGuid.UtworzZapytanieFiltrowaneLinq())
                    {
                    }
                }
            }
        }

        private void PobierzNazwyITypySlownikowSystemowych_Test()
        {
            Console.WriteLine("         Słowniki systemowe");
            foreach (Tuple<int, string, Type> slownikSystemowyInfo in _slownikiWlasne.PobierzNazwyITypySlownikowSystemowych())
            {
                int idSlownika = slownikSystemowyInfo.Item1;
                string nazwaSlownika = slownikSystemowyInfo.Item2;
                Type typSlownika = slownikSystemowyInfo.Item3;
                Console.WriteLine("             IdSlownika={0}, Nazwa={1}, Typ={2}", idSlownika, nazwaSlownika, typSlownika);

                switch (idSlownika)
                {
                    case IdentyfikatorSlownikaSystemowego.Waluty:
                        ISlownikoweZrodloDanych<Guid, Waluta> slownikWalut = _slownikiWlasne.PobierzDefinicjeSlownikaWalut();
                        //pobieramy wszystkie wartosci slownika
                        foreach (Waluta item in slownikWalut.UtworzZapytanieLinqTypowane())
                        {
                        }
                        //dla słowników systemowych zapytanie filtrowane zwraca te same dane, co zapytanie pełne
                        foreach (Waluta item in slownikWalut.UtworzZapytanieFiltrowaneLinqTypowane())
                        {
                        }
                        break;
                    case IdentyfikatorSlownikaSystemowego.Magazyny:
                        ISlownikoweZrodloDanych<int, Magazyn> slownikMagazynow = _slownikiWlasne.PobierzDefinicjeSlownikaMagazynow();
                        //pobieramy wszystkie wartosci slownika
                        foreach (Magazyn item in slownikMagazynow.UtworzZapytanieLinqTypowane())
                        {
                        }
                        //dla słowników systemowych zapytanie filtrowane zwraca te same dane, co zapytanie pełne
                        foreach (Magazyn item in slownikMagazynow.UtworzZapytanieFiltrowaneLinqTypowane())
                        {
                        }
                        break;
                    case IdentyfikatorSlownikaSystemowego.RachunkiBankowe:
                        ISlownikoweZrodloDanych<int, RachunekBankowy> slownikRachunkow = _slownikiWlasne.PobierzDefinicjeSlownikaRachunkowBankowych();
                        //pobieramy wszystkie wartosci slownika
                        foreach (RachunekBankowy item in slownikRachunkow.UtworzZapytanieLinqTypowane())
                        {
                        }
                        //dla słowników systemowych zapytanie filtrowane zwraca te same dane, co zapytanie pełne
                        foreach (RachunekBankowy item in slownikRachunkow.UtworzZapytanieFiltrowaneLinqTypowane())
                        {
                        }
                        break;
                    default:
                        throw new NotSupportedException(idSlownika.ToString());
                }
            }
        }
    }
}
