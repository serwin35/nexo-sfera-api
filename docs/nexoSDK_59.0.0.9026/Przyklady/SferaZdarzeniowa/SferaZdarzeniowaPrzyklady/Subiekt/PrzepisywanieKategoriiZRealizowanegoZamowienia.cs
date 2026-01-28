using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SferaZdarzeniowaPrzyklady.Subiekt
{
    /// <summary>
    /// Plugin przepisuje kategorię z realizowanych zamówień podczas realizacji dokumentem sprzedaży.
    /// W przypadku gdy kategoria z przepisywanego zamówienia nie jest dostępna dla konfiguracji realizującego dokumentu sprzedaży to pokazywane jest ostrzeżenie.
    /// W przypadku gdy na realizowanych zamówieniach są różne kategorie również dodawane jest ostrzeżenie.
    /// </summary>
    public class PrzepisywanieKategoriiZRealizowanegoZamowienia : KlientSferyZdarzeniowej<IDokumentSprzedazy>
    {
        private readonly IKonfiguracje _konfiguracje;

        public PrzepisywanieKategoriiZRealizowanegoZamowienia(IKonfiguracje konfiguracje)
        {
            _konfiguracje = konfiguracje ?? throw new ArgumentNullException(nameof(konfiguracje));
        }

        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokumentSprzedazy> kontekst)
        {
            if (kontekst.StanObiektu == StanObiektu.Dodawany
                && kontekst.Dane is DokumentDS dokument
                && kontekst.NazwaWlasciwosci == nameof(Dokument.DokumentyRealizowane)) // reagujemy na zmianę pola (kolekcji) dokumentów realizowanych
                PrzepiszKategorieNaRealizujacy(kontekst, dokument);
        }

        private void PrzepiszKategorieNaRealizujacy(IKontekstZdarzeniaZWalidacja kontekst, DokumentDS dokument)
        {
            IEnumerable<DokumentZK> realizowaneZamowienia = dokument.DokumentyRealizowane.OfType<DokumentZK>().ToArray();
            if (realizowaneZamowienia.Any())
            {
                // pobieramy kategorię z realizowanych zamówień:
                IEnumerable<KategoriaDokumentu> kategorieRealizowanych = realizowaneZamowienia.Where(zk => zk.KategoriaDokumentu != null).Select(zk => zk.KategoriaDokumentu).Distinct();
                if (kategorieRealizowanych.Any())
                {
                    if (kategorieRealizowanych.Count() == 1)
                    {
                        KategoriaDokumentu kategoriaDoPrzepisania = kategorieRealizowanych.Single();
                        // jeśli kategoria jest dostępna dla bieżącej konfiguracji dokumentu realizującego to ją ustawiamy:
                        if (kategoriaDoPrzepisania.Konfiguracje.Any(k => k.Id == dokument.KonfiguracjaId))
                            dokument.KategoriaDokumentu = kategoriaDoPrzepisania;
                        else
                            BrakMozliwosciPrzepisaniaKategorii(kontekst, dokument, kategoriaDoPrzepisania, realizowaneZamowienia.Count());
                    }
                    else
                        BrakMozliwosciPrzepisaniaKategorii(kontekst, dokument, null, realizowaneZamowienia.Count());
                }
            }
        }

        private void BrakMozliwosciPrzepisaniaKategorii(IKontekstZdarzeniaZWalidacja kontekst, DokumentDS dokument, KategoriaDokumentu kategoriaDoPrzepisania, int liczbaRealizowanych)
        {
            // jeśli nie możemy przepisać kategorii z realizowanych dokumentów to przywracamy kategorię domyślną:
            KategoriaDokumentu domyslna = _konfiguracje.PobierzParametryKonfiguracji(dokument).DomyslnaKategoriaDokumentu;
            if (dokument.KategoriaDokumentu?.Id != domyslna?.Id)
                dokument.KategoriaDokumentu = domyslna;
            if (kategoriaDoPrzepisania == null)
                kontekst.DodajOstrzezenie($"Kategoria nie została przepisana z dokumentów realizowanych ponieważ są one opisane różnymi kategoriami.", dokument, nameof(Dokument.KategoriaDokumentu));
            else
                kontekst.DodajOstrzezenie($"Nie można przepisać kategorii '{kategoriaDoPrzepisania.Nazwa}' z {(liczbaRealizowanych == 1 ? "realizowanego zamówienia" : "realizowanych zamówień")} ponieważ nie jest ona dozwolona dla tego typu dokumentu.", dokument, nameof(Dokument.KategoriaDokumentu));

        }
    }
}
