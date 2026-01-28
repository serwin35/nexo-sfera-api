using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;

namespace KsefPluginy
{
    /// <summary>
    /// Funkcja wypełnia pola dodatkowe info (w wierszach faktury oraz wierszach zamówienia) opisem pozycji z encji pozycji dokumentu.
    /// Wykorzystany jest fakt, że w kontekście pola jako Obiekt jest podawana encja pozycji.
    /// </summary>
    public class OpisPozycjiJakoIndeks : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private Guid _id = new Guid("FE1B633E-2732-49C9-A26D-EF4B1A7C18F8");

        public Guid Identyfikator => _id;

        public string Nazwa => "Ustawianie opisu pozycji jako Indeks";

        public string Opis => "Funkcja wypełnia pole Indeks opisem pozycji.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            switch (pole)
            {
                // dla zachowania kompatybilności ze schemą w wersji 1:
                case PolaEFaktury.DodatkoweInfo:
                case PolaEFaktury.DodatkoweInfoZ:
                // schema w wersji 2:
                case PolaEFaktury.Indeks:
                case PolaEFaktury.IndeksZ:
                    // minimalna długość pola DodatkoweInfo/Indeks w schemacie wynosi 1 więc dla pustego opisu zwracamy null żeby pola nie wypełniać w ogóle
                    if (kontekst.Obiekt is PozycjaDokumentu pozycja)
                        wartosc = string.IsNullOrEmpty(pozycja.Opis) ? null : pozycja.Opis;
                    break;
            }

            return wartosc;
        }
    }
}
