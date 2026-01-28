using InsERT.Moria.Klienci;
using InsERT.Moria.Komunikacja;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using System;
using System.Linq;

namespace SferaZdarzeniowaPrzyklady.Gestor
{
    public class WyslaniePowitalnegoSMSDoNowegoKlienta : KlientSferyZdarzeniowej<IPodmiot>
    {
        public override void PrzedZapisemObiektu(IKontekstZdarzeniaPrzedZapisemObiektu<IPodmiot> kontekst)
        {
            IRodzajeKontaktu rodzajeKontaktu = kontekst.Uchwyt.PodajObiektTypu<IRodzajeKontaktu>();

            kontekst.UsunBlad(kontekst.ObiektBiznesowy.Dane, nameof(Podmiot.Kontakty));

            if (kontekst.StanObiektu == StanObiektu.Dodawany
                && !kontekst.ObiektBiznesowy.Dane.JestPracownikiem()
                && !kontekst.ObiektBiznesowy.Dane.JestWspolnikiem()
                && !kontekst.ObiektBiznesowy.Dane.JestInstytucja()
                && !kontekst.ObiektBiznesowy.Dane.Kontakty.Any(k => k.Rodzaj.Id == rodzajeKontaktu.DaneDomyslne.Telefon.Id))
            {
                kontekst.DodajBlad("Nie można zapisać klienta bez numeru telefonu.", kontekst.ObiektBiznesowy.Dane, nameof(Podmiot.Kontakty));
            }
        }

        public override void PoZapisieObiektu(IKontekstZdarzeniaPoZapisieObiektu<IPodmiot> kontekst)
        {
            if (kontekst.StanZapisanegoObiektu != StanZapisanegoObiektu.Dodany)
                return;

            IRodzajeKontaktu rodzajeKontaktu = kontekst.Uchwyt.PodajObiektTypu<IRodzajeKontaktu>();
            IPodmioty podmioty = kontekst.Uchwyt.PodajObiektTypu<IPodmioty>();
            IWiadomosciSMS wiadomosciSMS = kontekst.Uchwyt.PodajObiektTypu<IWiadomosciSMS>();

            int identyfikatorZapisanegoPodmiotu = (int)kontekst.IdDanych;

            IPodmiot podmiot = podmioty.Znajdz(p => p.Id == identyfikatorZapisanegoPodmiotu);

            if (podmiot.Dane.JestPracownikiem()
                || podmiot.Dane.JestWspolnikiem()
                || podmiot.Dane.JestInstytucja())
            {
                return;
            }

            Kontakt podstawowyNumerTelefonu = podmiot.Dane.Kontakty.FirstOrDefault(k => k.Podstawowy && k.Rodzaj.Id == rodzajeKontaktu.DaneDomyslne.Telefon.Id);

            var wiadomoscSMSBO = wiadomosciSMS.Utworz();
            wiadomoscSMSBO.Dane.Podmiot = podmiot.Dane;
            wiadomoscSMSBO.Dane.Numer = podstawowyNumerTelefonu.Wartosc;
            wiadomoscSMSBO.Dane.Tresc = $"Witaj {podmiot.Dane.NazwaSkrocona}. Dziękujemy za zainteresowanie naszą ofertą.";
            wiadomoscSMSBO.Dane.Status = (byte)StatusWiadomosciSMS.Robocza;

            if (!wiadomoscSMSBO.Zapisz())
            {
                throw new InvalidOperationException(string.Join(" ", wiadomoscSMSBO.PobierzKomunikatyBledow().Select(k => k.Tresc)));
            }
        }
    }
}