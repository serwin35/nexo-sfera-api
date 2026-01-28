using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Listy;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpakowaniaPrezentowe
{
    /// <summary>
    /// Dodaje na liście zleceń w menu Operacje nową operację "Utwórz plik maszyny grawerującej", która pozwala na utworzenie pliku z informacjami o grawerunkach.
    /// </summary>
    public class Operacja_Lista_UtworzPlikDlaMaszynyGrawerujacej : OperacjaNaLiscieDanych<DokumentZPM, int>
    {
        public override string Identyfikator => "Lista_Utworz_Plik";

        public override string Nazwa => OperacjeHelper.PodajNazwe(KonfiguracjaPlugina.Instancja.Operacja_Lista_UtworzPlikMaszynyGrawerujacej);

        protected override string[] SciezkaWMenu => OperacjeHelper.PodajSciezkeWMenu(KonfiguracjaPlugina.Instancja.Operacja_Lista_UtworzPlikMaszynyGrawerujacej);

        protected override void Wykonaj(
            IReadOnlyCollection<int> identyfikatoryWybranychElementow,
            IKontekstListyDanych kontekstListyDanych,
            IKontekstOperacji kontekstOperacji)
        {
            List<DokumentZPM> zlecenia = PobierzZlecenia(kontekstOperacji.Uchwyt, identyfikatoryWybranychElementow);
            if (zlecenia.Count == 0)
            {
                PokazOknoZBledemONiewybraniuZlecen(kontekstOperacji.Uchwyt);

                return;
            }

            WykonajOperacje(zlecenia, kontekstOperacji);
        }

        private static void WykonajOperacje(
            List<DokumentZPM> zlecenia,
            IKontekstOperacji kontekst)
        {
            IOknoOperacjiZbiorczej oknoOperacji = UtworzOknoOperacjiPobieraniaDedykacji(kontekst.Uchwyt);

            WynikOperacjiZbiorczej<DokumentZPM> wynikOperacji
                = oknoOperacji.Pokaz(
                    zlecenia,
                    dok =>
                    {
                        List<string> dedyjkacje = dok
                            .PozycjaKomplet
                            .SpecyfikacjePozycji
                            .Select(p => p.PolaWlasneAdv2?.Get<string>(KonfiguracjaPlugina.Instancja.PoleWlasne_Partia_Dedykacja))
                            .Where(d => !string.IsNullOrWhiteSpace(d))
                            .ToList();

                        if (dedyjkacje.Count == 0)
                        {
                            return WynikOperacji.ZakonczonaNiepowodzeniem("Zlecenie nie zawiera żadnych dedykacji.");
                        }

                        for (int i = 0; i < dedyjkacje.Count; i++)
                        {
                            string nazwaPliku = OperacjeHelper.PodajNazwePlikuSVGDlaDedykacji(dok, i + 1);
                            string dane = new SerializatorGrawerunkow().Serializuj(dedyjkacje[i]);

                            OperacjeHelper.ZapewnijFolder(nazwaPliku);

                            File.WriteAllText(nazwaPliku, dane);
                        }

                        return WynikOperacji.ZakonczonaPowodzeniem();
                    });

            if (wynikOperacji.WynikiDlaObiektow.Any(o => o.wynikOperacji.Status == StatusOperacji.ZakonczonaPowodzeniem))
            {
                PokazOknoZInformacjaOSukcecie(kontekst.Uchwyt);
            }
        }

        private static IOknoOperacjiZbiorczej UtworzOknoOperacjiPobieraniaDedykacji(
            IUchwyt uchwyt)
        {
            IOknoOperacjiZbiorczej okno = uchwyt.PodajObiektTypu<IOknoOperacjiZbiorczej>();

            okno.Tytul = "Tworzenie plików dla maszyny grawerującej";

            return okno;
        }

        private static List<DokumentZPM> PobierzZlecenia(
            IUchwyt uchwyt,
            IEnumerable<int> identyfikatoryZlecen)
        {
            return uchwyt
                .PodajObiektTypu<IZleceniaProdukcyjneMontowania>()
                .Dane
                .Wszystkie()
                .Where(z => identyfikatoryZlecen.Contains(z.Id))
                .ToList();
        }

        private static void PokazOknoZBledemONiewybraniuZlecen(
            IUchwyt uchwyt)
        {
            Okna.PokazOknoZBledem(
                uchwyt,
                "Nie wybrano żadnego zlecenia montowania.");
        }

        private static void PokazOknoZInformacjaOSukcecie(
            IUchwyt uchwyt)
        {
            Okna.PokazOknoZInformacja(
                uchwyt,
                $"Pliki dla maszyny grawerującej zostały zapisane w:\r\n{KonfiguracjaPlugina.Instancja.SciezkaZapisuSVG}");
        }
    }
}