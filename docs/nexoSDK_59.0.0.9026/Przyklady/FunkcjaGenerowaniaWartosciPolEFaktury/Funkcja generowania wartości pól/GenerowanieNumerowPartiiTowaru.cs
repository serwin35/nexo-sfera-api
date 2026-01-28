using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KsefPluginy
{
    public class GenerowanieNumerowPartiiTowaru : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private readonly Guid _id = new Guid("ECEF45EB-741D-4431-AB48-73E42C87F51B");

        public Guid Identyfikator => _id;

        public string Nazwa => "Uzupełnianie numerow partii towaru";

        public string Opis => "Wypełnia sekcję NrPartiiTowaru w węźle WarunkiTransakcji danymi z rozchodowanych partii.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            if (pole == PolaEFaktury.NrPartiiTowaru
                && kontekst.SciezkaPola.StartsWith("Fa\\WarunkiTransakcji\\NrPartiiTowaru")
                // schema przewiduje maksymalnie 1000 wpisów w sekcji numerów partii towaru więc nexo przekazuje do plugina
                // numer elementu (liczba od 0 do 999) w polu IKontekstPolaEFaktury.IndeksElementu
                && kontekst.IndeksElementu.HasValue)
            {
                List<string> numeryPartii = PobierzNumeryPartii(kontekst.Dokument);
                if (kontekst.IndeksElementu.Value < numeryPartii.Count)
                    wartosc = numeryPartii[kontekst.IndeksElementu.Value];
            }
            else if (pole == PolaEFaktury.WarunkiDostawy)
            {
                // pole WarunkiDostawy występuje po numerach partii towaru więc możemy tutaj wyczyścić cache:
                _identyfikatorDokumentu = null;
                _numeryPartii = null;
            }
            return wartosc;
        }

        private int? _identyfikatorDokumentu;
        private List<string> _numeryPartii;
        private List<string> PobierzNumeryPartii(Dokument dokument)
        {
            // jeśli generujemy dla innego dokumentu niż poprzednio zapamiętany
            if (!_identyfikatorDokumentu.HasValue || _identyfikatorDokumentu.Value != dokument.Id)
            {
                _numeryPartii = null;
                if (dokument is DokumentDS dokumentSprzedazy)
                {
                    // zapamiętujemy wewnątrz plugina numery partii towaru dla podanego dokumentu
                    // żeby nie było konieczności pobierania ich po raz kolejny
                    _numeryPartii = dokumentSprzedazy
                        .Pozycje
                        .SelectMany(p => p.ZnajdzPozycjeRealizowane(TypDokumentu.WydanieZewnetrzne | TypDokumentu.KorektaWydaniaZewnetrznego))
                        .Where(p => p.Wydanie != null)
                        .SelectMany(p => p.Wydanie.Rozchody.Select(r => r.Partia.Numer))
                        .Where(nr => !string.IsNullOrEmpty(nr))
                        .Distinct()
                        .ToList();
                }
                else
                    _numeryPartii = new List<string>();
                _identyfikatorDokumentu = dokument.Id;
            }
            return _numeryPartii;
        }
    }
}
