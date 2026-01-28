using System;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System.Linq;

namespace KsefPluginy
{
    /// <summary>
    /// Funkcja zmienia sposób generowania numerów rachunków bankowych. Domyślnie rachunku w formacie NRB są wykazywane w polu NrRBPL (czyli bez przedrostka).
    /// Chcemy każdy rachunek bankowy przedstawiać w formacie IBAN (z przedrostkiem) więc funkcja wypełnia pola NrRBZagr oraz SWIFT, a oprócz tego musi
    /// zignorować pole NrRBPL ponieważ schemat XSD faktury ustrukturyzowanej nie pozwala wpisać rachunku bankowego jednocześnie w dwóch formatach.
    /// Wykorzystuje ona fakt, że dla pól związanych z rachunkiem bankowym w kontekście pola jako Obiekt jest podawana encja rachunku bankowego.
    /// </summary>
    public class AlternatywneGenerowanieRachunkuBankowego : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private Guid _id = new Guid("1485743C-341B-45FB-8906-23E120E35ECE");

        public Guid Identyfikator => _id;

        public string Nazwa => "Ustawianie rachunku bankowego jako zagraniczny";

        public string Opis => "Funkcja rachunek Polski ustawia jako zagraniczny z przedrostkiem PL.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            switch (pole)
            {
                case PolaEFaktury.NrRBPL:
                    ignorujPole = true; // dla pola NrRBPL ustawiamy flagę "ignorujPole" co jest sygnałem dla nexo żeby tego pola nie wypełniać w pliku XML
                    break;
                case PolaEFaktury.NrRBZagr:
                    {
                        if (kontekst.Obiekt is RachunekBankowy rachunek)
                            wartosc = $"PL{rachunek.NumerBezSeparatorow}"; // doklejamy przedrostek PL
                    }
                    break;
                case PolaEFaktury.SWIFT:
                    {
                        if (kontekst.Obiekt is RachunekBankowy rachunek && rachunek.KodSWIFT != null)
                            wartosc = rachunek.KodSWIFT.Kod; // pobieramy kod SWIFT z rachunku bankowego
                    }
                    break;
                case PolaEFaktury.NrRB:
                    {
                        if (kontekst.Obiekt is RachunekBankowy rachunek && rachunek.NumerBezSeparatorow.All(c => char.IsDigit(c)))
                            wartosc = $"PL{rachunek.NumerBezSeparatorow}"; // doklejamy przedrostek PL

                    }
                    break;
            }

            return wartosc;
        }
    }
}
