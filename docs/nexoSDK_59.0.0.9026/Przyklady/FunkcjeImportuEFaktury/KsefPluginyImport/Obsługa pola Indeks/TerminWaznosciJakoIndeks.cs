using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Text.RegularExpressions;

namespace KsefPluginyImport
{
    public class TerminWaznosciJakoIndeks : IFunkcjaObslugiPolaIndeks
    {
        private readonly Regex _terminWaznosciRegex = new Regex(@"^T:(?<data>\d{4}-\d{2}-\d{2})$");
        private readonly Guid _id = new Guid("8ED3CA76-5A04-44FC-8CBC-D1A919C6D48F");

        public Guid Identyfikator => _id;

        public string Nazwa => "Jako termin ważności";

        public string Opis => "Funkcja importuje wartość pola Indeks/DodatkoweInfo jako termin ważności dostawy.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public bool CzyMoznaUzyc(IDokument dokument, PozycjaDokumentu pozycja) => dokument is IDokumentZRozbiciem // dokument tworzy rozbicie
            && pozycja.AsortymentAktualny != null // asortyment na pozycji nie jest usługą jednorazową
            && pozycja.AsortymentAktualny.Rodzaj.StanyMagazynowe // asortyment na pozycji ma stany magazynowe (nie jest usługą kartotekową ani opłatą dodatkową)
            && pozycja.CzyPrzyjmujaca(true) // pozycja dokumentu tworzy przyjęcie
            && (!(pozycja.Dokument is DokumentKDZ) || pozycja.Dokument.SkutekMagazynowy == (byte) SkutekMagazynowy.SkutekMagazynowy);

        public void Obsluz(IDokument dokument, PozycjaDokumentu pozycja, string indeks)
        {
            if (string.IsNullOrWhiteSpace(indeks))
                return;
            if (dokument == null)
                throw new ArgumentNullException(nameof(dokument));
            if (pozycja == null)
                throw new ArgumentNullException(nameof(pozycja));

            Match terminWaznosciMatch = _terminWaznosciRegex.Match(indeks);
            if (!terminWaznosciMatch.Success)
                return;
            if (DateTime.TryParse(terminWaznosciMatch.Groups["data"].Value, out DateTime termin))
            {

                if (dokument is IDokumentZRozbiciem dokumentZRozbiciem)
                {
                    IRozbiciePozycji rozbicie = null;
                    try
                    {
                        rozbicie = dokumentZRozbiciem.RozpocznijRozbicie(pozycja);
                        if (rozbicie is IRozbiciePozycjiPrzyjeciowe rozbiciePrzyjeciowe)
                        {
                            foreach (IPozycjaRozbiciaPrzyjeciowa pozycjaRozbicia in rozbiciePrzyjeciowe.Pozycje)
                                pozycjaRozbicia.TerminWaznosci = termin;
                        }
                    }
                    finally
                    {
                        if (rozbicie != null)
                            rozbicie.ZakonczRozbicie();
                    }
                }
            }
        }
    }
}
