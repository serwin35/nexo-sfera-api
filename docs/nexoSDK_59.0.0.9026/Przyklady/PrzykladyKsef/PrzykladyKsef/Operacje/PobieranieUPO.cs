using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrzykladyKsef.Operacje
{
    /// <summary>
    /// W tym przykładzie zaprezentowano sposób pobierania UPO dla zbioru wysłanych e-Faktur.
    /// </summary>
    public class PobieranieUPO : BazowaOperacja
    {
        public PobieranieUPO(Uchwyt uchwyt) : base(uchwyt) { }

        public override void Wykonaj()
        {
            PobierzUPODlaDokumentowWyslanychWDniu(DateTime.Today);
        }

        private void PobierzUPODlaDokumentowWyslanychWDniu(DateTime dataWysylki)
        {
            IKoordynatorWysylaniaEFaktur koordynatorWysylania = _sfera.KoordynatorWysylaniaEFaktur();
            IDokumentyElektroniczne dokumentyElektroniczne = _sfera.DokumentyElektroniczne();
            DateTime dataKoncowa = dataWysylki.AddDays(1); // data wysyłki zapisywana jest wraz z godziną co trzeba uwzględnić przy odpytywaniu o e-Faktury
            List<DokumentElektroniczny> dokumentyWyslane = dokumentyElektroniczne.Dane.Wszystkie()
                .Where(d => d.EStatus == (byte)StatusWysylkiElektronicznejEFaktur.PobranyNumerKsef
                            && d.DataWysylki.HasValue
                            && d.DataWysylki.Value >= dataWysylki
                            && d.DataWysylki.Value < dataKoncowa)
                .ToList();
            List<IWynikPobieraniaUpo> wynikPobieraniaUpo = koordynatorWysylania.PobierzUpo(dokumentyWyslane);
            foreach (IWynikPobieraniaUpo wynik in wynikPobieraniaUpo)
            {
                if (wynik.Sukces)
                    Console.WriteLine($"Poprawnie pobrano UPO dla dokumentu {wynik.NumerDokumentu} o numerze KSeF {wynik.NumerKsef}.");
                else
                    Console.WriteLine($"Pobieranie UPO dla dokumentu {wynik.NumerDokumentu} o numerze KSeF {wynik.NumerKsef} zakończyło się błędem: {string.Join(Environment.NewLine, wynik.Bledy)}");
            }
        }
    }
}
