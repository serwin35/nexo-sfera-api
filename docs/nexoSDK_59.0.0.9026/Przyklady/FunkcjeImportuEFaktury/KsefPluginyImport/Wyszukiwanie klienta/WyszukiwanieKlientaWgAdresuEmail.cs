using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Linq;

namespace KsefPluginyImport
{
    /// <summary>
    /// Wyszukuje klienta w kartotece wg adresu e-mail podanego w danych podmiotu faktury ustrukturyzowanej.
    /// </summary>
    public class WyszukiwanieKlientaWgAdresuEmail : IFunkcjaWyszukiwaniaPodmiotu
    {
        private readonly Guid _id = new Guid("A5A0683C-BDEC-4834-BEF1-FDA20F5A4C68");
        private readonly IPodmioty _podmiotyMenedzer;

        public WyszukiwanieKlientaWgAdresuEmail(IPodmioty podmiotyMenedzer)
        {
            _podmiotyMenedzer = podmiotyMenedzer ?? throw new ArgumentNullException(nameof(podmiotyMenedzer));
        }

        public Guid Identyfikator => _id;

        public string Nazwa => "Wg adresu e-mail";

        public string Opis => "Funkcja wyszukuje klienta w kartotece wg adresu e-mail.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        /// <param name="_">W tym przykładzie parametr z encją e-Faktury jest nieużywany więc można go zignorować.</param>
        public PodmiotHistoria Wyszukaj(DokumentElektroniczny _, IDanePodmiotuFaktury danePodmiotu)
        {
            if (string.IsNullOrWhiteSpace(danePodmiotu.Email))
                return null;
            return (from podmiot in _podmiotyMenedzer.Dane.Wszystkie()
                    let pasujacyKontakt = podmiot.Kontakty.Where(k => k.Wartosc.Equals(danePodmiotu.Email)).FirstOrDefault()
                    where pasujacyKontakt != null
                           && pasujacyKontakt.Rodzaj.Nazwa.Equals("e-mail")
                    select podmiot.Aktualny).FirstOrDefault();
        }
    }
}
