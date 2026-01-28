using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;

namespace KsefPluginy
{
    public class OpisPozycjiJakoNazwaAsortymentu : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private readonly Guid _id = new Guid("937DFB23-2972-4CC4-8874-49807F5973B9");

        public Guid Identyfikator => _id;

        public string Nazwa => "Ustawianie opisu pozycji jako nazwa asortymentu";

        public string Opis => "Wypełnia pole P_7 lub P_7Z dla dokumentów zaliczkowych opisem pozycji.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            switch (pole)
            {
                case PolaEFaktury.P_7:
                case PolaEFaktury.P_7Z:
                    {
                        if (kontekst.Obiekt is PozycjaDokumentu pozycja)
                        {
                            string opis = pozycja.Opis.Trim(); // żeby usunąć zbędne białe znaki
                            if (!string.IsNullOrEmpty(opis) // jeśli opis jest pusty to z niego nie korzystamy
                                && opis.Length <= 256) // jeśli opis jest dłuższy niż 256 znaków (maksymalna długość pola w schemie) to z niego nie korzystamy
                            {
                                wartosc = opis;
                            }
                        }
                    }
                    break;
            }

            return wartosc;
        }
    }
}
