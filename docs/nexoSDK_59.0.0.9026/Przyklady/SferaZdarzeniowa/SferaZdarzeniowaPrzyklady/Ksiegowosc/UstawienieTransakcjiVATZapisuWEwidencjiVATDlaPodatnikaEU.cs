using InsERT.Moria.EwidencjaVAT;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;

namespace SferaZdarzeniowaPrzyklady.Ksiegowosc
{
    /// <summary>
    /// Plugin ustawiający transakcję VAT zapisu w ewidencji VAT zakupu dla kontrahenta unijnego
    /// </summary>
    public class UstawienieTransakcjiVATZapisuWEwidencjiVATDlaPodatnikaEU : KlientSferyZdarzeniowej<IZapisWEwidencjiVAT>
    {
        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IZapisWEwidencjiVAT> kontekst)
        {
            if (kontekst.Dane is BazowyZapisWEwidencjiVAT zapisVAT
                && kontekst.NazwaWlasciwosci == nameof(BazowyZapisWEwidencjiVAT.Podmiot)
                && zapisVAT.Podmiot != null
                && zapisVAT.Podmiot.PodmiotDlaKtoregoHistoria.PodatnikUE)
            {
                var transakcje = kontekst.Uchwyt.PodajObiektTypu<ITransakcjeVAT>();
                var transakcjaWDT = transakcje.DaneDomyslne.WewnatrzWspolnotowaDostawaTowarow;
                var transakcjaWNT = transakcje.DaneDomyslne.WewnatrzwspolnotoweNabycieTowarow;

                zapisVAT.DaneDodatkowe.Transakcja = zapisVAT.TypEwidencji == (byte)TypEwidencjiVAT.Zakup ? transakcjaWNT : transakcjaWDT;
            }
        }
    }
}