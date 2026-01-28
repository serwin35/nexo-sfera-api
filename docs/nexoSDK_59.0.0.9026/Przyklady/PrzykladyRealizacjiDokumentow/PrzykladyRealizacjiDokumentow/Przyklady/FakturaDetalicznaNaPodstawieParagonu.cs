using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RealizacjeDokumentow.Przyklady
{
    public class FakturaDetalicznaNaPodstawieParagonu : RealizacjaBase
    {
        public FakturaDetalicznaNaPodstawieParagonu(Uchwyt sfera) : base(sfera) { }

        public void DodajFaktureDetalicznaNaPodstawieParagonuBezNIP()
        {
            DokumentDS paragon = DodajParagon(string.Empty, "DZSO100", "DZSO50", "PEWTK50", "WOFOREVER15O", "DZSO100", "ZELAQUA", "DZSO50");
            using (IDokumentSprzedazy fakturaDetaliczna = _sfera.DokumentySprzedazy().UtworzFaktureDetaliczna())
            {
                fakturaDetaliczna.PodmiotyDokumentu.UstawNabywceWedlugSymbolu("MIKOŁAJ"); // trzeba ustawić podmiot gdyż na paragonie nie było NIPu nabywcy więc program nie miał jak go odszukać.
                fakturaDetaliczna.WypelnijNaPodstawieDS(paragon);
                fakturaDetaliczna.Dane.Uwagi = "Faktura detaliczna na podstawie jednego paragonu bez NIP nabywcy.";
                if (fakturaDetaliczna.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano fakturę detaliczną o numerze {fakturaDetaliczna.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                }
                else
                {
                    fakturaDetaliczna.WypiszBledy();
                }
            }
        }

        public void DodajFaktureDetalicznaNaPodstawieParagonuZNIPBezKonsolidacji()
        {
            DokumentDS paragon = DodajParagon("894-56-53-56", "DZSO100", "DZSO50", "PEWTK50", "WOFOREVER15O", "DZSO100", "ZELAQUA", "DZSO50");
            using (IDokumentSprzedazy fakturaDetaliczna = _sfera.DokumentySprzedazy().UtworzFaktureDetaliczna())
            {
                // nie trzeba uzupełniać nabywcy - program sam go wyszuka po NIP podanym na paragonie
                fakturaDetaliczna.WypelnijNaPodstawieDS(paragon);
                fakturaDetaliczna.Dane.Uwagi = "Faktura detaliczna na podstawie jednego paragonu z NIP nabywcy.";
                if (fakturaDetaliczna.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano fakturę detaliczną o numerze {fakturaDetaliczna.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                }
                else
                {
                    fakturaDetaliczna.WypiszBledy();
                }
            }
        }

        public void DodajFaktureDetalicznaNaPodstawieParagonuZNIPZKonsolidacja()
        {
            DokumentDS paragon = DodajParagon("894-56-53-56", "DZSO100", "DZSO50", "PEWTK50", "WOFOREVER15O", "DZSO100", "ZELAQUA", "DZSO50");
            using (IDokumentSprzedazy fakturaDetaliczna = _sfera.DokumentySprzedazy().UtworzFaktureDetaliczna())
            {
                // nie trzeba uzupełniać nabywcy - program sam go wyszuka po NIP podanym na paragonie
                fakturaDetaliczna.WypelnijNaPodstawieDS(new DokumentDS[] { paragon }, paragon, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiary
                });
                fakturaDetaliczna.Dane.Uwagi = "Faktura detaliczna na podstawie jednego paragonu z NIP nabywcy z konsolidacją w jednostce miary.";
                if (fakturaDetaliczna.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano fakturę detaliczną o numerze {fakturaDetaliczna.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                }
                else
                {
                    fakturaDetaliczna.WypiszBledy();
                }
            }
        }

        public void DodajFaktureDetalicznaNaPodstawieWieluParagonowZNIPZKonsolidacja()
        {
            DokumentDS paragon1 = DodajParagon("894-56-53-56", "DZSO100", "DZSO50", "PEWTK50", "WOFOREVER15O", "DZSO100", "ZELAQUA", "DZSO50");
            DokumentDS paragon2 = DodajParagon("894-56-53-56", "DZSO100", "DZSO50", "PEWTK50", "WOFOREVER15O", "DZSO100", "ZELAQUA", "DZSO50");
            using (IDokumentSprzedazy fakturaDetaliczna = _sfera.DokumentySprzedazy().UtworzFaktureDetaliczna())
            {
                // nie trzeba uzupełniać nabywcy - program sam go wyszuka po NIP podanym na paragonie
                fakturaDetaliczna.WypelnijNaPodstawieDS(new DokumentDS[] { paragon1, paragon2 }, paragon1, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaBezWzgleduNaJednostkeMiary
                });
                fakturaDetaliczna.Dane.Uwagi = "Faktura detaliczna na podstawie wielu paragonów z NIP nabywcy z konsolidacją bez względu na jednostkę miary.";
                if (fakturaDetaliczna.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano fakturę detaliczną o numerze {fakturaDetaliczna.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                }
                else
                {
                    fakturaDetaliczna.WypiszBledy();
                }
            }
        }

        public void DodajFaktureDetalicznaDlaCzesciPozycjiZParagonowZNIPZKonsolidacja()
        {
            DokumentDS paragon1 = DodajParagon("894-56-53-56", "DZSO100", "DZSO50", "PEWTK50", "WOFOREVER15O", "DZSO100", "ZELAQUA", "DZSO50");
            DokumentDS paragon2 = DodajParagon("894-56-53-56", "DZSO100", "DZSO50", "PEWTK50", "WOFOREVER15O", "DZSO100", "ZELAQUA", "DZSO50");
            using (IDokumentSprzedazy fakturaDetaliczna = _sfera.DokumentySprzedazy().UtworzFaktureDetaliczna())
            {
                // nie trzeba uzupełniać nabywcy - program sam go wyszuka po NIP podanym na paragonie
                IEnumerable<PozycjaDokumentu> pozycjeDoFaktury = paragon1.Pozycje.Concat(paragon2.Pozycje).Where(p => p.AsortymentAktualny?.Grupa?.Nazwa == "Dezodoranty").ToArray();
                fakturaDetaliczna.WypelnijNaPodstawieDS(pozycjeDoFaktury, paragon1, new ParametryGrupowaniaDS()
                {
                    MetodaGrupowaniaPozycji = MetodaGrupowaniaPozycji.KonsolidacjaWJednostceMiaryICenie
                });
                fakturaDetaliczna.Dane.Uwagi = "Faktura detaliczna na podstawie wielu paragonów z NIP nabywcy dla wybranych pozycji z konsolidacją w jednostce miary i cenie.";
                if (fakturaDetaliczna.Zapisz())
                {
                    Console.WriteLine($"Poprawnie zapisano fakturę detaliczną o numerze {fakturaDetaliczna.Dane.NumerWewnetrzny.PelnaSygnatura}.");
                }
                else
                {
                    fakturaDetaliczna.WypiszBledy();
                }
            }
        }
    }
}
