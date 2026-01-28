using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Dokumenty.Logistyka.Zdarzenia;
using InsERT.Moria.Rozszerzanie;

namespace OpakowaniaPrezentowe
{
    /// <summary>
    /// Przepisuje uwagi kupującego z zamówienia wysyłkowego do pola własnego "Wiadomość od sprzedającego" przy wystawianiu dokumentu wstępnego ZK.
    /// </summary>
    public class PrzepisywanieUwagKupujacegoPlugin : KlientSferyZdarzeniowejDokumentu<IZamowienieOdKlienta>
    {
        public override void PoWypelnieniuNaPodstawieZamowieniaWysylkowego(IKontekstZdarzeniaPoWypelnieniuNaPodstawieZamowieniaWysylkowego<IZamowienieOdKlienta> kontekst)
        {
            if (string.IsNullOrWhiteSpace(kontekst.ZamowienieZrodlowe.UwagiKupujacego))
            {
                return;
            }

            kontekst.ObiektBiznesowy.Dane.PolaWlasneAdv2.Set(
                KonfiguracjaPlugina.Instancja.PoleWlasne_DokumentZK_WiadomoscDoSprzedajacego,
                kontekst.ZamowienieZrodlowe.UwagiKupujacego);
        }
    }
}