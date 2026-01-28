using InsERT.Moria.Archiwa;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.Klienci;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;

namespace DaneArchiwalne
{
    public class DaneArchiwalneInsertGtMulti : DaneArchiwalneInsertGtBazowa, IFunkcjaPobieraniaDanychArchiwalnych
    {
        private const string _nazwaWezlaKonfiguracje = "Konfiguracje";

        #region Konstruktor

        public DaneArchiwalneInsertGtMulti()
        {
            _dostawca = new Producent();
        }

        #endregion

        #region IFunkcjaPobieraniaDanychArchiwalnych

        public XElement DomyslnaKonfiguracja
        {
            get
            {
                return UtworzKonfiguracje("Serwer", "BazaDanych", "Login", "Haslo");
            }
        }

        public string DefinicjaWidokuKonfiguracji
        {
            get
            {
                return null;
            }
        }

        public bool CzyTypDanychObslugiwany(TypDanych typDanych)
        {
            return _obslugiwaneTypy.Contains(typDanych);
        }

        private IQueryable _ostatnioPobraneDane = null;
        private IDictionary<string, Magazyn> _magazyny = null;
        private IDictionary<string, Kategoria> _kategorie = null;
        private int _konfiguracjaHashCode = 0;
        public object PobierzDane(XElement konfiguracja, TypDanych typDanych)
        {
            int hashCode = konfiguracja.ToString().GetHashCode();
            if (_konfiguracjaHashCode != hashCode)
            {
                _konfiguracjaHashCode = hashCode;
                _ostatnioPobraneDane = null;
                _magazyny = null;
                _kategorie = null;
            }

            if (_magazyny == null)
            {
                var magazyny = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzMagazyny);
                string glowny = magazyny.Where(m => m.Glowny).First().Symbol;
                var wynik = magazyny.Distinct(new MagazynComparer());
                wynik.Where(m => m.Symbol == glowny).Single().Glowny = true;
                _magazyny = new Dictionary<string, Magazyn>();
                foreach (var mag in wynik)
                    _magazyny.Add(mag.Symbol, mag);
            }
            if (_kategorie == null)
            {
                var kategorie = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzKategorie);
                var wynik = kategorie.Distinct(new KategoriaComparer());
                _kategorie = new Dictionary<string, Kategoria>();
                foreach (var kat in wynik)
                    _kategorie.Add(kat.Nazwa, kat);
            }

            switch (typDanych)
            {
                case TypDanych.FakturySprzedazy:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzDokumentySprzedazyWszystkie); break;
                case TypDanych.FakturyZakupu:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzDokumentyZakupu); break;
                case TypDanych.KorektySprzedazy:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzKorektySprzedazy); break;
                case TypDanych.SprzedazDetaliczna:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzParagony); break;
                case TypDanych.KorektyZakupu:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzKorektyZakupu); break;
                case TypDanych.PrzyjeciaMagazynowe:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzPrzyjeciaMagazynowe); break;
                case TypDanych.WydaniaMagazynowe:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzWydaniaMagazynowe); break;
                case TypDanych.ZamowieniaDoDostawcow:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzZamowieniaDoDostawcow); break;
                case TypDanych.ZamowieniaOdKlientow:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzZamowieniaOdKlientowNew); break;
                case TypDanych.ZwrotyDetaliczne:
                    _ostatnioPobraneDane = null;
                    _ostatnioPobraneDane = WykonajDlaWszystkichKonfiguracji(konfiguracja, PobierzZwrotyDoParagonu); break;
                case TypDanych.Magazyny: return _magazyny.Values.AsQueryable();
                case TypDanych.Kategorie: return _kategorie.Values.AsQueryable();
            }
            return _ostatnioPobraneDane;
        }

        public string PobierzHTMLPodgladu(XElement konfiguracja, TypPodgladu typPodgladu, int idDokumentu)
        {
            if (_ostatnioPobraneDane == null)
                throw new InvalidOperationException("Jeszcze nie pobierano danych.");

            IQueryable<IZrodlo> dane = _ostatnioPobraneDane.Cast<IZrodlo>();
            if (dane == null)
                return PodajHTMLPodgladuInformacjiOBledzie("dane == null");

            IZrodlo zrodlo = dane.Where(d => d.Id == idDokumentu).Single();
            TypPodgladuEx typPodgladuEx = _typyPodgladu
                .Where(t => t.Nazwa == typPodgladu.Nazwa && t.KonfiguracjaHashCode == zrodlo.Konfiguracja.ToString().GetHashCode())
                .FirstOrDefault();

            if (typPodgladuEx == null)
                return PodajHTMLPodgladuInformacjiOBledzie(
                    "typPodgladuEx == null<br>" +
                    "Nazwa: " + typPodgladu.Nazwa + "<br>" +
                    "Konfiguracja: <pre>" + typPodgladu.Nazwa + "</pre><br>");

            IKontekstBazy kontekst = WczytajKonfiguracje(typPodgladuEx.Konfiguracja);
            TypPodgladu typ = new TypPodgladu() { Id = typPodgladuEx.OryginalnyId, Nazwa = typPodgladuEx.Nazwa, TypDanych = typPodgladuEx.TypDanych };

            return PobierzHTMLPodgladuObiektu(kontekst, typ, zrodlo.OryginalnyId);
        }

        private List<TypPodgladuEx> _typyPodgladu = null;
        private List<TypPodgladu> _typyPodgladuDlaUi = null;
        public IEnumerable<TypPodgladu> PodajTypyPodgladu(XElement konfiguracja, TypDanych typDanych)
        {
            int id = 1;
            _typyPodgladu = new List<TypPodgladuEx>();
            _typyPodgladuDlaUi = new List<TypPodgladu>();
            foreach (XElement konf in konfiguracja.Elements(_nazwaWezlaKonfiguracja))
            {
                using (SqlConnection conn = UtworzPolaczenie(konf))
                using (SqlCommand cmd = new SqlCommand(_sqlMozliwePodglady, conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("id", PodajTypPodgladu(typDanych));
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            _typyPodgladu.Add(new TypPodgladuEx()
                            {
                                Id = id++,
                                OryginalnyId = reader.GetInt32(0),
                                Konfiguracja = konf,
                                Nazwa = reader.GetString(1),
                                TypDanych = typDanych
                            });
                    }
                }
            }
            int i = 1;
            foreach (var o in _typyPodgladu.GroupBy(t => t.Nazwa))
            {
                var b = o.First();
                _typyPodgladuDlaUi.Add(new TypPodgladu()
                {
                    Id = i++,
                    Nazwa = b.Nazwa,
                    TypDanych = b.TypDanych,
                });
            }
            return _typyPodgladuDlaUi;
        }

        public IEnumerable<BladKonfiguracji> SprawdzKonfiguracje(XElement konfig)
        {
            List<BladKonfiguracji> bledy = new List<BladKonfiguracji>();
            int i = 0;
            foreach (var konfiguracja in konfig.Elements(_nazwaWezlaKonfiguracja))
            {
                i++;
                var elem = konfiguracja.Element(_nazwaWezlaAutentykacja);
                if (elem == null || !(elem.Value == _autentykacjaMixed || elem.Value == _autentykacjaWindows))
                {
                    bledy.Add(new BladKonfiguracji
                    {
                        NazwaElementu = _nazwaWezlaAutentykacja,
                        TrescBledu = string.Format("Konfiguracja {0}: Dozwolone wartości to {1} lub {2}",
                            i, _autentykacjaMixed, _autentykacjaWindows)
                    });
                }

                if (bledy.Count() == 0)
                {
                    try
                    {
                        if (!BazaInsertGt(konfiguracja))
                            bledy.Add(new BladKonfiguracji { NazwaElementu = _nazwaWezlaBaza, TrescBledu = string.Format("Konfiguracja {0}: Baza danych nie jest bazą programu InsERT GT.", i) });
                    }
                    catch
                    {
                        bledy.Add(new BladKonfiguracji { TrescBledu = string.Format("Konfiguracja {0}: Nie udało się nawiązać połączenia z bazą danych.", i) });
                    }
                }
            }
            return bledy;
        }

        #endregion

        #region IFunkcja

        public Guid Identyfikator
        {
            get { return Guid.Parse("7C2B10ED-AD08-4CC5-826C-D6A57CFD09DE"); }
        }

        public string Nazwa
        {
            get { return "Dane archiwalne z wielu podmiotów InsERT GT"; }
        }

        public string Opis
        {
            get { return "Dane archiwalne z wielu podmiotów InsERT GT"; }
        }

        private IDostawcaPluginow _dostawca;
        public IDostawcaPluginow Dostawca
        {
            get { return _dostawca; }
        }

        #endregion

        #region Metody pomocnicze

        private int _ostatniId = 0;
        public IQueryable<TDok> WykonajDlaWszystkichKonfiguracji<TDok>(XElement konfiguracja, Func<XElement, IQueryable<TDok>> funkcja)
            where TDok : class
        {
            _ostatniId = 0;
            IEnumerable<TDok> wynikZagregowany = null;
            foreach (var konf in konfiguracja.Elements(_nazwaWezlaKonfiguracja))
            {
                IEnumerable<TDok> wynik = funkcja(konf).Cast<TDok>();
                if (wynikZagregowany == null)
                    wynikZagregowany = wynik;
                else
                    wynikZagregowany = wynikZagregowany.Union(wynik);
            }
            return wynikZagregowany.AsQueryable();
        }

        private IQueryable<PrzyjecieMagazynowe> PobierzPrzyjeciaMagazynowe(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyPZ, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<PrzyjecieMagazynowe> lista = new List<PrzyjecieMagazynowe>();
                    while (reader.Read())
                    {
                        lista.Add(new PrzyjecieMagazynoweEx
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            NumerOryginalu = reader.GetString(6),
                            Kontrahent = !reader.IsDBNull(7) ? reader.GetString(7) : null,
                            Wartosc = reader.GetDecimal(8),
                            Koszt = reader.GetDecimal(9),
                            NumerDokumentuPowiazanego = reader.GetString(10),
                            Numer = reader.GetInt32(11),
                            Symbol = reader.GetString(12),
                            Magazyn = _magazyny == null ? reader.GetInt32(13) : _magazyny[reader.GetString(14)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(15) : _kategorie[reader.GetString(16)].Id,
                            Kategoria  = reader.GetString(16),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private IQueryable<WydanieMagazynowe> PobierzWydaniaMagazynowe(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyWZ, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<WydanieMagazynowe> lista = new List<WydanieMagazynowe>();
                    while (reader.Read())
                    {
                        lista.Add(new WydanieMagazynoweEx()
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            Kontrahent = !reader.IsDBNull(6) ? reader.GetString(6) : null,
                            Wartosc = reader.GetDecimal(7),
                            Koszt = reader.GetDecimal(8),
                            NumerDokumentuPowiazanego = reader.GetString(9),
                            Numer = reader.GetInt32(10),
                            Symbol = reader.GetString(11),
                            Magazyn = _magazyny == null ? reader.GetInt32(12) : _magazyny[reader.GetString(13)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(14) : _kategorie[reader.GetString(15)].Id,
                            Kategoria = reader.GetString(15),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private IQueryable<ZamowienieDoDostawcy> PobierzZamowieniaDoDostawcow(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyZD, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<ZamowienieDoDostawcy> lista = new List<ZamowienieDoDostawcy>();
                    while (reader.Read())
                    {
                        lista.Add(new ZamowienieDoDostawcyEx
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            StatusRealizacji = (StatusRealizacji)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            Kontrahent = !reader.IsDBNull(6) ? reader.GetString(6) : null,
                            Wartosc = reader.GetDecimal(7),
                            WartoscZrealizowana = !reader.IsDBNull(8) ? reader.GetDecimal(8) : (decimal?)null,
                            TerminRealizacji = !reader.IsDBNull(9) ? reader.GetDateTime(9) : (DateTime?)null,
                            DataZrealizowania = !reader.IsDBNull(10) ? reader.GetDateTime(10) : (DateTime?)null,
                            TransakcjaVatRaw = reader.GetInt32(11),
                            Numer = reader.GetInt32(12),
                            Symbol = reader.GetString(13),
                            Magazyn = _magazyny == null ? reader.GetInt32(14) : _magazyny[reader.GetString(15)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(16) : _kategorie[reader.GetString(17)].Id,
                            Kategoria = reader.GetString(17),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }


        private IQueryable<ZamowienieOdKlienta> PobierzZamowieniaOdKlientowNew(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyZK, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<ZamowienieOdKlienta> lista = new List<ZamowienieOdKlienta>();
                    while (reader.Read())
                    {
                        lista.Add(new ZamowienieOdKlientaEx
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            StatusRealizacji = (StatusRealizacji)reader.GetInt32(1),
                            StatusRezerwacji = (StatusRezerwacji)reader.GetInt32(2),
                            DataWystawienia = reader.GetDateTime(3),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(4),
                            PodtypRaw = reader.GetInt32(5),
                            NumerPelny = reader.GetString(6),
                            NumerOryginalu = reader.GetString(7),
                            Kontrahent = !reader.IsDBNull(8) ? reader.GetString(8) : null,
                            Wartosc = reader.GetDecimal(9),
                            WartoscZrealizowana = !reader.IsDBNull(10) ? reader.GetDecimal(10) : (decimal?)null,
                            TerminRealizacji = !reader.IsDBNull(11) ? reader.GetDateTime(11) : (DateTime?)null,
                            DataZrealizowania = !reader.IsDBNull(12) ? reader.GetDateTime(12) : (DateTime?)null,
                            TransakcjaVatRaw = reader.GetInt32(13),
                            CzyPrzetwarzaneNaZD = reader.GetBoolean(14),
                            Numer = reader.GetInt32(15),
                            Symbol = reader.GetString(16),
                            Magazyn = _magazyny == null ? reader.GetInt32(17) : _magazyny[reader.GetString(18)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(19) : _kategorie[reader.GetString(20)].Id,
                            Kategoria = reader.GetString(20),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private IQueryable<KorektaDokumentuZakupu> PobierzKorektyZakupu(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyKFZ, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<KorektaDokumentuZakupu> lista = new List<KorektaDokumentuZakupu>();
                    while (reader.Read())
                    {
                        lista.Add(new KorektaDokumentuZakupuEx
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataOtrzymania = !reader.IsDBNull(2) ? reader.GetDateTime(2) : (DateTime?)null,
                            DataWystawienia = reader.GetDateTime(3),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(4),
                            PodtypRaw = reader.GetInt32(5),
                            NumerPelny = reader.GetString(6),
                            NumerOryginalu = reader.GetString(7),
                            Kontrahent = !reader.IsDBNull(8) ? reader.GetString(8) : null,
                            Wartosc = reader.GetDecimal(9),
                            TransakcjaVatRaw = reader.GetInt32(10),
                            Numer = reader.GetInt32(11),
                            Symbol = reader.GetString(12),
                            Magazyn = _magazyny == null ? reader.GetInt32(13) : _magazyny[reader.GetString(14)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(15) : _kategorie[reader.GetString(16)].Id,
                            Kategoria = reader.GetString(16),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private IQueryable<ZwrotDoParagonu> PobierzZwrotyDoParagonu(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyZW, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<ZwrotDoParagonu> lista = new List<ZwrotDoParagonu>();
                    while (reader.Read())
                    {
                        lista.Add(new ZwrotDoParagonuEx
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            Wartosc = reader.GetDecimal(6),
                            Wystawil = reader.GetString(7),
                            TransakcjaVatRaw = reader.GetInt32(8),
                            Numer = reader.GetInt32(9),
                            Symbol = reader.GetString(10),
                            Magazyn = _magazyny == null ? reader.GetInt32(11) : _magazyny[reader.GetString(12)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(13) : _kategorie[reader.GetString(14)].Id,
                            Kategoria = reader.GetString(14),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private IQueryable<Paragon> PobierzParagony(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyPA, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<Paragon> lista = new List<Paragon>();
                    while (reader.Read())
                    {
                        lista.Add(new ParagonEx
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            StatusFiskalny = (StatusFiskalny)reader.GetInt32(2),
                            DataWystawienia = reader.GetDateTime(3),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(4),
                            PodtypRaw = reader.GetInt32(5),
                            NumerPelny = reader.GetString(6),
                            Wartosc = reader.GetDecimal(7),
                            Wystawil = reader.GetString(8),
                            TransakcjaVatRaw = reader.GetInt32(9),
                            Numer = reader.GetInt32(10),
                            Symbol = reader.GetString(11),
                            Magazyn = _magazyny == null ? reader.GetInt32(12) : _magazyny[reader.GetString(13)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(14) : _kategorie[reader.GetString(15)].Id,
                            Kategoria = reader.GetString(15),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private IQueryable<KorektaDokumentuSprzedazy> PobierzKorektySprzedazy(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyKFS, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<KorektaDokumentuSprzedazy> lista = new List<KorektaDokumentuSprzedazy>();
                    while (reader.Read())
                    {
                        lista.Add(new KorektaDokumentuSprzedazyEx
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataWystawienia = reader.GetDateTime(2),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(3),
                            PodtypRaw = reader.GetInt32(4),
                            NumerPelny = reader.GetString(5),
                            NumerKorygowanego = reader.GetString(6),
                            Kontrahent = !reader.IsDBNull(7) ? reader.GetString(7) : null,
                            Wartosc = reader.GetDecimal(8),
                            TransakcjaVatRaw = reader.GetInt32(9),
                            Numer = reader.GetInt32(10),
                            Symbol = reader.GetString(11),
                            Magazyn = _magazyny == null ? reader.GetInt32(12) : _magazyny[reader.GetString(13)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(14) : _kategorie[reader.GetString(15)].Id,
                            Kategoria = reader.GetString(15),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private IQueryable<DokumentZakupu> PobierzDokumentyZakupu(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyFZ, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<DokumentZakupu> lista = new List<DokumentZakupu>();
                    while (reader.Read())
                    {
                        lista.Add(new DokumentZakupuEx
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            Status = (StatusDokumentu)reader.GetInt32(1),
                            DataOtrzymania = !reader.IsDBNull(2) ? reader.GetDateTime(2) : (DateTime?)null,
                            DataWystawienia = reader.GetDateTime(3),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(4),
                            PodtypRaw = reader.GetInt32(5),
                            NumerPelny = reader.GetString(6),
                            NumerOryginalu = reader.GetString(7),
                            Kontrahent = !reader.IsDBNull(8) ? reader.GetString(8) : null,
                            Wartosc = reader.GetDecimal(9),
                            TransakcjaVatRaw = reader.GetInt32(10),
                            Numer = reader.GetInt32(11),
                            Symbol = reader.GetString(12),
                            Magazyn = _magazyny == null ? reader.GetInt32(13) : _magazyny[reader.GetString(14)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(15) : _kategorie[reader.GetString(16)].Id,
                            Kategoria = reader.GetString(16),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private IQueryable<DokumentSprzedazy> PobierzDokumentySprzedazyWszystkie(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlDokumentyFS, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<DokumentSprzedazy> lista = new List<DokumentSprzedazy>();
                    while (reader.Read())
                    {
                        lista.Add(new DokumentSprzedazyEx()
                        {
                            Id = ++_ostatniId,
                            Konfiguracja = konfiguracja,
                            OryginalnyId = reader.GetInt32(0),
                            DataWystawienia = reader.GetDateTime(1),
                            NumerPelny = reader.GetString(2),
                            Kontrahent = !reader.IsDBNull(3) ? reader.GetString(3) : null,
                            Wartosc = reader.GetDecimal(4),
                            Status = (StatusDokumentu)reader.GetInt32(5),
                            StatusFiskalny = (StatusFiskalny)reader.GetInt32(6),
                            Typ = (InsERT.Moria.Archiwa.TypDokumentu)reader.GetInt32(7),
                            PodtypRaw = reader.GetInt32(8),
                            TransakcjaVatRaw = reader.GetInt32(9),
                            FakturaUproszczona = reader.GetBoolean(10),
                            Numer = reader.GetInt32(11),
                            Symbol = reader.GetString(12),
                            Magazyn = _magazyny == null ? reader.GetInt32(13) : _magazyny[reader.GetString(14)].Id,
                            KategoriaId = _kategorie == null ? reader.GetInt32(15) : _kategorie[reader.GetString(16)].Id,
                            Kategoria = reader.GetString(16),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        class MagazynComparer : Magazyn, IEqualityComparer<Magazyn>
        {
            public bool Equals(Magazyn x, Magazyn y)
            {
                return x.Symbol == y.Symbol;
            }

            public int GetHashCode(Magazyn obj)
            {
                return obj.Symbol.GetHashCode();
            }
        }
        class KategoriaComparer : Kategoria, IEqualityComparer<Kategoria>
        {
            public bool Equals(Kategoria x, Kategoria y)
            {
                return x.Nazwa == y.Nazwa;
            }

            public int GetHashCode(Kategoria obj)
            {
                return obj.Nazwa.GetHashCode();
            }
        }
        private IQueryable<Magazyn> PobierzMagazyny(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlMagazyny, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<Magazyn> lista = new List<Magazyn>();
                    while (reader.Read())
                    {
                        lista.Add(new Magazyn()
                        {
                            Id = ++_ostatniId,
                            Symbol = reader.GetString(1),
                            Nazwa = reader.GetString(2),
                            Opis = reader.GetString(3),
                            Glowny = reader.GetBoolean(4),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }
        private IQueryable<Kategoria> PobierzKategorie(XElement konfiguracja)
        {
            using (SqlConnection conn = UtworzPolaczenie(konfiguracja))
            using (SqlCommand cmd = new SqlCommand(_sqlKategorie, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    List<Kategoria> lista = new List<Kategoria>();
                    while (reader.Read())
                    {
                        lista.Add(new Kategoria()
                        {
                            Id = ++_ostatniId,
                            Nazwa = reader.GetString(1),
                            Podtytul = reader.GetString(2),
                        });
                    }
                    return lista.AsQueryable();
                }
            }
        }

        private string PodajHTMLPodgladuInformacjiOBledzie(string informacja, string tytul = "Nie udało się wygenerować podglądu z powodu błędu:")
        {
            return Resources.podglad.Replace(_podgladPlaceholder, "<p style=\"font-weight: bold;\">" + tytul + ":</p><div style=\"width: 500; font-family: courier new\">" + informacja + "</div>");
        }

        private static IEnumerable<IKontekstBazy> WczytajMultiKonfiguracje(XElement wezelKonf)
        {
            List<IKontekstBazy> lista = new List<IKontekstBazy>();
            try
            {
                foreach (var elemKonfiguracja in wezelKonf.Elements(_nazwaWezlaKonfiguracja))
                {
                    lista.Add(WczytajKonfiguracje(elemKonfiguracja));
                }
            }
            catch { }
            return lista;
        }

        #endregion

        #region IFunkcjaPobieraniaDanychArchiwalnychGT

        public XElement UtworzKonfiguracje(string serwer, string baza)
        {
            return UtworzKonfiguracje(serwer, baza, null, null);
        }

        public XElement UtworzKonfiguracje(string serwer, string baza, string login, string haslo)
        {
            return new XElement(_nazwaWezlaKonfiguracje,
                        new XElement(_nazwaWezlaKonfiguracja,
                            new XElement(_nazwaWezlaSerwer, serwer),
                            new XElement(_nazwaWezlaAutentykacja, !string.IsNullOrEmpty(login) && haslo != null ? _autentykacjaMixed : _autentykacjaWindows),
                            new XElement(_nazwaWezlaLogin, !string.IsNullOrEmpty(login) && haslo != null ? string.Format("{0}/{1}", login, haslo) : ""),
                            new XElement(_nazwaWezlaBaza, baza)));
        }

        #endregion

    }


}
