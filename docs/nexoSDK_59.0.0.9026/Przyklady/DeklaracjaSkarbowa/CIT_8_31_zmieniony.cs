using InsERT.Moria.Deklaracje;
using InsERT.Moria.Deklaracje.Atrybuty;
using InsERT.Moria.Ksiegowosc;

namespace DeklaracjaSkarbowa
{
    [Wersja(
        identyfikator: "F85680E4-FA01-43A7-AF23-5442E96B67F4",
        typ: "CIT-8",
        podtyp: PodtypDeklaracji.Deklaracja,
        nazwa: "CIT-8 (31) zmieniona",
        numer: 31,
        typOkresu: TypOkresu.OkresObrachunkowy,
        typPodatnika: TypPodatnika.MojaFirma,
        dataObowiazywaniaOd: "2021-01-01",
        dataObowiazywaniaDo: "2022-12-31",
        NumerWyswietlany = "31 zmieniona",
        Opis = "Zeznanie o wysokości osiągniętego dochodu (poniesionej straty) przez podatnika podatku dochodowego od osób prawnych",
        ObslugaEDeklaracji = TypObslugiEDeklaracji.WysylkaEDeklaracji | TypObslugiEDeklaracji.PodpisywanieEDeklaracji | TypObslugiEDeklaracji.GenerowaniePlikuXML,
        OczekiwanieNaWysylkeElektroniczna = true,
        Grupa = "CIT",
        Waluta = "PLN",
        Produkt = TypProduktu.Rewizor,
        FormyKsiegowosci = DozwoloneFormyKsiegowosci.KR_ZPiK
    )]
    public class CIT_8_31_zmieniony : InsERT.Moria.Deklaracje.Definicje.CIT_8_31
    {
        // [Pole("321", "Inne informacje", Sekcja = "L.2")]
        public override string Pole_321(IKontekstWyliczaniaDeklaracji kontekst)
        {
            return "To jest test wstawienie własnej wartości do pola 321. 'Inne informacje' w sekcji 'L.2' deklaracji CIT-8 (31).";
        }
    }
}
