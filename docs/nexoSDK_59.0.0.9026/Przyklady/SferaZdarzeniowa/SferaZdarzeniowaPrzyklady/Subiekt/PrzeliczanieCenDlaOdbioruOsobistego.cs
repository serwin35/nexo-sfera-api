using InsERT.Moria.CennikiICeny;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq;

namespace SferaZdarzeniowaPrzyklady.Subiekt
{
    /// <summary>
    /// Plugin reagujący na zmianę pola 'SposobDostawy' na dokumencie sprzedaży, który po jego zmianie na 'Odbiór osobisty' przelicza ceny na pozycjach wg cennika o nazwie 'Specjalny', a w przeciwnym wypadku przelicza ceny na pozycjach wg domyślnego cennika.
    /// Rozpoznawanie sposobu dostawy typu 'Odbiór osobisty' zrealizowane jest przez pole 'WidocznyAdresDostawy' w sposobie dostawy.
    /// W przypadku gdy plugin nie odnajdzie cennika głównego o nazwie 'Specjalny' to dodawane jest ostrzeżenie.
    /// Dla innych sposobów dostawy (w szczególności przy braku sposobu dostawy) przywracane są ceny z domyślnego cennika.
    /// </summary>
    public class PrzeliczanieCenDlaOdbioruOsobistego : KlientSferyZdarzeniowej<IDokument>
    {
        private static readonly string NazwaPoziomuCenDlaOdbioruOsobistego = "Specjalny";
        private readonly ICenniki _cenniki;
        private readonly IKonfiguracje _konfiguracje;

        public PrzeliczanieCenDlaOdbioruOsobistego(ICenniki cenniki
            , IKonfiguracje konfiguracje)
        {
            _cenniki = cenniki ?? throw new ArgumentNullException(nameof(cenniki));
            _konfiguracje = konfiguracje ?? throw new ArgumentNullException(nameof(konfiguracje));
        }

        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokument> kontekst)
        {
            if ((kontekst.Dane is DokumentDS || kontekst.Dane is DokumentZK) // dla dokumentów sprzedaży i zamówień od klienta
                && kontekst.ObiektBiznesowy is ICenaIRabat obslugaCen
                && kontekst.NazwaWlasciwosci == nameof(DokumentDS.SposobDostawy)) // po zmianie pola "SposobDostawy"
                PrzeliczCenyPoZmianieSposobuDostawy(kontekst, obslugaCen, kontekst.Dane as Dokument);
        }

        private void PrzeliczCenyPoZmianieSposobuDostawy(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokument> kontekst, ICenaIRabat obslugaCen, Dokument dokument)
        {
            if (CzyOdbiorOsobisty(dokument)) // sprawdzamy czy na dokumencie jest ustawiony odbiór osobisty
            {
                bool ustawionyOdpowiedniPoziomCen = false;
                if (dokument.PoziomCen == null || !dokument.PoziomCen.Nazwa.Equals(NazwaPoziomuCenDlaOdbioruOsobistego))
                {
                    // jeśli na dokumencie NIE jest ustawiony cennik główny (poziom cen) o nazwie specjalny to wyszukujemy go w bazie i go ustawiamy
                    PoziomCen specjalny = _cenniki.Dane.Wszystkie().Where(c => c.Status == (int)StatusCennika.Zatwierdzony && c.Bazowy && c.Tytul.Equals(NazwaPoziomuCenDlaOdbioruOsobistego)).Select(c => c.PoziomCen).FirstOrDefault();
                    if (specjalny != null)
                    {
                        UstawPoziomCenDokumentu(dokument, specjalny);
                        ustawionyOdpowiedniPoziomCen = true;
                    }
                }
                else
                    ustawionyOdpowiedniPoziomCen = true;

                if (ustawionyOdpowiedniPoziomCen)
                    PrzeliczCeny(obslugaCen); // przeliczamy ceny wg cennika głównego o nazwie "Specjalny"
                else
                    kontekst.DodajOstrzezenie("Nie znaleziono cennika głównego o nazwie 'Specjalny' do przeliczenia cen po zmianie sposobu dostawy.", dokument, nameof(DokumentDS.SposobDostawy));
            }
            else
            {
                // jeśli na dokumencie sposób dostawy jest inny niż "Odbiór osobisty" to przeliczamy ceny wg domyślnego sposobu wyliczania cen pobranego z klienta, oddziału (miejsce sprzedaży) bądź konfiguracji
                if (dokument.Konfiguracja == null)
                    return;
                IKonfiguracjaObowiazujaca konfiguracjaDokumentu = _konfiguracje.PobierzParametryKonfiguracji(dokument);
                PoziomCen domyslnyPoziomCen = null;
                Guid? domyslnaFunkcjaWyliczaniaCeny = null;
                if (konfiguracjaDokumentu.PoziomCen == null && konfiguracjaDokumentu.FunkcjaWyliczaniaCenyZeStanami == null)
                {
                    if (dokument.Podmiot != null && (dokument.Podmiot.PoziomCen != null || dokument.Podmiot.FunkcjaWyliczaniaCeny.HasValue))
                    {
                        domyslnyPoziomCen = dokument.Podmiot.PoziomCen;
                        domyslnaFunkcjaWyliczaniaCeny = dokument.Podmiot.FunkcjaWyliczaniaCeny;
                    }
                    else if (dokument.MiejsceSprzedazy != null)
                    {
                        domyslnyPoziomCen = dokument.MiejsceSprzedazy.PoziomCen;
                        domyslnaFunkcjaWyliczaniaCeny = dokument.MiejsceSprzedazy.FunkcjaWyliczaniaCeny;
                    }
                }
                else if (konfiguracjaDokumentu.PoziomCen != null)
                    domyslnyPoziomCen = konfiguracjaDokumentu.PoziomCen;
                else if (konfiguracjaDokumentu.FunkcjaWyliczaniaCenyZeStanami.HasValue)
                    domyslnaFunkcjaWyliczaniaCeny = konfiguracjaDokumentu.FunkcjaWyliczaniaCenyZeStanami;

                if (domyslnaFunkcjaWyliczaniaCeny.HasValue || domyslnyPoziomCen != null)
                {
                    if (domyslnaFunkcjaWyliczaniaCeny.HasValue)
                        UstawFunkcjeWyliczaniaCenyDokumentu(dokument, domyslnaFunkcjaWyliczaniaCeny.Value);
                    else
                        UstawPoziomCenDokumentu(dokument, domyslnyPoziomCen);
                    PrzeliczCeny(obslugaCen);
                }

            }
        }

        private bool CzyOdbiorOsobisty(Dokument dokument) => dokument.SposobDostawy != null
                //&& dokument.SposobDostawy.Nazwa == "Odbiór osobisty" // rozpoznawanie sposobu dostawy wg nazwy jest ryzykowne
                && !dokument.SposobDostawy.WidocznyAdresDostawy;

        private void PrzeliczCeny(ICenaIRabat obslugaCen)
        {
            obslugaCen.UstawCeny();
            obslugaCen.UstawRabaty();
            obslugaCen.Przelicz();
        }

        private void UstawPoziomCenDokumentu(Dokument dokument, PoziomCen poziomCen)
        {
            if (dokument.FunkcjaWyliczaniaCenyZeStanami.HasValue)
                dokument.FunkcjaWyliczaniaCenyZeStanami = null;
            if (dokument.FunkcjaWyliczaniaCenyBezStanow.HasValue)
                dokument.FunkcjaWyliczaniaCenyBezStanow = null;
            dokument.PoziomCen = poziomCen;
        }

        private void UstawFunkcjeWyliczaniaCenyDokumentu(Dokument dokument, Guid funkcja)
        {
            if (dokument.PoziomCen != null)
                dokument.PoziomCen = null;
            dokument.FunkcjaWyliczaniaCenyZeStanami =
                dokument.FunkcjaWyliczaniaCenyBezStanow = funkcja;
        }
    }
}
