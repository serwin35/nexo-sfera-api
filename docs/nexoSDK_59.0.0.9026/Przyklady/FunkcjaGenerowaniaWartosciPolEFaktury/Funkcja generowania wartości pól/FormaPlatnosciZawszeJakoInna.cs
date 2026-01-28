using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;

namespace KsefPluginy
{
    /// <summary>
    /// Funkcja zmienia sposób wypełniania pól dotyczących formy płatności. Domyślnie wypełniane są one wartością słownikową określoną w schemacie XSD.
    /// Chcemy określać formę płatności zawsze poprzez jej nazwę określoną w słowniku form płatności w nexo. Funkcja wypełnia więc pola PlatnoscInna (stała wartość 1) oraz
    /// OpisPlatnosci pobierając nazwę formy z encji płatność dokumentu przekazanej do kontekstu w polu Obiekt. Dodatkowo należy zignorować wartość w polu FormaPlatnosci ponieważ
    /// schemat XSD nie zezwala na wypełnienie obu tych części.
    /// </summary>
    public class FormaPlatnosciZawszeJakoInna : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private Guid _id = new Guid("54C97509-F434-4794-93F0-3EFE1C8D5B9D");

        public Guid Identyfikator => _id;

        public string Nazwa => "Wypełnianie formy płatności jako Inna";

        public string Opis => "Funkcja wypełnia pola PlatnoscInna oraz OpisPlatnosci zamiast pola FormaPlatnosci.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            switch (pole)
            {
                case PolaEFaktury.FormaPlatnosci:
                    ignorujPole = true; // ustawienie flagi "ignorujPole" powoduje przekazanie do nexo informacji aby tego pola nie wypełniać
                    break;
                case PolaEFaktury.PlatnoscInna:
                    wartosc = "1"; // stała wartość
                    break;
                case PolaEFaktury.OpisPlatnosci:
                    if (kontekst.Obiekt is PlatnoscDokumentu platnosc && platnosc.FormaPlatnosci != null)
                        return platnosc.FormaPlatnosci.Nazwa; // pobranie nazwy formy płatności
                    break;
            }

            return wartosc;
        }
    }
}
