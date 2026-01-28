using InsERT.Moria.EwidencjaVAT;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Pojazdy;
using InsERT.Moria.Rozszerzanie;
using System.Linq;

namespace SferaZdarzeniowaPrzyklady.Ksiegowosc
{
    /// <summary>
    /// Plugin ustawiający rodzaj odliczenia oraz wysokość kosztów po wpisaniu w opisie numeru rejestracyjnego pojazdu
    /// </summary>
    public class UstawienieRodzajuOdliczeniaZapisuWEwidencjiVATNaPodstawieSkojarzonegoPojazdu : KlientSferyZdarzeniowej<IZapisWEwidencjiVAT>
    {
        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IZapisWEwidencjiVAT> kontekst)
        {
            if (kontekst.Dane is DaneZapisuBadzPozycjiEwidencjiVAT daneZapisu
                && daneZapisu.Zapis.TypEwidencji == (byte)TypEwidencjiVAT.Zakup
                && kontekst.NazwaWlasciwosci == nameof(DaneZapisuBadzPozycjiEwidencjiVAT.Opis))
            {
                var pojazd = kontekst.Uchwyt.PodajObiektTypu<IPojazdy>().Dane.Wszystkie().FirstOrDefault(p => daneZapisu.Opis.Contains(p.NumerRejestracyjny));

                if (pojazd != null)
                {
                    daneZapisu.Zapis.PelneOdliczenieVATPojazdow = pojazd.PelneOdliczenieVAT(daneZapisu.Zapis.DataWystawienia);

                    if (!daneZapisu.Zapis.PelneOdliczenieVATPojazdow.Value)
                    {
                        switch ((TypUzytkownikaPojazdu)pojazd.TypUzytkownika)
                        {
                            case TypUzytkownikaPojazdu.Firma:
                                daneZapisu.Zapis.SposobNaliczaniaKwotyDoOdliczenia = (byte)DoUjeciaWKosztach.SiedemdziesiatPiecProcent;
                                break;
                            case TypUzytkownikaPojazdu.OsobaWspolpracujaca:
                            case TypUzytkownikaPojazdu.Wspolnik:
                                daneZapisu.Zapis.SposobNaliczaniaKwotyDoOdliczenia = (byte)DoUjeciaWKosztach.DwadziesciaProcent;
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        switch ((TypUzytkownikaPojazdu)pojazd.TypUzytkownika)
                        {
                            case TypUzytkownikaPojazdu.Firma:
                            case TypUzytkownikaPojazdu.OsobaWspolpracujaca:
                            case TypUzytkownikaPojazdu.Wspolnik:
                                daneZapisu.Zapis.SposobNaliczaniaKwotyDoOdliczenia = (byte)DoUjeciaWKosztach.Proporcjonalnie;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }
    }
}
