using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using ObslugaLogistyki.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ObslugaLogistyki.Pomocnicze
{
    public class ObslugaPolWlasnychLogistyki
    {
        private readonly Lazy<IPolaWlasneAdv2AccessorFactory> _polaWlasneAdv2AccessorFactory;
        private readonly Lazy<ISlownikiWlasne> _slownikiWlasne;
        private readonly Lazy<IZaawansowanePolaWlasne> _zaawansowanePolaWlasne;

        public ObslugaPolWlasnychLogistyki(Func<IPolaWlasneAdv2AccessorFactory> polaWlasneAccessorFactory
            , Func<ISlownikiWlasne> slownikiWlasne
            , Func<IZaawansowanePolaWlasne> zaawansowanePolaWlasne)
        {
            if (polaWlasneAccessorFactory == null)
                throw new ArgumentNullException(nameof(polaWlasneAccessorFactory));
            if (slownikiWlasne == null)
                throw new ArgumentNullException(nameof(slownikiWlasne));
            if (zaawansowanePolaWlasne == null)
                throw new ArgumentNullException(nameof(zaawansowanePolaWlasne));
            _polaWlasneAdv2AccessorFactory = new Lazy<IPolaWlasneAdv2AccessorFactory>(polaWlasneAccessorFactory);
            _slownikiWlasne = new Lazy<ISlownikiWlasne>(slownikiWlasne);
            _zaawansowanePolaWlasne = new Lazy<IZaawansowanePolaWlasne>(zaawansowanePolaWlasne);
        }

        protected IPolaWlasneAdv2AccessorFactory PolaWlasneAccessorFactory => _polaWlasneAdv2AccessorFactory.Value;
        protected ISlownikiWlasne SlownikiWlasne => _slownikiWlasne.Value;
        protected IZaawansowanePolaWlasne ZaawansowanePolaWlasne => _zaawansowanePolaWlasne.Value;

        /// <summary>
        /// Pobiera wartość pola własnego słownikowego (wartość klucza) o nazwie "Samochód dostawczy" na dokumencie.
        /// </summary>
        /// <param name="dokument">Dokument, z którego pobieramy wartość pola.</param>
        /// <returns>Wartość pola własnego (klucz typu liczba całkowita).</returns>
        internal int? SprawdzCzyPoleIstniejeIPobierzWybranySamochodDostawczy(Dokument dokument)
        {
            if (dokument == null)
                throw new ArgumentException();
            if (PobierzPoleWlasneSamochodDostawczy(dokument) == null)
                return null;
            return PobierzWybranySamochodDostawczy(dokument);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dokument"></param>
        /// <returns></returns>
        internal int? PobierzWybranySamochodDostawczy(Dokument dokument)
        {
            IPolaWlasneAdv2Accessor accessor = PolaWlasneAccessorFactory.Utworz(dokument);
            return accessor.PobierzWartoscTypuSlownikWlasnySqlByInt2(Resources.NazwaPolaWlasnego);
        }

        /// <summary>
        /// Pobiera metadane pola własnego o nazwie "Samochód dostawczy".
        /// </summary>
        /// <param name="dokument">Dokument, z którego pobierane są metadane pola własnego.</param>
        /// <returns>Metadane pola własnego.</returns>
        internal IZaawansowanePoleWlasne PobierzPoleWlasneSamochodDostawczy(Dokument dokument)
        {
            IZaawansowanePoleWlasne samochodDostawczy;
            Type typEncji;
            if (!ZaawansowanePolaWlasne.ObslugujeZaawansowanePolaWlasne(typEncji = dokument.GetType()) // encja obsługuje zaawansowane pola własne
                || (samochodDostawczy = ZaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne(typEncji, Resources.NazwaPolaWlasnego)) == null) // zdefiniowano pole własne o nazwie 'Samochód dostawczy'
            {
                return null;
            }
            return samochodDostawczy;
        }

        /// <summary>
        /// Ustawia pole własne o nazwie "Samochód dostawczy" na dokumencie.
        /// </summary>
        /// <param name="dokument">Dokument, dla którego ustawiane jest pole własne.</param>
        /// <param name="kluczElementu">Klucz elementu słownika własnego.</param>
        /// <remarks>W przypadku gdy pole własne nie istnieje to na polu sposobu dostawy ustawiany jest błąd.</remarks>
        internal void UstawSamochodDostawczy(Dokument dokument, int? kluczElementu, IKontekstZdarzeniaZWalidacja kontekst)
        {
            Type typEncji = dokument.GetType();
            if (!ZaawansowanePolaWlasne.ObslugujeZaawansowanePolaWlasne(typEncji))
            {
                if (kluczElementu.HasValue)
                    kontekst.DodajBlad(string.Format(Resources.NieobslugiwanePolaWlasneKomunikat, typEncji.Name), dokument, nameof(Dokument.SposobDostawy));
                return;
            }
            if (ZaawansowanePolaWlasne.PobierzZaawansowanePoleWlasne(typEncji, Resources.NazwaPolaWlasnego) == null)
            {
                if (kluczElementu.HasValue)
                    kontekst.DodajBlad(string.Format(Resources.BrakPolaWlasnegoKomunikat, Resources.NazwaPolaWlasnego), dokument, nameof(Dokument.SposobDostawy));
                return;
            }
            IPolaWlasneAdv2Accessor accessor = PolaWlasneAccessorFactory.Utworz(dokument);
            accessor.UstawWartoscTypuSlownikWlasnySqlByInt(Resources.NazwaPolaWlasnego, kluczElementu);
        }

        /// <summary>
        /// Tworzy okno wyboru samochodu dostawczego.
        /// </summary>
        /// <param name="nazwaSposobuDostawy">Nazwa sposobu dostawy na dokumencie.</param>
        /// <param name="komunikatBledu">Komunikat błędu w przypadku niepoprawnie zdefiniowanego słownika samochodów dostawczych.</param>
        /// <returns>Obiekt okna wyboru samochodu dostawczego lub <c>null</c> w przypadku gdy słownik samochodów dostawczych nie został zdefiniowany lub został zdefiniowany niepoprawnie.</returns>
        internal List<ElementSlownikowegoZrodlaDanych<int>> PobierzZrodloDanychSlownikaSamochodowDostawczych(out string komunikatBledu)
        {
            List<ElementSlownikowegoZrodlaDanych<int>> dataSource = null;
            komunikatBledu = string.Empty;
            if (SlownikiWlasne.IstniejeSlownikWlasnySql(Resources.NazwaSlownikaWlasnego, out int idSlownikaWlasnegoSql)) // jeśli istnieje słownik własny o nazwie "Samochody dostawcze"
            {
                ISlownikoweZrodloDanych zrodloDanych = SlownikiWlasne.PobierzDefinicjeSlownikaWlasnegoSql(idSlownikaWlasnegoSql);
                if (zrodloDanych.TypKlucza == typeof(int)) // i typem klucza w tym słowniku jest "liczba całkowita" to tworzymy dialog wyboru samochodu dostawczego
                {
                    dataSource = ((ISlownikoweZrodloDanych<int>)zrodloDanych).UtworzZapytanieFiltrowaneLinq().OrderBy(e => e.Wartosc).ToList();
                    if (!dataSource.Any()) // jeśli w słowniku nie ma żadnych elementów
                        komunikatBledu = Resources.BrakElementowSlownikaSamochodowDostawczychKomunikat;
                }
                else
                    komunikatBledu = string.Format(Resources.NiepoprawnieZdefiniowanySlownikSamochodowDostawczychKomunikat, Resources.NazwaSlownikaWlasnego);
            }
            else
                komunikatBledu = string.Format(Resources.BrakSlownikaSamochodowDostawczychKomunikat, Resources.NazwaSlownikaWlasnego);
            return dataSource;
        }
    }
}
