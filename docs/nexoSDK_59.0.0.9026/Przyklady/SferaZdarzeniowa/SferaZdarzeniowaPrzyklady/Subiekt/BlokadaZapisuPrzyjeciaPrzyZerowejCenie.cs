using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;

namespace SferaZdarzeniowaPrzyklady.Subiekt
{
    /// <summary>
    /// Plugin ustawiający blokadę zapisu dokumentu przyjęcia lub zakupu w przypadku próby dodania dokumentu z zerowymi cenami na pozycjach.
    /// </summary>
    public class BlokadaZapisuPrzyjeciaPrzyZerowejCenie : KlientSferyZdarzeniowej<IDokument>
    {
        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokument> kontekst)
        {
            if (kontekst.Dane is PozycjaDokumentu pozycja
                && (pozycja.Dokument is DokumentPW || pozycja.Dokument is DokumentPZ || pozycja.Dokument is DokumentDZ) // kontrolujemy cenę na dokumentach przyjęć i zakupu
                && pozycja.AsortymentAktualny != null // nie jest to usługa jednorazowa
                && pozycja.AsortymentAktualny.Rodzaj.StanyMagazynowe // asortyment ma stany magazynowe (nie jest usługą ani opłatą dodatkową)
                && pozycja.Cena.NettoPrzedRabatem == 0m)
            {
                kontekst.DodajBlad("Nie można przyjmować towaru na magazyn z zerową ceną.", pozycja, $"{nameof(PozycjaDokumentu.Cena)}.{nameof(Cena.NettoPrzedRabatem)}");
            }
        }
    }
}
