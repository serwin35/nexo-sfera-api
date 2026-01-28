using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using System;

namespace KsefPluginy
{
    public class KorektaDanychMojejFirmy : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private readonly Guid _id = new Guid("9B0853C5-245C-472A-A998-CF0EEAF82864");
        private readonly IPolaWlasneAdv2AccessorFactory _polaWlasneAccessorFactory;

        public KorektaDanychMojejFirmy(IPolaWlasneAdv2AccessorFactory polaWlasneAccessorFactory)
        {
            _polaWlasneAccessorFactory = polaWlasneAccessorFactory ?? throw new ArgumentNullException(nameof(polaWlasneAccessorFactory));
        }

        public Guid Identyfikator => _id;

        public string Nazwa => "Korekta danych mojej firmy";

        public string Opis => "Dla korekt dokumentów sprzedaży oznaczonych logicznym polem własnym generowana jest sekcja danych podmiotu-sprzedawcy przed korektą.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            if (kontekst.SciezkaPola.Contains("Podmiot1K") && kontekst.Dokument is DokumentKDS kds && kds.DokumentKorygowany != null)
            {
                IPolaWlasneAdv2Accessor accessor = _polaWlasneAccessorFactory.Utworz(kontekst.Dokument, false);
                if (accessor.PobierzWartoscTypuLogicznego("Korekta danych mojej firmy") == true)
                {
                    switch (pole)
                    {
                        case PolaEFaktury.NIP:
                            wartosc = kds.DokumentKorygowany.MojaFirmaWybrana.NIP;
                            break;
                        case PolaEFaktury.Nazwa:
                            wartosc = kds.DokumentKorygowany.MojaFirmaWybrana.Nazwa;
                            break;
                        case PolaEFaktury.KodKraju:
                            wartosc = kds.DokumentKorygowany.AdresMojejFirmy.Panstwo?.KodISOAlfa2() ?? "PL";
                            break;
                        case PolaEFaktury.AdresL1:
                            wartosc = kds.DokumentKorygowany.AdresMojejFirmy.Linia1;
                            break;
                        case PolaEFaktury.AdresL2:
                            wartosc = (kds.DokumentKorygowany.AdresMojejFirmy.Linia2 + " " + kds.DokumentKorygowany.AdresMojejFirmy.Linia3).Trim();
                            break;
                    }
                }
            }

            return wartosc;
        }
    }
}
