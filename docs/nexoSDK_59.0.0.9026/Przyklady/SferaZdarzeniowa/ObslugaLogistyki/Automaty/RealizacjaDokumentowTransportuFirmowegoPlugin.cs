using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using InsERT.Mox.DataAccess.EntityFramework;
using ObslugaLogistyki.Parametry;
using ObslugaLogistyki.Pomocnicze;
using ObslugaLogistyki.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ObslugaLogistyki.Automaty
{
    public class RealizacjaDokumentowTransportuFirmowegoPlugin : KlientSferyZdarzeniowej<IDokument>
    {
        private readonly ObslugaPolWlasnychLogistyki _obslugaPolWlasnych;

        public RealizacjaDokumentowTransportuFirmowegoPlugin(Func<IPolaWlasneAdv2AccessorFactory> polaWlasneAccessorFactory
            , Func<ISlownikiWlasne> slownikiWlasne
            , Func<IZaawansowanePolaWlasne> zaawansowanePolaWlasne)
        {
            _obslugaPolWlasnych = new ObslugaPolWlasnychLogistyki(polaWlasneAccessorFactory, slownikiWlasne, zaawansowanePolaWlasne);
        }

        public override void PoZmianieWlasciwosciObiektu(IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst, ParametryObslugi.Instance.ObslugujRealizacje))
                return;
            if (!kontekst.NazwaWlasciwosci.Equals(nameof(Dokument.DokumentyRealizowane)))
                return;
            if (kontekst.Dane is Dokument dokument)
            {
                if (dokument is DokumentDS dsWz && dsWz.DokumentyRealizowane.Any(d => d is DokumentWZ wz && !wz.PowstalZHandlowego))
                {
                    // realizacja WZ --> DS
                    if (ParametryObslugi.Instance.PrzepisujSamochodDostawczyPrzyRealizacji.HasFlag(TypObslugiwanejRealizacji.WzDs))
                        PrzepiszSamochodDostawczyPrzyRealizacji(dsWz, dsWz.DokumentyRealizowane.Where(d => d is DokumentWZ wz && !wz.PowstalZHandlowego).ToArray(), kontekst);
                }
                else if (dokument is DokumentDS dsZk && dsZk.DokumentyRealizowane.Any(d => d is DokumentZK))
                {
                    // realizacja ZK --> DS
                    if (ParametryObslugi.Instance.PrzepisujSamochodDostawczyPrzyRealizacji.HasFlag(TypObslugiwanejRealizacji.ZkDs))
                        PrzepiszSamochodDostawczyPrzyRealizacji(dsZk, dsZk.DokumentyRealizowane.Where(d => d is DokumentZK).ToArray(), kontekst);
                }
                else if (dokument is DokumentWZ wz && wz.DokumentyRealizowane.Any(d => d is DokumentZK))
                {
                    // realizacja ZK --> WZ
                    if (ParametryObslugi.Instance.PrzepisujSamochodDostawczyPrzyRealizacji.HasFlag(TypObslugiwanejRealizacji.ZkWz))
                        PrzepiszSamochodDostawczyPrzyRealizacji(wz, wz.DokumentyRealizowane.Where(d => d is DokumentZK).ToArray(), kontekst);
                }
            }
            else
                throw new ArgumentException(); // sytuacja niespodziewana
        }

        private void PrzepiszSamochodDostawczyPrzyRealizacji(Dokument dokumentRealizujacy, IEnumerable<Dokument> dokumentyRealizowane, IKontekstZdarzeniaPoZmianieWlasciwosciObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.CzyTransportFirmowy(dokumentRealizujacy))
                return;
            IZaawansowanePoleWlasne samochodDostawczy = _obslugaPolWlasnych.PobierzPoleWlasneSamochodDostawczy(dokumentRealizujacy);
            if (samochodDostawczy == null)
                return;
            IEnumerable<int> samochodyDostawcze = dokumentyRealizowane.Select(_obslugaPolWlasnych.SprawdzCzyPoleIstniejeIPobierzWybranySamochodDostawczy).Where(s => s.HasValue).Select(s => s.Value).Distinct().ToArray();
            if (samochodyDostawcze.Any())
            {
                object polaWlasne = dokumentRealizujacy is DokumentDS ds ? ds.PolaWlasneAdv2 : dokumentRealizujacy is DokumentWZ wz ? (object)wz.PolaWlasneAdv2 : null;
                _obslugaPolWlasnych.UstawSamochodDostawczy(dokumentRealizujacy, samochodyDostawcze.First(), kontekst);
                if (samochodyDostawcze.Count() > 1)
                {
                    if (polaWlasne != null)
                        kontekst.DodajOstrzezenie(Resources.WieleSamochodowDostawczychNaDokumentachRealizowanychKomunikat, polaWlasne as EntityDataObjectBase, samochodDostawczy.Id);
                    else
                        kontekst.DodajOstrzezenie(Resources.WieleSamochodowDostawczychNaDokumentachRealizowanychKomunikat, dokumentRealizujacy, nameof(Dokument.SposobDostawy));
                }
            }
        }
    }
}
