using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Globalization;
using System.Linq;

namespace KsefPluginy
{
    public class UzupelnianieUmowNaPodstawieUwag : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private readonly Guid _id = new Guid("96B627D9-C919-4498-BE3F-CD443D9BFEBE");

        public Guid Identyfikator => _id;

        public string Nazwa => "Uzupełnianie umów na podstawie uwag";

        public string Opis => "Wypełnia sekcję Umowy w węźle WarunkiTransakcji danymi z uwag na dokumencie.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            string uwagi = kontekst.Dokument.Uwagi;
            // puste uwagi ignorujemy:
            if (!string.IsNullOrWhiteSpace(uwagi)
                // sprawdzamy czy znajdujemy się w węźle dotyczącym umów
                && kontekst.SciezkaPola.StartsWith("Fa\\WarunkiTransakcji\\Umowy")
                // schema przewiduje maksymalnie 100 wpisów w sekcji umów więc nexo przekazuje do plugina
                // numer elementu (liczba całkowita od 0 do 99) w polu IKontekstPolaEFaktury.IndeksElementu
                && kontekst.IndeksElementu.HasValue)
            {
                wartosc = PobierzWartoscZUwag(uwagi, kontekst.IndeksElementu.Value, pole);
            }
            return wartosc;
        }

        /// <summary>
        /// Pobiera z pojedynczej linii uwag w postaci
        /// [NRUMOWY]:[DATAUMOWY]
        /// Wartość do określonego pola w XML.
        /// </summary>
        /// <param name="uwagi">Uwagi do dokumentu, z których pobierane są dane.</param>
        /// <param name="nrLinii">Nr linii uwag, z której pobierane będą dane.</param>
        /// <param name="pole">Pole do którego ma być wpisana wartość (DataUmowy lub NrUmowy).</param>
        /// <returns>Wartość dla pola w XML.</returns>
        private string PobierzWartoscZUwag(string uwagi, int nrLinii, string pole)
        {
            string[] linie = uwagi.Split('\r', '\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (nrLinii < linie.Length)
            {
                string linia = linie[nrLinii];
                string[] liniaPodzielona = linia.Split(':');
                switch (pole)
                {
                    case PolaEFaktury.NrUmowy:
                        {
                            return liniaPodzielona.Length > 0 ? liniaPodzielona[0] : null;                            
                        }
                    case PolaEFaktury.DataUmowy:
                        {
                            string dataTekst = null;
                            if (liniaPodzielona.Length > 1)
                            {
                                string elementLinii = liniaPodzielona[1];
                                if (DateTime.TryParse(elementLinii, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime data))
                                    dataTekst = data.ToString("yyyy-MM-dd");
                            }
                            return dataTekst;
                        }
                }
            }
            return null;
        }
    }
}
