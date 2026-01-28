using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Xml;
using System.Linq;
using InsERT.Moria.Sfera;

namespace KsefPluginyImport
{
    /// <summary>
    /// Funkcja wypełnia pola własne dokumentu na podstawie sekcji transport z faktury ustrukturyzowanej.
    /// Opis struktury pól własnych jest umieszczony w przykładach funkcji generowania wartości pól faktury ustrukturyzowanej: GenerowanieSekcjiTransport
    /// </summary>
    public class WypelnianieSekcjiTransport : IDodatkowaFunkcjaImportuFaktury
    {
        private readonly IUchwyt _uchwyt;
        private readonly Guid _id = new Guid("68BF2E27-91B9-4ECA-8807-8EDF00EA1F9C");

        public WypelnianieSekcjiTransport(IUchwyt uchwyt)
        {
            _uchwyt = uchwyt ?? throw new ArgumentNullException(nameof(uchwyt));
        }

        public Guid Identyfikator => _id;

        public string Nazwa => "Wypełnianie sekcji transport";

        public string Opis => "Funkcja wypełnia sekcję transport na dokumencie zdefiniowaną w polach własnych";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public void Przetworz(IDokument dokument, IDaneEFaktury daneFaktury, string trescXml)
        {
            XmlDocument dokumentXml = new XmlDocument();
            dokumentXml.LoadXml(trescXml);

            IWersjaSchemyKsef wersjaSchemy = _uchwyt.WersjaSchemyKsef();
            string defaultNamespace = wersjaSchemy.DomyslnaPrzestrzenNazw(daneFaktury.Dokument);
            XmlNodeList sekcjaTransport = dokumentXml.GetElementsByTagName("Transport", defaultNamespace);
            IPolaWlasneAdv2Accessor accessor = _uchwyt.UtworzPolaWlasneAdv2Accessor(dokument.Dokument);
            if (sekcjaTransport.Count == 1)
            {
                XmlNode nodeTransport = sekcjaTransport.Item(0);
                foreach (XmlNode node in nodeTransport)
                {
                    switch (node.LocalName)
                    {
                        case PolaEFaktury.RodzajTransportu:
                            int? rodzaj = ReadInteger(node);
                            if (rodzaj.HasValue)
                                UstawWartoscSlownikowa(accessor, "Rodzaj transportu", rodzaj.Value, 1);
                            break;
                        case PolaEFaktury.NrZleceniaTransportu:
                            string nrZlecenia = ReadString(node);
                            if (!string.IsNullOrEmpty(nrZlecenia))
                                accessor.UstawWartoscTypuTekst("Numer zlecenia transportu", nrZlecenia);
                            break;
                        case PolaEFaktury.OpisLadunku:
                            int? opis = ReadInteger(node);
                            if (opis.HasValue)
                                UstawWartoscSlownikowa(accessor, "Opis ładunku", opis.Value, 2);
                            break;
                        case PolaEFaktury.JednostkaOpakowania:
                            string jednostka = ReadString(node);
                            if (!string.IsNullOrEmpty(jednostka))
                                accessor.UstawWartoscTypuTekst("Jednostka opakowania", jednostka);
                            break;
                        case PolaEFaktury.DataGodzRozpTransportu:
                            DateTime? dataRozp = ReadDate(node);
                            if (dataRozp.HasValue)
                                accessor.UstawWartoscTypuData("Data rozpoczęcia transportu", dataRozp);
                            break;
                        case PolaEFaktury.DataGodzZakTransportu:
                            DateTime? dataZak = ReadDate(node);
                            if (dataZak.HasValue)
                                accessor.UstawWartoscTypuData("Data zakończenia transportu", dataZak);
                            break;

                    }
                }
            }

            // pobieranie listy węzłów o nazwie "PelnaNazwa"
            XmlNodeList nazwaPelna = daneFaktury.Dokument.WersjaSchemy == 1
                ? dokumentXml.GetElementsByTagName(PolaEFaktury.PelnaNazwa, defaultNamespace)
                : dokumentXml.GetElementsByTagName(PolaEFaktury.Nazwa, defaultNamespace);
            if (nazwaPelna.Count > 1)
            {
                foreach (XmlNode node in nazwaPelna)
                {
                    // jeśli nadrzędnymi węzłami są węzły o nazwie "DaneIdentyfikacyjne" oraz "Przewoznik"
                    if (node.ParentNode?.LocalName == "DaneIdentyfikacyjne" && node.ParentNode.ParentNode?.LocalName == "Przewoznik")
                    {
                        string nazwa = ReadString(node);
                        if (!string.IsNullOrEmpty(nazwa))
                        {
                            // identyfikujemy przewoźnika ze słownika własnego po nazwie:
                            var elementy = accessor.PobierzWartosciTypuSlownikWlasnySqlByInt("Przewoźnik");
                            var wybrany = elementy.Where(e => e.Wartosc.Equals(nazwa, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                            if (wybrany != null)
                                accessor.UstawWartoscTypuSlownikWlasnySqlByInt("Przewoźnik", wybrany.Klucz);

                        }
                    }
                }
            }
        }

        /// <summary>
        /// Ustawia wartość słownikową na podstawie wartości liczbowej z dokumentu XML.
        /// </summary>
        private void UstawWartoscSlownikowa(IPolaWlasneAdv2Accessor accessor, string nazwaPola, int wartoscXml, int liczbaZnakow)
        {
            var elementy = accessor.PobierzWartosciTypuSlownikWlasny(nazwaPola);
            var wybranyElement = elementy.FirstOrDefault(e =>
            {
                int.TryParse(new string(e.Wartosc.Take(liczbaZnakow).ToArray()), out int wartosc);
                return wartosc == wartoscXml;
            });
            if (wybranyElement != null)
                accessor.UstawWartoscTypuSlownikWlasny(nazwaPola, wybranyElement.Klucz);
        }

        private int? ReadInteger(XmlNode node)
        {
            int? result = null;
            if (node.ChildNodes.Count > 0 && int.TryParse(node.ChildNodes.Item(0).Value, out int value))
                result = value;
            return result;
        }

        private string ReadString(XmlNode node)
        {
            if (node.ChildNodes.Count > 0)
                return node.ChildNodes.Item(0).Value;
            return string.Empty;
        }

        private DateTime? ReadDate(XmlNode node)
        {
            DateTime? data = null;
            if (node.ChildNodes.Count > 0 && DateTime.TryParse(node.ChildNodes.Item(0).Value, out DateTime value))
                data = value;
            return data;
        }
    }
}
