using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Dzialania;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using InsERT.Mox.ObiektyBiznesowe;
using ObslugaLogistyki.Pomocnicze;
using ObslugaLogistyki.Properties;
using ObslugaLogistyki.UI;
using System;
using System.Linq;
using System.Windows.Controls;

namespace ObslugaLogistyki.Automaty
{
    public class AutomatycznaRezerwacjaSamochoduDostawczegoPlugin : KlientSferyZdarzeniowej<IDokument>
    {
        private readonly IZasoby _zasoby;
        private readonly IRezerwacjeZasobow _rezerwacje;
        private readonly IDokumenty _dokumenty;
        private readonly IDzialania _dzialania;
        private readonly ISzablonyDzialan _szablonyDzialan;
        private readonly ITypyDzialan _typyDzialan;
        private readonly ObslugaPolWlasnychLogistyki _obslugaPolWlasnych;

        public AutomatycznaRezerwacjaSamochoduDostawczegoPlugin(IZasoby zasoby
            , IRezerwacjeZasobow rezerwacje
            , IDokumenty dokumenty
            , IDzialania dzialania
            , ISzablonyDzialan szablonyDzialan
            , ITypyDzialan typyDzialan
            , Func<IPolaWlasneAdv2AccessorFactory> polaWlasneAccessorFactory
            , Func<ISlownikiWlasne> slownikiWlasne
            , Func<IZaawansowanePolaWlasne> zaawansowanePolaWlasne)
        {
            _zasoby = zasoby ?? throw new ArgumentNullException(nameof(zasoby));
            _rezerwacje = rezerwacje ?? throw new ArgumentNullException(nameof(rezerwacje));
            _dokumenty = dokumenty ?? throw new ArgumentNullException(nameof(dokumenty));
            _dzialania = dzialania ?? throw new ArgumentNullException(nameof(dzialania));
            _szablonyDzialan = szablonyDzialan ?? throw new ArgumentNullException(nameof(szablonyDzialan));
            _typyDzialan = typyDzialan ?? throw new ArgumentNullException(nameof(typyDzialan));
            _obslugaPolWlasnych = new ObslugaPolWlasnychLogistyki(polaWlasneAccessorFactory, slownikiWlasne, zaawansowanePolaWlasne);
        }

        public override void PoZapisieObiektu(IKontekstZdarzeniaPoZapisieObiektu<IDokument> kontekst)
        {
            if (!ObslugaLogistykiHelper.SprawdzWarunkiWstepne(kontekst))
                return;
            int dokumentId = (int)kontekst.IdDanych;
            if (!ObslugaLogistykiHelper.CzyDokumentRezerwujeSamochodDostawczy(dokumentId, _dokumenty))
                return;

            Dokument dokument = _dokumenty.Dane.Wszystkie().Where(d => d.Id == dokumentId).FirstOrDefault();
            if (dokument == null)
                throw new InvalidOperationException();

            int? samochodDostawczyId = _obslugaPolWlasnych.SprawdzCzyPoleIstniejeIPobierzWybranySamochodDostawczy(dokument);
            if (!samochodDostawczyId.HasValue || samochodDostawczyId.Value <= 0)
                return;

            Zasob samochodDostawczy = PobierzZasob(samochodDostawczyId.Value);
            if (samochodDostawczy == null)
            {
                ObsluzKomunikat(Resources.BrakZasobuWBazieDanychKomunikat);
                return;
            }

            DateTime dataDostawy = dokument is DokumentDS ds ? ds.DataSprzedazy : dokument.DataWprowadzenia;
            DaneCzasuDostawy godzinyDostawy = WyznaczCzasDostawy(samochodDostawczyId.Value, dataDostawy);

            string komunikatZmianyDaty = string.Empty;
            if (dataDostawy != godzinyDostawy.DataKoncowa && !ZmienDateDostawyDokumentu(dokument, godzinyDostawy.DataKoncowa, out komunikatZmianyDaty))
            {
                ObsluzKomunikat(komunikatZmianyDaty);
                return;
            }
            UtworzZadanie(samochodDostawczy
                , dokument
                , godzinyDostawy
                , out string komunikatTworzeniaZadania);
            ObsluzKomunikat(string.Format("{0}{1}{2}"
                , string.IsNullOrEmpty(komunikatZmianyDaty) ? string.Empty : komunikatZmianyDaty
                , string.IsNullOrEmpty(komunikatZmianyDaty) ? string.Empty : Environment.NewLine
                , komunikatTworzeniaZadania));

        }

        private Zasob PobierzZasob(int zasobId) => _zasoby.Dane.Wszystkie().Where(z => z.Id == zasobId).FirstOrDefault();

        private DaneCzasuDostawy WyznaczCzasDostawy(int zasobId, DateTime dataDostawy)
        {
            RezerwacjaZasobu ostatniaRezerwacjaZasobu = _rezerwacje.Dane.Wszystkie()
                .Where(r => r.Zasob.Id == zasobId && r.KoniecDzien >= dataDostawy)
                .OrderByDescending(r => r.KoniecDzien)
                .ThenByDescending(r => r.KoniecGodzina)
                .FirstOrDefault();
            if (ostatniaRezerwacjaZasobu == null) // brak rezerwacji
                return new DaneCzasuDostawy(dataDostawy);
            else if (ostatniaRezerwacjaZasobu.KoniecGodzina < new TimeSpan(15, 0, 0)) // ostatnia rezerwacja kończy się przed 15
                return new DaneCzasuDostawy(dataDostawy, ostatniaRezerwacjaZasobu.KoniecGodzina.Add(new TimeSpan(0, 15, 0)));
            else
                return new DaneCzasuDostawy(ostatniaRezerwacjaZasobu.KoniecDzien.AddDays(1));
        }

        private bool ZmienDateDostawyDokumentu(Dokument dokument, DateTime nowaDataDostawy, out string komunikat)
        {
            bool zapisano = false;
            komunikat = null;

            using (IObiektBiznesowy dokumentEdycja = _dokumenty.Znajdz(dokument))
            {
                string rodzajDaty = string.Empty;
                if (dokumentEdycja is IDokumentSprzedazy dsBo)
                {
                    rodzajDaty = "zakończenia dostawy";
                    dsBo.Dane.DataSprzedazy = nowaDataDostawy;
                }
                else if (dokumentEdycja is IDokument dokumentBo)
                {
                    rodzajDaty = "wystawienia";
                    dokumentBo.Dokument.DataWprowadzenia = nowaDataDostawy;
                }
                else
                    throw new InvalidOperationException(); // sytuacja niespodziewana
                if (zapisano = dokumentEdycja.Zapisz())
                    komunikat = string.Format(Resources.ZmienionoDateDokumentuDostawyKomunikat, rodzajDaty);
                else
                    komunikat = dokumentEdycja.WypiszBledy();
            }
            return zapisano;
        }

        private bool UtworzZadanie(Zasob zasob
            , Dokument dokument
            , DaneCzasuDostawy czasDostawy
            , out string komunikat)
        {
            bool zapisano = false;
            SzablonDzialania szablon = _szablonyDzialan.DomyslnySzablonDlaTypu(_typyDzialan.DaneDomyslne.Zadanie);
            komunikat = null;
            using (IDzialanie dzialanie = _dzialania.UtworzNaPodstawieSzablonu(szablon))
            {
                RezerwacjaZasobu rezerwacja = dzialanie.DodajRezerwacje(zasob);
                dzialanie.Dane.PlanowanyPoczatekDzien = rezerwacja.PoczatekDzien = czasDostawy.DataPoczatkowa;
                dzialanie.Dane.PlanowanyPoczatekGodzina = rezerwacja.PoczatekGodzina = czasDostawy.GodzinaPoczatkowa;
                dzialanie.Dane.PlanowanyKoniecDzien = rezerwacja.KoniecDzien = czasDostawy.DataKoncowa;
                dzialanie.Dane.PlanowanyKoniecGodzina = rezerwacja.KoniecGodzina = czasDostawy.GodzinaKoncowa;
                dzialanie.Dane.OpisKrotki = rezerwacja.Komentarz = $"Transport dla dokumentu {dokument.NumerWewnetrzny.PelnaSygnatura}";
                dzialanie.Dane.Dokument = dokument;
                if (zapisano = dzialanie.Zapisz())
                    komunikat = string.Format(Resources.ZarezerwowanoSamochodDostawczyKomunikat, zasob.Nazwa, dzialanie.Dane.Sygnatura.PelnaSygnatura);
                else
                    komunikat = dzialanie.WypiszBledy();
            }
            return zapisano;
        }

        private void ObsluzKomunikat(string komunikat) => new Komunikat(komunikat).ShowDialog();
    }
}
