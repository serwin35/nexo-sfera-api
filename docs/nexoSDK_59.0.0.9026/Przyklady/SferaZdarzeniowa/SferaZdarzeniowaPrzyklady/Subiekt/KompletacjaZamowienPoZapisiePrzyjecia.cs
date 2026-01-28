using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.EgzekutorMagazynowy;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SferaZdarzeniowaPrzyklady.Subiekt
{
    public class KompletacjaZamowienPoZapisiePrzyjecia : KlientSferyZdarzeniowej<IDokument>
    {
        private readonly IDokumenty _dokumentyMenedzer;
        private readonly IZamowieniaOdKlientow _zamowieniaMenedzer;

        public KompletacjaZamowienPoZapisiePrzyjecia(IDokumenty dokumentyMenedzer
            , IZamowieniaOdKlientow zamowieniaMenedzer)
        {
            _dokumentyMenedzer = dokumentyMenedzer ?? throw new ArgumentNullException(nameof(dokumentyMenedzer));
            _zamowieniaMenedzer = zamowieniaMenedzer ?? throw new ArgumentNullException(nameof(zamowieniaMenedzer));
        }

        public override void PoZapisieObiektu(IKontekstZdarzeniaPoZapisieObiektu<IDokument> kontekst)
        {
            if (kontekst.TypDanych != typeof(DokumentPZ) && kontekst.TypDanych != typeof(DokumentPW))
                return;
            if (kontekst.IdDanych == null || !(kontekst.IdDanych is int))
                return;

            int dokumentPrzyjeciaId = (int)kontekst.IdDanych;
            Dokument dokumentPrzyjecia = _dokumentyMenedzer.Dane.Wszystkie(nameof(Dokument.Pozycje)).Where(d => d.Id == dokumentPrzyjeciaId).FirstOrDefault();
            if (dokumentPrzyjecia == null)
                return;
            HashSet<int> asortymenty = dokumentPrzyjecia.Pozycje
                 .Where(p => p.AsortymentAktualnyId.HasValue && p.AsortymentAktualny.Rodzaj.StanyMagazynowe)
                 .Select(p => p.AsortymentAktualnyId.Value)
                 .Distinct()
                 .ToHashSet();
            if (asortymenty.Any())
            {
                IEnumerable<int> dokumentyZk = _zamowieniaMenedzer.Dane
                    .Wszystkie()
                    .Where(zk => !zk.Zamkniety // nie kompletujemy zamówień zamkniętych
                                && !zk.BlokujRealizacje // nie kompletujemy zamówień zablokowanych do realizacji
                                && (zk.SkutekMagazynowy == (byte)SkutekMagazynowy.Brak // kompletujemy tylko zamówienia bez rezerwacji, z rezerwacją częściową lub rezerwacją stanów
                                    || (zk.SkutekMagazynowy == (byte)SkutekMagazynowy.Rezerwacja && zk.TypRezerwacji == (byte)TypRezerwacji.Ilosciowa)
                                    || zk.SkutekMagazynowy == (byte)SkutekMagazynowy.RezerwacjaCzesciowa))
                    .SelectMany(d => d.Pozycje)
                    .Where(p => p.AsortymentAktualnyId.HasValue && asortymenty.Contains(p.AsortymentAktualnyId.Value)) // tylko zamówienia, które zawierają przyjęty asortyment
                    .Select(p => p.Dokument.Id)
                    .Distinct()
                    .ToArray();
                List<string> skompletowaneZamowienia = new List<string>();
                List<string> nieSkompletowaneZamowienia = new List<string>();
                foreach (int zamowienieId in dokumentyZk)
                {
                    DokumentZK zamowienieEncja = _zamowieniaMenedzer.Dane.Wszystkie().Where(d => d.Id == zamowienieId).FirstOrDefault();
                    if (zamowienieEncja != null)
                    {
                        using (IZamowienieOdKlienta zamowienieEdycja = _zamowieniaMenedzer.Znajdz(zamowienieEncja))
                        {
                            var pozycjeDoKompletacji = zamowienieEdycja.Dane.Pozycje
                                .Where(p => p.AsortymentAktualnyId.HasValue && asortymenty.Contains(p.AsortymentAktualnyId.Value)) // pozycje zamówienia z asortymentem, który został przyjęty
                                .ToArray();
                            var partie = dokumentPrzyjecia.Pozycje
                                .Where(p => p.AsortymentAktualnyId.HasValue && pozycjeDoKompletacji.Any(pZk => pZk.AsortymentAktualnyId.Value == p.AsortymentAktualnyId.Value)) // partie asortymentu, który występuje na kompletowanych pozycjach
                                .SelectMany(p => p.Przyjecie.Partie);
                            var wynik = zamowienieEdycja.KompletujZamowienie(dokumentPrzyjecia.DataWprowadzenia, pozycjeDoKompletacji, partie);
                            if (wynik.Any(w => w.NowaIloscSkompletowana > w.AktualnaIloscSkompletowana)) // zapisujemy tylko gdy coś uda się skompletować
                            {
                                if (zamowienieEdycja.Zapisz())
                                    skompletowaneZamowienia.Add(zamowienieEdycja.Dane.NumerWewnetrzny.PelnaSygnatura);
                                else
                                    nieSkompletowaneZamowienia.Add(zamowienieEdycja.Dane.NumerWewnetrzny.PelnaSygnatura);
                            }
                        }
                    }
                }

                if (skompletowaneZamowienia.Any() || nieSkompletowaneZamowienia.Any())
                {
                    ToastContentBuilder builder = new ToastContentBuilder();
                    if (skompletowaneZamowienia.Any())
                        builder = builder.AddText($"Skompletowano {(skompletowaneZamowienia.Count == 1 ? "zamówienie" : "zamówienia")} nr {string.Join(", ", skompletowaneZamowienia)}");
                    if (nieSkompletowaneZamowienia.Any())
                        builder = builder.AddText($"Nie udało się skompletować {(nieSkompletowaneZamowienia.Count == 1 ? "zamówienia" : "zamówień")} nr {string.Join(", ", nieSkompletowaneZamowienia)}");
                    builder.Show();
                }
            }
        }
    }
}
