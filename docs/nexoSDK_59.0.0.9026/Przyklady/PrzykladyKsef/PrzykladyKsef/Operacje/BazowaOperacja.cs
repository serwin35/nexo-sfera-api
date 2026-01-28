using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyKsef.Operacje
{
    public abstract class BazowaOperacja
    {
        protected readonly Uchwyt _sfera;
        protected BazowaOperacja(Uchwyt sfera)
        {
            _sfera = sfera ?? throw new ArgumentNullException(nameof(sfera));
        }

        public abstract void Wykonaj();

        /// <summary>
        /// Wyszukiwanie e-Faktury do importu.
        /// </summary>
        /// <returns>Importowana e-Faktura.</returns>
        protected DokumentElektroniczny ZnajdzEFaktureDoPrzetworzenia()
        {
            IDokumentyElektroniczne dokumentyElektroniczne = _sfera.DokumentyElektroniczne();
            return dokumentyElektroniczne.Dane
                .Wszystkie(fe => fe.Rodzaj == (byte)RodzajDokumentuElektronicznego.Importowany // tylko e-Faktury importowane
                                && fe.StatusPrzetworzenia == (byte)StatusPrzetworzeniaEFaktury.DoPrzetworzeniaWSubiekcie) // o statusie "Do przetworzenia w Subiekcie"
                .OrderByDescending(fe => fe.Id) // sortujemy malejąco po identyfikatorze
                .FirstOrDefault();
        }

        protected List<Dokument> DodajDokumentySprzedazy(string[] symboleTowarow, decimal[] ilosci, string[] nipyKlientow)
        {
            IDokumenty menedzerDokumentow = _sfera.Dokumenty();
            List<int> identyfikatoryDokumentow = new List<int>();
            for (int i=0; i<symboleTowarow.Length; i++)
            {
                int? id = DodajDokumentSprzedazy(symboleTowarow[i], ilosci[i], nipyKlientow[i]);
                if (id.HasValue)
                    identyfikatoryDokumentow.Add(id.Value);
            }
            return menedzerDokumentow.Dane.Wszystkie().Where(d => identyfikatoryDokumentow.Contains(d.Id)).ToList();
        }

        protected int? DodajDokumentSprzedazy(string symbolTowaru, decimal ilosc, string nipKlienta)
        {
            IDokumentySprzedazy dokumentySprzedazy = _sfera.DokumentySprzedazy();
            using (IDokumentSprzedazy faktura = dokumentySprzedazy.UtworzFaktureSprzedazy())
            {
                faktura.Dane.Uwagi = "Dokument wygenerowany i wysłany do KSeF przez Sferę nexo.";
                faktura.PodmiotyDokumentu.UstawNabywceWedlugNIP(nipKlienta);
                faktura.Pozycje.Dodaj(symbolTowaru, ilosc);
                faktura.Przelicz();
                faktura.Platnosci.DodajDomyslnaPlatnoscNatychmiastowaNaKwoteDokumentu();
                if (faktura.Zapisz())
                    return faktura.Dane.Id;
                else
                {
                    faktura.WypiszBledy();
                    return null;
                }
            }
        }

        protected void ObsluzListeStatusowWysylki(List<IWynikSprawdzaniaStatusuWysylki> statusyWysylki)
        {
            foreach (IWynikSprawdzaniaStatusuWysylki status in statusyWysylki)
            {
                if (!status.PrzetwarzanieZakonczone)
                    Console.WriteLine($"e-Faktura {status.NumerDokumentu} nie została jeszcze przetworzona.");
                else if (status.Sukces)
                    Console.WriteLine($"e-Faktura {status.NumerDokumentu} została przetworzona poprawnie i został jej nadany numer KSeF: {status.NumerKsef}.");
                else
                    Console.WriteLine($"e-Faktura została przetworzona niepoprawnie: {string.Join(Environment.NewLine, status.Bledy)}");
            }
        }
    }
}
