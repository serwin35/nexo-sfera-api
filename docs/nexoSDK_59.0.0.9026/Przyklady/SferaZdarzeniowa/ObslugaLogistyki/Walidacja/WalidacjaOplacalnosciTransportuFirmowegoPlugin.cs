using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Waluty;
using InsERT.Mox.DataAccess.EntityFramework;
using ObslugaLogistyki.Parametry;
using ObslugaLogistyki.Pomocnicze;
using ObslugaLogistyki.Properties;
using System;

namespace ObslugaLogistyki.Walidacja
{
    public class WalidacjaOplacalnosciTransportuFirmowegoPlugin : KlientSferyZdarzeniowej<IDokument>
    {
        private readonly IParametryWalut _parametryWalut;
        private readonly Lazy<Waluta> _walutaSystemowa;
        private readonly IJednostkiMiar _jednostkiMiar;
        private readonly Lazy<JednostkaMiary> _kilogram;
        public WalidacjaOplacalnosciTransportuFirmowegoPlugin(IParametryWalut parametryWalut
            , IJednostkiMiar jednostkiMiar)
        {
            _parametryWalut = parametryWalut ?? throw new ArgumentNullException(nameof(parametryWalut));
            _walutaSystemowa = new Lazy<Waluta>(() => _parametryWalut.WalutaSystemowa);
            _jednostkiMiar = jednostkiMiar ?? throw new ArgumentNullException(nameof(jednostkiMiar));
            _kilogram = new Lazy<JednostkaMiary>(() => _jednostkiMiar.DaneDomyslne.Kilogram);
        }

        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst, ParametryObslugi.Instance.SprawdzajOplacalnoscTransportuFirmowego))
                return;
            if (!kontekst.NazwaWlasciwosci.Equals(nameof(Dokument.MasaTowarow))
                && !kontekst.NazwaWlasciwosci.Equals(nameof(Dokument.KwotaDoZaplaty))
                && !kontekst.NazwaWlasciwosci.Equals(nameof(Dokument.SposobDostawy)))
                return;

            Dokument dokument = kontekst.ObiektBiznesowy.Dokument;
            if (ObslugaLogistykiHelper.CzyTransportFirmowy(dokument) && !CzyTransportFirmowyOplacalny(dokument))
            {
                kontekst.DodajOstrzezenie(string.Format(Resources.NieoplacalnyTransportFirmowyKomunikat
                    , ParametryObslugi.Instance.MinimalnaMasaDlaTransportuFirmowego
                    , ParametryObslugi.Instance.MinimalnaKwotaDlaTransportuFirmowego
                    , _walutaSystemowa.Value.Symbol), kontekst.Dane as EntityDataObjectBase, nameof(Dokument.SposobDostawy));
            }
            else
                kontekst.UsunOstrzezenie(kontekst.Dane as EntityDataObjectBase, nameof(Dokument.SposobDostawy));
        }

        private bool CzyTransportFirmowyOplacalny(Dokument dokument)
        {
            if (dokument.Pozycje.Count == 0)
                return true;
            if (!dokument.MasaTowarow.HasValue || !dokument.JednostkaMasyTowarowId.HasValue)
                return true;
            if (dokument.MasaTowarow.Value == 0m)
                return true; // szczególny przypadek - brak uzupełnionej masy w kartotece towarów, nie chcemy ostrzegać na siłę
            return KwotaDoZaplatyPln(dokument) >= ParametryObslugi.Instance.MinimalnaKwotaDlaTransportuFirmowego
                && MasaTowarowKg(dokument) >= ParametryObslugi.Instance.MinimalnaMasaDlaTransportuFirmowego;
        }

        private decimal KwotaDoZaplatyPln(Dokument dokument)
        {
            decimal kwota = dokument.KwotaDoZaplaty;
            if (dokument.Waluta.Id != _walutaSystemowa.Value.Id
                && dokument.KursWalutyDokumentu != null)
                kwota = Math.Round(kwota * dokument.KursWalutyDokumentu.ZnormalizujKurs(), _walutaSystemowa.Value.Precyzja);
            return kwota;
        }

        private decimal MasaTowarowKg(Dokument dokument)
        {
            decimal masa = dokument.MasaTowarow.GetValueOrDefault(0m);
            if (masa > 0m
                && dokument.JednostkaMasyTowarowId.HasValue
                && dokument.JednostkaMasyTowarowId != _kilogram.Value.Id)
                masa = dokument.JednostkaMasyTowarow.PrzeliczIloscNaJednostke(masa, _kilogram.Value);
            return masa;
        }
    }
}
