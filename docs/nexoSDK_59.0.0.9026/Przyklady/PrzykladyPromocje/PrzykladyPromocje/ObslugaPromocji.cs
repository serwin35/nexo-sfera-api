using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Promocje;
using InsERT.Moria.Sfera;
using System;
using System.Linq;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.Klienci;
using InsERT.Moria;
using InsERT.Moria.Waluty;
using InsERT.Moria.Kasa;

namespace PrzykladyPromocje
{
    public class ObslugaPromocji
    {
        private readonly Uchwyt _sfera;

        public ObslugaPromocji(Uchwyt uchwyt)
        {
            _sfera = uchwyt;
        }


        /// <summary>
        /// Dodaje promocję rabat procentowy 35% na grupę asortymentu o nazwie 'Pomadki', działającą w każdy poniedziałek, środę i piątek przez najbliższe 60 dni.
        /// </summary>
        public void DodajPromocjeGrupaAsortymentu()
        {
            IPromocje promocje = _sfera.PodajObiektTypu<IPromocje>();
            IGrupyAsortymentu grupyAsortymentu = _sfera.PodajObiektTypu<IGrupyAsortymentu>();

            using (IPromocja promocja = promocje.Utworz())
            {
                // podstawowe dane:
                promocja.Dane.Symbol = "PROMO_RABAT_35%";
                promocja.Dane.Nazwa = "Promocja rabat 35% na pomadki";

                // status promocji:
                promocja.Dane.Status = (byte)StatusPromocji.Aktywna;

                // harmonogram:
                KryteriumWyboruPromocji kryteriumHarmonogram = promocja.Dane.KryteriaWyboru.FirstOrDefault(k => k.PluginKryteriumWyboru == FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruOkresObowiazywania);
                if (kryteriumHarmonogram == null)
                {
                    kryteriumHarmonogram = new KryteriumWyboruPromocji();
                    promocja.Dane.KryteriaWyboru.Add(kryteriumHarmonogram);
                    kryteriumHarmonogram.PluginKryteriumWyboru = FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruOkresObowiazywania;
                }
                kryteriumHarmonogram.ParametrOkresuObowiazywania.HarmonogramWaznosci.RodzajHarmonogramu = (byte)RodzajHarmonogramu.Cotygodniowy;
                kryteriumHarmonogram.ParametrOkresuObowiazywania.HarmonogramWaznosci.DataPoczatkowa = DateTime.Now.Date;
                kryteriumHarmonogram.ParametrOkresuObowiazywania.HarmonogramWaznosci.DataKoncowa = DateTime.Now.Date.AddDays(60);
                kryteriumHarmonogram.ParametrOkresuObowiazywania.HarmonogramWaznosci.UstawAktywneDniTygodnia(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);

                // grupa asortymentu:
                KryteriumWyboruPromocji kryteriumAsortyment = promocja.Dane.KryteriaWyboru.FirstOrDefault(k => k.PluginKryteriumWyboru == FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruAsortymentu);
                if (kryteriumAsortyment == null)
                {
                    kryteriumAsortyment = new KryteriumWyboruPromocji();
                    promocja.Dane.KryteriaWyboru.Add(kryteriumAsortyment);
                    kryteriumAsortyment.PluginKryteriumWyboru = FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruAsortymentu;
                }
                GrupaAsortymentu grupaPomadki = grupyAsortymentu.Dane.Wszystkie().Where(g => g.Nazwa == "Pomadki").FirstOrDefault();
                if (grupaPomadki == null)
                    Console.WriteLine("Nie znaleziono grupy o nazwie 'Pomadki'.");
                else
                    kryteriumAsortyment.ParametrWyboruAsortymentu.Grupy.Add(grupaPomadki);

                // wynik promocji - rabat procentowy 35%:
                promocja.Dane.Wartosc.PluginWartosci = FunkcjeWartosciPromocji.FunkcjaWartosciPromocjiProcent;
                promocja.Dane.Wartosc.Procent.Wartosc = 0.35m; // 35%

                // zapis promocji:
                if (promocja.Zapisz())
                    Console.WriteLine("Zapisano promocję z rabatem procentowym 35%.");
                else
                    promocja.WypiszBledy();
            }
        }

        /// <summary>
        /// Dodaje promocję rabat kwotowy 10PLN na grupę klientów 'Drogerie' działającą każdego 1,5,10,15,20,25 i 30 dnia miesiąca od czerwca do września.
        /// </summary>
        public void DodajPromocjeGrupaKlienta()
        {
            IPromocje promocje = _sfera.PodajObiektTypu<IPromocje>();
            IGrupy grupyKlientow = _sfera.PodajObiektTypu<IGrupy>();
            IWaluty waluty = _sfera.PodajObiektTypu<IWaluty>();

            using (IPromocja promocja = promocje.Utworz())
            {
                // podstawowe dane:
                promocja.Dane.Symbol = "PROMO_RABAT_10PLN";
                promocja.Dane.Nazwa = "Promocja rabat 10PLN dla drogerii";

                // status promocji:
                promocja.Dane.Status = (byte)StatusPromocji.Aktywna;

                // harmonogram:
                KryteriumWyboruPromocji kryteriumHarmonogram = promocja.Dane.KryteriaWyboru.FirstOrDefault(k => k.PluginKryteriumWyboru == FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruOkresObowiazywania);
                if (kryteriumHarmonogram == null)
                {
                    kryteriumHarmonogram = new KryteriumWyboruPromocji();
                    promocja.Dane.KryteriaWyboru.Add(kryteriumHarmonogram);
                    kryteriumHarmonogram.PluginKryteriumWyboru = FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruOkresObowiazywania;
                }
                kryteriumHarmonogram.ParametrOkresuObowiazywania.HarmonogramWaznosci.RodzajHarmonogramu = (byte)RodzajHarmonogramu.Comiesięczny;
                kryteriumHarmonogram.ParametrOkresuObowiazywania.HarmonogramWaznosci.UstawAktywneMiesiace(Miesiace.Czerwiec, Miesiace.Lipiec, Miesiace.Sierpien, Miesiace.Wrzesien);
                kryteriumHarmonogram.ParametrOkresuObowiazywania.HarmonogramWaznosci.UstawAktywneDniMiesiaca(1, 5, 10, 15, 20, 25, 30);

                // grupa klientów:
                KryteriumWyboruPromocji kryteriumPodmiot = promocja.Dane.KryteriaWyboru.FirstOrDefault(k => k.PluginKryteriumWyboru == FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruPodmiotu);
                if (kryteriumPodmiot == null)
                {
                    kryteriumPodmiot = new KryteriumWyboruPromocji();
                    promocja.Dane.KryteriaWyboru.Add(kryteriumPodmiot);
                    kryteriumPodmiot.PluginKryteriumWyboru = FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruPodmiotu;
                }
                Grupa grupaDrogerie = grupyKlientow.Dane.Wszystkie().Where(g => g.Nazwa == "Drogerie").FirstOrDefault();
                if (grupaDrogerie == null)
                    Console.WriteLine("Nie znaleziono grupy o nazwie 'Drogerie'.");
                else
                    kryteriumPodmiot.ParametrWyboruPodmiotu.Grupy.Add(grupaDrogerie);

                // wynik promocji - rabat 10PLN
                promocja.Dane.Wartosc.PluginWartosci = FunkcjeWartosciPromocji.FunkcjaWartosciPromocjiKwota;
                promocja.Dane.Wartosc.Kwota.Wartosc = 10m;
                promocja.Dane.Wartosc.Kwota.Waluta = waluty.Dane.Wszystkie().Where(w => w.Symbol == "PLN").FirstOrDefault();

                // zapis promocji:
                if (promocja.Zapisz())
                    Console.WriteLine("Zapisano promocję z rabatem kwotowym 10PLN.");
                else
                    promocja.WypiszBledy();
            }
        }


        public void DodajPromocjeZGratisami()
        {
            IPromocje promocje = _sfera.PodajObiektTypu<IPromocje>();
            IAsortymenty asortymenty = _sfera.PodajObiektTypu<IAsortymenty>();
            IFormyPlatnosci formyPlatnosci = _sfera.PodajObiektTypu<IFormyPlatnosci>();
            IWaluty waluty = _sfera.PodajObiektTypu<IWaluty>();

            using (IPromocja promocja = promocje.Utworz())
            {
                // podstawowe dane:
                promocja.Dane.Symbol = "PROMO_DODATKI";
                promocja.Dane.Nazwa = "Promocja z prezentami przy zapłacie kartą powyżej wartości dokumentu 500PLN";

                // status promocji:
                promocja.Dane.Status = (byte)StatusPromocji.Aktywna;

                // kryterium forma płatności:
                KryteriumWyboruPromocji kryteriumFormaPlatnosci = promocja.Dane.KryteriaWyboru.FirstOrDefault(k => k.PluginKryteriumWyboru == FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruRodzajPlatnosci);
                if (kryteriumFormaPlatnosci == null)
                {
                    kryteriumFormaPlatnosci = new KryteriumWyboruPromocji();
                    promocja.Dane.KryteriaWyboru.Add(kryteriumFormaPlatnosci);
                    kryteriumFormaPlatnosci.PluginKryteriumWyboru = FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruRodzajPlatnosci;
                }
                FormaPlatnosci kartaPlatnicza = formyPlatnosci.Dane.Wszystkie().Where(fp => fp.Nazwa == "Karta płatnicza").FirstOrDefault();
                if (kartaPlatnicza == null)
                    Console.WriteLine("Nie znaleziono formy płatności o nazwie 'Karta płatnicza'.");
                else
                    kryteriumFormaPlatnosci.ParametrRodzajuPlatnosci.FormyPlatnosci.Add(kartaPlatnicza);

                // kryterium wartość asortymentu:
                KryteriumWyboruPromocji kryteriumWartoscDokumentu = promocja.Dane.KryteriaWyboru.FirstOrDefault(k => k.PluginKryteriumWyboru == FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruWartosciDokumentu);
                if (kryteriumWartoscDokumentu == null)
                {
                    kryteriumWartoscDokumentu = new KryteriumWyboruPromocji();
                    promocja.Dane.KryteriaWyboru.Add(kryteriumWartoscDokumentu);
                    kryteriumWartoscDokumentu.PluginKryteriumWyboru = FunkcjeKryteriumWyboru.FunkcjaKryteriumWyboruWartosciDokumentu;
                }
                kryteriumWartoscDokumentu.ParametrWartosciDokumentu.Wartosc = 500m;
                kryteriumWartoscDokumentu.ParametrWartosciDokumentu.Waluta = waluty.Dane.Wszystkie().Where(w => w.Symbol == "PLN").FirstOrDefault();

                // wynik promocji - dodatkowy asortyment:
                promocja.Dane.Wartosc.PluginWartosci = FunkcjeWartosciPromocji.FunkcjaWartosciPromocjiDodatek;
                Asortyment banaw200 = asortymenty.Dane.Wszystkie().Where(a => a.Symbol == "BANAW200").FirstOrDefault();
                if (banaw200 == null)
                    Console.WriteLine("Nie znaleziono asortymentu o symbolu 'BANAW200'.");
                else
                {
                    DodatkowyAsortyment dodatekGratis = new DodatkowyAsortyment();
                    promocja.Dane.Wartosc.Dodatek.DodatkoweAsortymenty.Add(dodatekGratis);
                    dodatekGratis.Gratis = true;
                    dodatekGratis.Wartosc = 0m;
                    dodatekGratis.Ilosc = 2m;
                    dodatekGratis.Asortyment = banaw200;
                    dodatekGratis.JednostkaMiaryAsortymentu = banaw200.PodstawowaJednostkaMiaryAsortymentu;
                }
                Asortyment zelalgi = asortymenty.Dane.Wszystkie().Where(a => a.Symbol == "ZELALGI").FirstOrDefault();
                if (zelalgi == null)
                    Console.WriteLine("Nie znaleziono asortymentu o symbolu 'ZELALGI'");
                else
                {
                    DodatkowyAsortyment dodatekGratis = new DodatkowyAsortyment();
                    promocja.Dane.Wartosc.Dodatek.DodatkoweAsortymenty.Add(dodatekGratis);
                    dodatekGratis.Gratis = false;
                    dodatekGratis.Wartosc = 0.01m;
                    dodatekGratis.Waluta = waluty.Dane.Wszystkie().Where(w => w.Symbol == "PLN").FirstOrDefault();
                    dodatekGratis.Ilosc = 1m;
                    dodatekGratis.Asortyment = zelalgi;
                    dodatekGratis.JednostkaMiaryAsortymentu = zelalgi.PodstawowaJednostkaMiaryAsortymentu;
                }

                // zapis promocji:
                if (promocja.Zapisz())
                    Console.WriteLine("Zapisano promocję z prezentami.");
                else
                    promocja.WypiszBledy();
            }
        }

        public void ZamykaniePromocji()
        {
            IPromocje promocje = _sfera.PodajObiektTypu<IPromocje>();
            Promocja rabat35Procent = promocje.Dane.Wszystkie().Where(p => p.Symbol == "PROMO_RABAT_35%").FirstOrDefault();
            if (rabat35Procent == null)
                Console.WriteLine("Nie znaleziono promocji 'PROMO_RABAT_35%'.");
            else
            {
                using (IPromocja promocja = promocje.Znajdz(rabat35Procent))
                {
                    promocja.Dane.Status = (byte)StatusPromocji.Zakonczona;

                    // zapis promocji:
                    if (promocja.Zapisz())
                        Console.WriteLine("Zamknięto promocję z rabatem procentowym 35%.");
                    else
                        promocja.WypiszBledy();
                }
            }
        }
    }
}
