using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using System;

namespace KsefPluginy
{
    /// <summary>
    /// Funkcja na podstawie pól własnych o nazwach "Data początkowa" i "Data końcowa" wypełnia sekcję OkresFa zastępujący pole P_6 (datę zakończenia dostawy).
    /// </summary>
    public class GenerowanieSekcjiOkresFa : IFunkcjaGenerowaniaWartosciPolEFaktury
    {
        private readonly Guid _id = new Guid("E37E0EA0-4AB9-4D59-9734-50E06703BBB6");
        private readonly IUchwyt _uchwyt;

        public GenerowanieSekcjiOkresFa(IUchwyt uchwyt)
        {
            _uchwyt = uchwyt ?? throw new ArgumentNullException(nameof(uchwyt));
        }

        public Guid Identyfikator => _id;

        public string Nazwa => "Wypełnianie sekcji OkresFa";

        public string Opis => "Funkcja wypełnia pola P_6_Od / P_6_Do w sekcji OkresFa na podstawie pól własnych.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujWartoscPola(string pole, IKontekstPolaEFaktury kontekst, out bool ignorujPole)
        {
            string wartosc = null;
            ignorujPole = false;

            switch (pole)
            {
                case PolaEFaktury.P_6_Od:
                    wartosc = PobierzDate("Data początkowa", kontekst.Dokument);
                    break;
                case PolaEFaktury.P_6_Do:
                    wartosc = PobierzDate("Data końcowa", kontekst.Dokument);
                    break;
            }

            return wartosc;
        }

        private string PobierzDate(string nazwaPola, Dokument dokument)
        {
            IPolaWlasneAdv2Accessor accessor = _uchwyt.UtworzPolaWlasneAdv2Accessor(dokument, createAndAttachIfNull: false);
            if (accessor != null)
            {
                DateTime? data = accessor.PobierzWartoscTypuData(nazwaPola);
                return data.HasValue ? data.Value.ToString("yyyy-MM-dd") : null;
            }
            return null;
        }
    }
}
