using InsERT.Moria;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Listy;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using InsERT.Moria.Sfera;
using InsERT.Mox.DataAccess;
using InsERT.Mox.DataAccess.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OperacjeSferyczne.OperacjeNaDokumentach
{
    public class ZmienPolaWlasne : OperacjaNaLiscieDanych<Dokument, int>
    {
        private static readonly MethodInfo _utworzParametrWyboruGenericMI = typeof(ZmienPolaWlasne).GetMethod(nameof(UtworzParametrListyWyboruGeneric), BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo _utworzParametrListyWyboruEncjiGenericMI = typeof(ZmienPolaWlasne).GetMethod(nameof(UtworzParametrListyWyboruEncjiGeneric), BindingFlags.Static | BindingFlags.NonPublic);

        public override string Nazwa => "Zmień pola własne";

        protected override string[] SciezkaWMenu => new[] { "Zbiorczo" };

        protected override void Wykonaj(
            IReadOnlyCollection<int> identyfikatoryWybranychElementow,
            IKontekstListyDanych kontekstListyDanych,
            IKontekstOperacji kontekstOperacji)
        {
            IUchwyt uchwyt = kontekstOperacji.Uchwyt;
            List<Dokument> wybraneDokumenty = PobierzWybraneDokumenty(identyfikatoryWybranychElementow, uchwyt);

            if (CzyDokumentyRoznychTypow(wybraneDokumenty))
            {
                WyswietlBladORoznychTypachDokumentow(uchwyt);

                return;
            }

            Type typDokumentu = wybraneDokumenty[0].GetType();
            List<IZaawansowanePoleWlasne> polaWlasne = PobierzPolaWlasne(uchwyt, wybraneDokumenty);

            if (polaWlasne.Count == 0)
            {
                WyswietlBladOBrakuPolWlasnych(uchwyt, typDokumentu);

                return;
            }

            if (!WybierzWartosciPolWlasnychDoZmiany(uchwyt, polaWlasne, out var wartosciDoZmiany))
            {
                return;
            }

            ZmienPolaWlasneZbiorczo(uchwyt, wybraneDokumenty, wartosciDoZmiany);
        }

        private static List<Dokument> PobierzWybraneDokumenty(
            IReadOnlyCollection<int> identyfikatoryWybranychDokumentow,
            IUchwyt uchwyt)
        {
            IDokumenty dokumenty = uchwyt.PodajObiektTypu<IDokumenty>();

            return dokumenty
                .Dane
                .Wszystkie()
                .Where(d => identyfikatoryWybranychDokumentow.Contains(d.Id))
                .ToList();
        }

        private static bool CzyDokumentyRoznychTypow(
            List<Dokument> dokumenty)
        {
            return dokumenty
                .Select(d => d.GetType())
                .Distinct()
                .Count() > 1;
        }

        private static void WyswietlBladORoznychTypachDokumentow(
            IUchwyt uchwyt)
        {
            Okna.PokazOknoZBledem(
                uchwyt,
                "Operację można wykonać tylko na dokumentach tego samego typu.");
        }

        private static List<IZaawansowanePoleWlasne> PobierzPolaWlasne(
            IUchwyt uchwyt,
            List<Dokument> dokumenty)
        {
            IZaawansowanePolaWlasne polaWlasne = uchwyt.PodajObiektTypu<IZaawansowanePolaWlasne>();

            return dokumenty
                .Select(d => d.KonfiguracjaRzeczywista)
                .Distinct(EntityComparerByID<Konfiguracja, Guid>.Instance)
                .SelectMany(k => k.PodajPolaWlasneDostepneDlaDokumentu(polaWlasne))
                .Distinct()
                .ToList();
        }

        private static void WyswietlBladOBrakuPolWlasnych(
            IUchwyt uchwyt,
            Type typDokumentu)
        {
            Okna.PokazOknoZBledem(
                uchwyt,
                $"Dla dokumentów typu {typDokumentu.Name} nie zostały zdefiniowane żadne pola własne.");
        }

        private static bool WybierzWartosciPolWlasnychDoZmiany(
            IUchwyt uchwyt,
            IEnumerable<IZaawansowanePoleWlasne> polaWlasne,
            out List<(IZaawansowanePoleWlasne poleWlasne, object nowaWartosc)> wartosciDoZmiany)
        {
            wartosciDoZmiany = new List<(IZaawansowanePoleWlasne poleWlasne, object nowaWartosc)>();

            IOknoParametrowOperacji oknoParametrow = uchwyt.PodajObiektTypu<IOknoParametrowOperacji>();
            oknoParametrow.Tytul = "Wprowadź wartości pól własnych do zmiany";

            ParametrOperacji[] parametry = UtworzParametry(polaWlasne).ToArray();

            oknoParametrow.Parametry.Dodaj(parametry);

            if (!oknoParametrow.Pokaz())
            {
                return false;
            }

            foreach (ParametrOperacji parametr in parametry.Where(p => p.CzyParametrWybrany))
            {
                IZaawansowanePoleWlasne poleWlasne = polaWlasne.First(p => p.Nazwa == parametr.Nazwa);
                wartosciDoZmiany.Add((poleWlasne, parametr.PodajWartosc()));
            }

            return wartosciDoZmiany.Count > 0;
        }

        private static ParametrOperacji[] UtworzParametry(
            IEnumerable<IZaawansowanePoleWlasne> polaWlasne)
        {
            return polaWlasne
                .OrderBy(p => p.Grupa?.PozycjaWyswietlania ?? -1)
                .ThenBy(p => p.PozycjaWyswietlania)
                .Select(UtworzParametrOperacjiDlaPola)
                .Where(p => p != null)
                .ToArray();
        }

        private static ParametrOperacji UtworzParametrOperacjiDlaPola(
            IZaawansowanePoleWlasne poleWlasne)
        {
            if (poleWlasne.JestReferencjaDoSlownika)
            {
                return UtworzParametrListyWyboru(poleWlasne);
            }

            switch (poleWlasne.Typ)
            {
                case TypSkalarnyZaawansowanegoPolaWlasnego.Tekst:
                    return new TekstowyParametrOperacji(poleWlasne.Nazwa)
                    {
                        ZezwalajNaWyborParametru = true,
                        ZezwalajNaBrakWartosci = !poleWlasne.Wymagane,
                        Wartosc = (string)poleWlasne.WartoscDomyslna,
                        MaksymalnaLiczbaZnakow = 256,
                        GrupaParametrow = poleWlasne.Grupa?.Nazwa,
                    };

                case TypSkalarnyZaawansowanegoPolaWlasnego.DlugiTekst:
                    return new TekstowyParametrOperacji(poleWlasne.Nazwa)
                    {
                        ZezwalajNaWyborParametru = true,
                        ZezwalajNaBrakWartosci = !poleWlasne.Wymagane,
                        Wartosc = (string)poleWlasne.WartoscDomyslna,
                        MinimalnaLiczbaWierszy = poleWlasne.MinWidoczneLinie,
                        MaksymalnaLiczbaWierszy = poleWlasne.MaxWidoczneLinie,
                        MaksymalnaLiczbaZnakow = 4000,
                        GrupaParametrow = poleWlasne.Grupa?.Nazwa,
                    };

                case TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita:
                    return new CalkowitoliczbowyParametrOperacji(poleWlasne.Nazwa)
                    {
                        ZezwalajNaWyborParametru = true,
                        ZezwalajNaBrakWartosci = !poleWlasne.Wymagane,
                        Wartosc = (int?)poleWlasne.WartoscDomyslna,
                        GrupaParametrow = poleWlasne.Grupa?.Nazwa,
                    };

                case TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaRzeczywista:
                    return new KwotowyParametrOperacji(poleWlasne.Nazwa)
                    {
                        ZezwalajNaWyborParametru = true,
                        ZezwalajNaBrakWartosci = !poleWlasne.Wymagane,
                        Wartosc = (decimal?)poleWlasne.WartoscDomyslna,
                        Precyzja = poleWlasne.Precyzja,
                        GrupaParametrow = poleWlasne.Grupa?.Nazwa,
                    };

                case TypSkalarnyZaawansowanegoPolaWlasnego.WartoscLogiczna:
                    return new LogicznyParametrOperacji(poleWlasne.Nazwa)
                    {
                        ZezwalajNaWyborParametru = true,
                        ZezwalajNaBrakWartosci = !poleWlasne.Wymagane,
                        Wartosc = (bool?)poleWlasne.WartoscDomyslna,
                        GrupaParametrow = poleWlasne.Grupa?.Nazwa,
                    };

                case TypSkalarnyZaawansowanegoPolaWlasnego.Data:
                    return new DatowyParametrOperacji(poleWlasne.Nazwa)
                    {
                        ZezwalajNaWyborParametru = true,
                        ZezwalajNaBrakWartosci = !poleWlasne.Wymagane,
                        Wartosc = (DateTime?)poleWlasne.WartoscDomyslna,
                        GrupaParametrow = poleWlasne.Grupa?.Nazwa,
                    };

                default:
                    return null;
            }
        }

        private static ParametrOperacji UtworzParametrListyWyboru(
            IZaawansowanePoleWlasne poleWlasne)
        {
            ISlownikoweZrodloDanych slownik = poleWlasne.PobierzDefinicjeSlownika();

            if (slownik.TypEncji != null)
            {
                return (ParametrOperacji)_utworzParametrListyWyboruEncjiGenericMI
                    .MakeGenericMethod(slownik.TypEncji, slownik.TypKlucza)
                    .Invoke(null, new object[] { poleWlasne, slownik });
            }
            else
            {
                return (ParametrOperacji)_utworzParametrWyboruGenericMI
                    .MakeGenericMethod(slownik.TypKlucza)
                    .Invoke(null, new object[] { poleWlasne, slownik });
            }
        }

        private static ParametrOperacji UtworzParametrListyWyboruGeneric<TWartosc>(
            IZaawansowanePoleWlasne poleWlasne,
            ISlownikoweZrodloDanych<TWartosc> zrodloDanych) 
            where TWartosc  : struct, IEquatable<TWartosc>
        {
            Dictionary<TWartosc, ElementSlownikowegoZrodlaDanych<TWartosc>> wartosci = new Dictionary<TWartosc, ElementSlownikowegoZrodlaDanych<TWartosc>>();

            foreach (var element in zrodloDanych.UtworzZapytanieLinq())
            {
                wartosci[element.Klucz] = element;
            }

            return new ParametrWyboruWartosci<TWartosc?>(
                poleWlasne.Nazwa,
                wartosci.OrderBy(w => w.Value.Wartosc).Select(v => (TWartosc?)v.Key))
            {
                ZezwalajNaWyborParametru = true,
                ZezwalajNaBrakWartosci = !poleWlasne.Wymagane,
                Wartosc = (TWartosc?)poleWlasne.WartoscDomyslna,
                FunkcjaOpisuWartosci = v => v == null ? "(brak)" : wartosci[v.Value].Wartosc,
                GrupaParametrow = poleWlasne.Grupa?.Nazwa,
            };
        }

        private static ParametrOperacji UtworzParametrListyWyboruEncjiGeneric<TEncja, TId>(
            IZaawansowanePoleWlasne poleWlasne,
            ISlownikoweZrodloDanych<TId, TEncja> zrodloDanych)
            where TEncja : EntityDataObjectBase, IIdentifiable<TId>
            where TId : IEquatable<TId>
        {
            List<TEncja> wartosci = zrodloDanych.UtworzZapytanieLinqTypowane().ToList();

            return new ParametrWyboruWartosci<TEncja>(
                poleWlasne.Nazwa,
                wartosci)
            {
                ZezwalajNaWyborParametru = true,
                ZezwalajNaBrakWartosci = !poleWlasne.Wymagane,
                Wartosc = poleWlasne.WartoscDomyslna is TId id ? wartosci.FirstOrDefault(w => w.Id.Equals(id)) : null,
                GrupaParametrow = poleWlasne.Grupa?.Nazwa,
            };
        }

        private static void ZmienPolaWlasneZbiorczo(
            IUchwyt uchwyt,
            List<Dokument> wybraneDokumenty,
            List<(IZaawansowanePoleWlasne poleWlasne, object nowaWartosc)> wartosciDoZmiany)
        {
            IDokumenty dokumenty = uchwyt.PodajObiektTypu<IDokumenty>();

            IOknoOperacjiZbiorczej oknoOperacjiZbiorczej = uchwyt.PodajObiektTypu<IOknoOperacjiZbiorczej>();
            oknoOperacjiZbiorczej.Tytul = "Zmiana pól własnych";

            oknoOperacjiZbiorczej.Pokaz(
                wybraneDokumenty,
                d =>
                {
                    using (IDokument dokumentBO = (IDokument)dokumenty.Znajdz(d))
                    {
                        UstawWartosciPolWlasnych(uchwyt, dokumentBO, wartosciDoZmiany);

                        if (dokumentBO.Zapisz())
                        {
                            return WynikOperacji.ZakonczonaPowodzeniem();
                        }
                        else
                        {
                            return dokumentBO.PobierzKomunikatyBledow().ToWynikOperacji();
                        }
                    }
                });
        }

        private static void UstawWartosciPolWlasnych(
            IUchwyt uchwyt,
            IDokument dokument,
            List<(IZaawansowanePoleWlasne poleWlasne, object nowaWartosc)> wartosciDoZmiany)
        {
            IPolaWlasneAdv2AccessorFactory pwAccessorFactory = uchwyt.PodajObiektTypu<IPolaWlasneAdv2AccessorFactory>();
            IPolaWlasneAdv2Accessor accessor = pwAccessorFactory.Utworz(dokument.Dokument);

            foreach (var wartoscDoZmiany in wartosciDoZmiany)
            {
                UstawWartoscPolaWlasnego(accessor, wartoscDoZmiany.poleWlasne, wartoscDoZmiany.nowaWartosc);
            }
        }

        private static void UstawWartoscPolaWlasnego(
            IPolaWlasneAdv2Accessor accessor,
            IZaawansowanePoleWlasne poleWlasne,
            object nowaWartosc)
        {
            if (poleWlasne.JestReferencjaDoSlownika)
            {
                UstawWartoscPolaWlasnegoSlownikowego(accessor, poleWlasne, nowaWartosc);
            }
            else
            {
                UstawWartoscPolaWlasnegoSkalarnego(accessor, poleWlasne, nowaWartosc);
            }
        }

        private static void UstawWartoscPolaWlasnegoSlownikowego(
            IPolaWlasneAdv2Accessor accessor,
            IZaawansowanePoleWlasne poleWlasne,
            object nowaWartosc)
        {
            ISlownikoweZrodloDanych slownik = poleWlasne.PobierzDefinicjeSlownika();

            if (slownik.Rodzaj == RodzajSlownikowegoZrodlaDanych.SlownikWlasny)
            {
                accessor.UstawWartoscTypuSlownikWlasny(poleWlasne.Nazwa, ((PozycjaSlownikaWlasnego)nowaWartosc)?.Id);
            }
            else if (slownik.Rodzaj == RodzajSlownikowegoZrodlaDanych.SlownikSystemowy)
            {
                if (slownik.TypEncji == typeof(Magazyn))
                {
                    accessor.UstawWartoscTypuSlownikSystemowyMagazynow(poleWlasne.Nazwa, ((Magazyn)nowaWartosc)?.Id);
                }
                else if (slownik.TypEncji == typeof(RachunekBankowy))
                {
                    accessor.UstawWartoscTypuSlownikSystemowyRachunkowBankowych(poleWlasne.Nazwa, ((RachunekBankowy)nowaWartosc)?.Id);
                }
                else if (slownik.TypEncji == typeof(Waluta))
                {
                    accessor.UstawWartoscTypuSlownikSystemowyWalut(poleWlasne.Nazwa, ((Waluta)nowaWartosc)?.Id);
                }
            }
            else if (slownik.Rodzaj == RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql)
            {
                if (slownik.TypKlucza == typeof(int))
                {
                    accessor.UstawWartoscTypuSlownikWlasnySqlByInt(poleWlasne.Nazwa, (int?)nowaWartosc);
                }
                else if (slownik.TypKlucza == typeof(Guid))
                {
                    accessor.UstawWartoscTypuSlownikWlasnySqlByGuid(poleWlasne.Nazwa, (Guid?)nowaWartosc);
                }
            }
        }

        private static void UstawWartoscPolaWlasnegoSkalarnego(
           IPolaWlasneAdv2Accessor accessor,
           IZaawansowanePoleWlasne poleWlasne,
           object nowaWartosc)
        {
            switch (poleWlasne.Typ)
            {
                case TypSkalarnyZaawansowanegoPolaWlasnego.Tekst:
                case TypSkalarnyZaawansowanegoPolaWlasnego.DlugiTekst:
                    accessor.UstawWartoscTypuTekst(poleWlasne.Nazwa, (string)nowaWartosc);
                    break;
                case TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita:
                    accessor.UstawWartoscTypuLiczbaCalkowita(poleWlasne.Nazwa, (int?)nowaWartosc);
                    break;
                case TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaRzeczywista:
                    accessor.UstawWartoscTypuLiczbaRzeczywista(poleWlasne.Nazwa, (decimal?)nowaWartosc);
                    break;
                case TypSkalarnyZaawansowanegoPolaWlasnego.WartoscLogiczna:
                    accessor.UstawWartoscTypuLogicznego(poleWlasne.Nazwa, (bool?)nowaWartosc);
                    break;
                case TypSkalarnyZaawansowanegoPolaWlasnego.Data:
                    accessor.UstawWartoscTypuData(poleWlasne.Nazwa, (DateTime?)nowaWartosc);
                    break;

            }
        }
    }
}
