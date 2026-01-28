using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Waluty;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KsefPluginy
{
    /// <summary>
    /// Funkcja dla dokumentów w walucie obcej z płatnością odroczoną dopisuje dodatkowy rachunek bankowy do wpłaty VAT w PLN.
    /// </summary>
    public class DodatkowyRachunekBankowyDlaVat : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private Guid _id = new Guid("1F81BD08-44C5-4328-80B4-8E9D8B801660");

        private readonly IUchwyt _uchwyt;
        private readonly Lazy<Waluta> _walutaSystemowa;

        public DodatkowyRachunekBankowyDlaVat(IUchwyt uchwyt)
        {
            _uchwyt = uchwyt ?? throw new ArgumentNullException(nameof(uchwyt));
            _walutaSystemowa = new Lazy<Waluta>(PobierzWaluteSystemowa());
        }

        public Guid Identyfikator => _id;

        public string Nazwa => "Dodatkowy rachunek bankowy dla VAT";

        public string Opis => "Funkcja dla dokumentów w walucie obcej dopisuje dodatkowy rachunek bankowy w PLN dla VAT.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            if (kontekst.SciezkaPola.StartsWith("Fa\\Platnosc\\RachunekBankowy") // generujemy dane rachunku bankowego...
                && !kontekst.SciezkaPola.Contains("RachunekBankowyFaktora") // ... ale nie rachunku bankowego faktora (to jest osobna sekcja w schemie)
                && kontekst.Dokument.Waluta.Id != _walutaSystemowa.Value.Id // gdy dokument jest w obcej walucie 
                && kontekst.Dokument.PlatnosciDokumentow.Any(p => p.RodzajPlatnosci == (byte)RodzajPlatnosci.Odroczona)) // i posiada płatność odroczoną
            {
                // W przypadku gdy wypełniamy dane rachunku bankowego powiązanego z płatnością odroczoną na dokumencie
                // w kontekst.Obiekt przekazywana jest encja tego rachunku:
                if (kontekst.Obiekt is RachunekBankowy rachunek)
                {
                    switch (pole)
                    {
                        case PolaEFaktury.OpisRachunku: // podmieniamy domyśny opis rachunku
                            wartosc = $"Rachunek w {rachunek.Waluta.Symbol} do wpłaty wartości netto {kontekst.Dokument.Wartosc.NettoPoRabacie:0.00} {rachunek.Waluta.Symbol}";
                            break;
                    }
                }
                // Jeśli nie ma tu encji rachunku to wypełniamy dodatkowy rachunek bankowy:
                else if (SprobujPobracRachunekDlaVat(kontekst.Dokument, out RachunekBankowy rachunekPln))
                {
                    switch (pole)
                    {
                        case PolaEFaktury.NrRB:
                            wartosc = rachunekPln.NumerBezSeparatorow;
                            break;
                        case PolaEFaktury.SWIFT:
                            wartosc = rachunekPln.KodSWIFT?.Kod;
                            break;
                        case PolaEFaktury.NazwaBanku:
                            wartosc = rachunekPln?.PodmiotBankowy?.NazwaSkrocona;
                            break;
                        case PolaEFaktury.OpisRachunku:
                            wartosc = $"Rachunek w PLN do wpłaty VAT {kontekst.Dokument.TabeleVat.Select(t => t.WartoscVatSystemowa).DefaultIfEmpty(0m).Sum():0.00} PLN";
                            break;
                    }
                }
            }

            return wartosc;
        }

        private Waluta PobierzWaluteSystemowa() => _uchwyt.PodajObiektTypu<IParametryWalut>().WalutaSystemowa;

        /// <summary>
        /// Wyszukuje w danych mojej firmy rachunek bankowy w walucie PLN.
        /// </summary>
        /// <param name="dokument">Dokument, dla którego wyszukujemy rachunek bankowy.</param>
        /// <param name="rachunek">Znaleziony rachunek.</param>
        /// <returns><c>True</c> jeśli uda się odnaleźć rachunek bankowy.</returns>
        private bool SprobujPobracRachunekDlaVat(Dokument dokument, out RachunekBankowy rachunek)
        {
            IEnumerable<RachunekBankowy> rachunkiPln = dokument.MojaFirma.Rachunki.Where(r => r.Waluta.Id == _walutaSystemowa.Value.Id).ToArray();
            rachunek = rachunkiPln.FirstOrDefault(r => r.PodstawowyDlaWaluty)
                ?? rachunkiPln.FirstOrDefault(r => r.WlascicielPodstawowego != null)
                ?? rachunkiPln.FirstOrDefault();
            return rachunek != null;
        }
    }
}
