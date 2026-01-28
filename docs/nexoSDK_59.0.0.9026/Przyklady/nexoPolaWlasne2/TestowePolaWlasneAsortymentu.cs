using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Sfera;

namespace RozwiazanieWlasne
{
    public static class TestowePolaWlasneAsortymentu
    {
        public const string PoleTypuTekst = "pz tekst";
        public const string PoleTypuDlugiTekst = "pz długi tekst";
        public const string PoleTypuLiczbaCalkowita = "pz int";
        public const string PoleTypuLiczbaRzeczywista = "pz decimal";
        public const string PoleTypuWartoscLogiczna = "pz bool";
        public const string PoleTypuData = "pz data";
        public const string PoleTypuSlownikWlasny = "pz słownik";
        public const string PoleTypuSlownikWlasnySqlByInt = "pz słownik sql o kluczu Int";
        public const string PoleTypuSlownikWlasnySqlByGuid = "pz słownik sql o kluczu Guid";
        public const string PoleTypuSlownikSystemowyWalut = "pz waluta";
        public const string PoleTypuSlownikSystemowyMagazynow = "pz magazyn";
        public const string PoleTypuSlownikSystemowyRachunkowBankowych = "pz rachunek";

        public static void UpewnijSieCzyZdefiniowanoZaawansowanePolaWlasneAsortymentu(Uchwyt uchwyt)
        {
            var zaawansowanePolaWlasne = uchwyt.PodajObiektTypu<IZaawansowanePolaWlasne>();
            var slownikiWlasne = uchwyt.PodajObiektTypu<ISlownikiWlasne>();

            const string brakPolaErrorMessage = "W typie '{0}' nie zdefiniowano pola '{1}' (patrz instrukcja w pliku nexoPolaWlasne2.txt)";
            const string nieprawidlowyTypPolaErrorMessage = "Pole '{0}' musi być typu '{1}' (patrz instrukcja w pliku nexoPolaWlasne2.txt)";

            {
                const string nazwaPola = PoleTypuTekst;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else if (pole.Typ != TypSkalarnyZaawansowanegoPolaWlasnego.Tekst)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, TypSkalarnyZaawansowanegoPolaWlasnego.Tekst));
                }
            }

            {
                const string nazwaPola = PoleTypuDlugiTekst;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else if (pole.Typ != TypSkalarnyZaawansowanegoPolaWlasnego.DlugiTekst)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, TypSkalarnyZaawansowanegoPolaWlasnego.Tekst));
                }
            }

            {
                const string nazwaPola = PoleTypuLiczbaCalkowita;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else if (pole.Typ != TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita));
                }
            }

            {
                const string nazwaPola = PoleTypuLiczbaRzeczywista;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else if (pole.Typ != TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaRzeczywista)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaRzeczywista));
                }
            }

            {
                const string nazwaPola = PoleTypuWartoscLogiczna;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else if (pole.Typ != TypSkalarnyZaawansowanegoPolaWlasnego.WartoscLogiczna)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, TypSkalarnyZaawansowanegoPolaWlasnego.WartoscLogiczna));
                }
            }

            {
                const string nazwaPola = PoleTypuData;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else if (pole.Typ != TypSkalarnyZaawansowanegoPolaWlasnego.Data)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, TypSkalarnyZaawansowanegoPolaWlasnego.Data));
                }
            }

            {
                const string nazwaPola = PoleTypuSlownikWlasny;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else
                {
                    if (!pole.JestReferencjaDoSlownika || pole.RodzajSlownika != RodzajSlownikowegoZrodlaDanych.SlownikWlasny)
                    {
                        throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, RodzajSlownikowegoZrodlaDanych.SlownikWlasny));
                    }

                    var slownikWlasny = (ISlownikoweZrodloDanych<int>)pole.PobierzDefinicjeSlownika();
                    if (slownikWlasny.UtworzZapytanieLinq().Count() == 0)
                    {
                        throw new InvalidOperationException(string.Format("Pole '{0}' musi zawierać przynajmniej jedną pozycję (patrz instrukcja w pliku nexoPolaWlasne2.txt)", nazwaPola));
                    }
                }
            }

            {
                const string nazwaPola = PoleTypuSlownikWlasnySqlByInt;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else
                {
                    if (!pole.JestReferencjaDoSlownika || pole.RodzajSlownika != RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql)
                    {
                        throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql));
                    }
                    if (pole.Typ != TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita)
                    {
                        throw new InvalidOperationException(string.Format("Pole '{0}' musi mieć klucz obcy typu '{1}' (patrz instrukcja w pliku nexoPolaWlasne2.txt)", nazwaPola, TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita));
                    }
                    var slownikWlasnySql = (ISlownikoweZrodloDanych<int>)pole.PobierzDefinicjeSlownika();
                    if (slownikWlasnySql.UtworzZapytanieLinq().Count() == 0)
                    {
                        throw new InvalidOperationException(string.Format("Pole '{0}' musi zawierać przynajmniej jedną pozycję (patrz instrukcja w pliku nexoPolaWlasne2.txt)", nazwaPola));
                    }
                }
            }

            {
                const string nazwaPola = PoleTypuSlownikWlasnySqlByGuid;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else
                {
                    if (!pole.JestReferencjaDoSlownika || pole.RodzajSlownika != RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql)
                    {
                        throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql));
                    }
                    if (pole.Typ != TypSkalarnyZaawansowanegoPolaWlasnego.Guid)
                    {
                        throw new InvalidOperationException(string.Format("Pole '{0}' musi mieć klucz obcy typu '{1}' (patrz instrukcja w pliku nexoPolaWlasne2.txt)", nazwaPola, TypSkalarnyZaawansowanegoPolaWlasnego.Guid));
                    }
                    var slownikWlasnySql = (ISlownikoweZrodloDanych<Guid>)pole.PobierzDefinicjeSlownika();
                    if (slownikWlasnySql.UtworzZapytanieLinq().Count() == 0)
                    {
                        throw new InvalidOperationException(string.Format("Pole '{0}' musi zawierać przynajmniej jedną pozycję (patrz instrukcja w pliku nexoPolaWlasne2.txt)", nazwaPola));
                    }
                }
            }

            var nazwySlownikowSystemowych = slownikiWlasne.PobierzNazwySlownikowSystemowych().ToDictionary(x => x.Item1, x => x.Item2);
            const string nieprawidlowySlownikSystemowy = "Pole '{0}' musi być słownikiem systemowym typu '{1}' (patrz instrukcja w pliku nexoPolaWlasne2.txt)";

            {
                const string nazwaPola = PoleTypuSlownikSystemowyWalut;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else if (!pole.JestReferencjaDoSlownika || pole.RodzajSlownika != RodzajSlownikowegoZrodlaDanych.SlownikSystemowy)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, RodzajSlownikowegoZrodlaDanych.SlownikSystemowy));
                }
                else if (pole.PobierzDefinicjeSlownika().Id != IdentyfikatorSlownikaSystemowego.Waluty)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowySlownikSystemowy, nazwaPola, nazwySlownikowSystemowych[IdentyfikatorSlownikaSystemowego.Waluty]));
                }
            }

            {
                const string nazwaPola = PoleTypuSlownikSystemowyMagazynow;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else if (!pole.JestReferencjaDoSlownika || pole.RodzajSlownika != RodzajSlownikowegoZrodlaDanych.SlownikSystemowy)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, RodzajSlownikowegoZrodlaDanych.SlownikSystemowy));
                }
                else if (pole.PobierzDefinicjeSlownika().Id != IdentyfikatorSlownikaSystemowego.Magazyny)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowySlownikSystemowy, nazwaPola, nazwySlownikowSystemowych[IdentyfikatorSlownikaSystemowego.Magazyny]));
                }
            }

            {
                const string nazwaPola = PoleTypuSlownikSystemowyRachunkowBankowych;
                IZaawansowanePoleWlasne pole;
                if (!zaawansowanePolaWlasne.SprobujPobracZaawansowanePoleWlasne<Asortyment>(nazwaPola, out pole))
                {
                    throw new InvalidOperationException(string.Format(brakPolaErrorMessage, typeof(Asortyment).Name, nazwaPola));
                }
                else if (!pole.JestReferencjaDoSlownika || pole.RodzajSlownika != RodzajSlownikowegoZrodlaDanych.SlownikSystemowy)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowyTypPolaErrorMessage, nazwaPola, RodzajSlownikowegoZrodlaDanych.SlownikSystemowy));
                }
                else if (pole.PobierzDefinicjeSlownika().Id != IdentyfikatorSlownikaSystemowego.RachunkiBankowe)
                {
                    throw new InvalidOperationException(string.Format(nieprawidlowySlownikSystemowy, nazwaPola, nazwySlownikowSystemowych[IdentyfikatorSlownikaSystemowego.RachunkiBankowe]));
                }
            }
        }
    }
}
