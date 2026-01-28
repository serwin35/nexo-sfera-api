using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Uprawnienia;
using InsERT.Moria.Wspolne;
using InsERT.Mox.Formatting;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace ProfilowanieDanych
{
    public class ProfilowanieWgOpiekunaKlientaPlugin :
        IWarunekFiltrujacyDane<Dokument>,
        IWarunekFiltrujacyDane<DokumentOE>,
        IWarunekFiltrujacyDane<DokumentElektroniczny>,
        IWarunekFiltrujacyDane<DokumentKKD>,
        IWarunekFiltrujacyDane<OperacjaKasowa>,
        IWarunekFiltrujacyDane<OperacjaBankowa>,
        IWarunekFiltrujacyDane<DyspozycjaBankowa>,
        IWarunekFiltrujacyDane<Rozrachunek>,
        IWarunekFiltrujacyDane<PozycjaHarmonogramuRozrachunku>,
        IWarunekFiltrujacyDane<ProcesOfertowy>,
        IWarunekFiltrujacyDane<ZlecenieSerwisowe>,
        IWarunekFiltrujacyDane<WiadomoscSMS>,
        IWarunekFiltrujacyDane<UmowaKliencka>
    {
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("02197055-ce1d-451a-8ea9-e76045e6ece0");

        public string Nazwa => "Profilowanie danych wg opiekuna klienta";

        public string Opis => "Sferyczny komponent służący do profilowania dostępu do danych. Profilowanie oparte jest na opiekunie klienta i uprawnieniach dostępu do klientów innych opiekunów oraz klientów bez opiekunów.";

        private ISprawdzaczUprawnien _sprawdzaczUprawnien;

        private bool PosiadaUprawnienie(IKontekstFiltrowaniaDanych kontekst, string nazwaUprawnienia)
        {
            if (_sprawdzaczUprawnien == null)
            {
                _sprawdzaczUprawnien = kontekst.Uchwyt.PodajObiektTypu<ISprawdzaczUprawnien>();
            }

            return _sprawdzaczUprawnien.SprawdzStanUprawnienia(nazwaUprawnienia, out DescriptiveBoolean wynik) && wynik;
        }

        public bool CzyAktywny(IKontekstFiltrowaniaDanych kontekst)
        {
            return !PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów") ||
                    !PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");
        }

        Expression<Func<Dokument, bool>> IWarunekFiltrujacyDane<Dokument>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x => x.KonfiguracjaId == KonfiguracjeDokumentow.Oferta_ID || x.Podmiot == null || x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x => x.KonfiguracjaId == KonfiguracjeDokumentow.Oferta_ID || x.Podmiot == null || !x.Podmiot.Opiekunowie.Any() || x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x => x.KonfiguracjaId == KonfiguracjeDokumentow.Oferta_ID || x.Podmiot == null || x.Podmiot.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        /// <remarks>
        /// e-Faktury bez przypisanego klienta nie są widoczne dla użytkowników z obniżonymi uprawnieniami
        /// ponieważ brak przypisania klienta oznacza, że nie został on rozpoznany więc program nie jest w stanie sprawdzić
        /// czy dany dokument powinien być widoczny czy nie.
        /// </remarks>
        Expression<Func<DokumentElektroniczny, bool>> IWarunekFiltrujacyDane<DokumentElektroniczny>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x => x.Podmiot != null && x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x => x.Podmiot != null && (!x.Podmiot.Opiekunowie.Any() || x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id));
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x => x.Podmiot != null && x.Podmiot.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<DokumentOE, bool>> IWarunekFiltrujacyDane<DokumentOE>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x =>
                    x.Opiekun != null && x.Opiekun.Id == kontekst.ZalogowanyUzytkownik.Dane.Id ||
                    x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x =>
                    x.Opiekun == null ||
                    x.Opiekun.Id == kontekst.ZalogowanyUzytkownik.Dane.Id ||
                    !x.Podmiot.Opiekunowie.Any() ||
                    x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x =>
                    x.Opiekun != null ||
                    x.Podmiot.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<DokumentKKD, bool>> IWarunekFiltrujacyDane<DokumentKKD>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x => x.PodmiotAktualny == null || x.PodmiotAktualny.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x => x.PodmiotAktualny == null || !x.PodmiotAktualny.Opiekunowie.Any() || x.PodmiotAktualny.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x => x.PodmiotAktualny == null || x.PodmiotAktualny.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<OperacjaKasowa, bool>> IWarunekFiltrujacyDane<OperacjaKasowa>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x => x.Podmiot == null || x.Podmiot.PodmiotDlaKtoregoHistoria.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x => x.Podmiot == null || !x.Podmiot.PodmiotDlaKtoregoHistoria.Opiekunowie.Any() || x.Podmiot.PodmiotDlaKtoregoHistoria.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x => x.Podmiot == null || x.Podmiot.PodmiotDlaKtoregoHistoria.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<OperacjaBankowa, bool>> IWarunekFiltrujacyDane<OperacjaBankowa>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x => x.Kontrahent == null || x.Kontrahent.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x => x.Kontrahent == null || !x.Kontrahent.Opiekunowie.Any() || x.Kontrahent.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x => x.Kontrahent == null || x.Kontrahent.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<DyspozycjaBankowa, bool>> IWarunekFiltrujacyDane<DyspozycjaBankowa>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x => x.Odbiorca == null || x.Odbiorca.PodmiotDlaKtoregoHistoria.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x => x.Odbiorca == null || !x.Odbiorca.PodmiotDlaKtoregoHistoria.Opiekunowie.Any() || x.Odbiorca.PodmiotDlaKtoregoHistoria.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x => x.Odbiorca == null || x.Odbiorca.PodmiotDlaKtoregoHistoria.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<Rozrachunek, bool>> IWarunekFiltrujacyDane<Rozrachunek>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x => x.Podmiot == null || x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x => x.Podmiot == null || !x.Podmiot.Opiekunowie.Any() || x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x => x.Podmiot == null || x.Podmiot.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<PozycjaHarmonogramuRozrachunku, bool>> IWarunekFiltrujacyDane<PozycjaHarmonogramuRozrachunku>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x => x.Rozrachunek.Podmiot == null || x.Rozrachunek.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x => x.Rozrachunek.Podmiot == null || !x.Rozrachunek.Podmiot.Opiekunowie.Any() || x.Rozrachunek.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x => x.Rozrachunek.Podmiot == null || x.Rozrachunek.Podmiot.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<ProcesOfertowy, bool>> IWarunekFiltrujacyDane<ProcesOfertowy>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x =>
                    x.Opiekun != null && x.Opiekun.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id
                    ||
                    x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x =>
                    x.Opiekun == null ||
                    x.Opiekun.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id ||
                    !x.Podmiot.Opiekunowie.Any() ||
                    x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x =>
                    x.Opiekun != null ||
                    x.Podmiot.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<ZlecenieSerwisowe, bool>> IWarunekFiltrujacyDane<ZlecenieSerwisowe>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x =>
                    x.Opiekun != null && x.Opiekun.Id == kontekst.ZalogowanyUzytkownik.Dane.Id ||
                    x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x =>
                    x.Opiekun == null ||
                    x.Opiekun.Id == kontekst.ZalogowanyUzytkownik.Dane.Id ||
                    !x.Podmiot.Opiekunowie.Any() ||
                    x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x =>
                    x.Opiekun != null ||
                    x.Podmiot.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<WiadomoscSMS, bool>> IWarunekFiltrujacyDane<WiadomoscSMS>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x => x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x => !x.Podmiot.Opiekunowie.Any() || x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x => x.Podmiot.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }

        Expression<Func<UmowaKliencka, bool>> IWarunekFiltrujacyDane<UmowaKliencka>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            var inniOpiekunowe = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów innych opiekunów");
            var brakOpiekunow = PosiadaUprawnienie(kontekst, "Klienci - Dostęp do klientów bez opiekunów");

            if (!inniOpiekunowe && !brakOpiekunow)
            {
                // Brak uprawnienia do klientów innych opiekunów oraz klientów bez opiekunów.
                return x =>
                    x.Opiekun != null && x.Opiekun.Id == kontekst.ZalogowanyUzytkownik.Dane.Id ||
                    x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!inniOpiekunowe)
            {
                // Brak uprawnienia do klientów innych opiekunów, ale dostęp do klientów bez opiekunów.
                return x =>
                    x.Opiekun == null ||
                    x.Opiekun.Id == kontekst.ZalogowanyUzytkownik.Dane.Id ||
                    !x.Podmiot.Opiekunowie.Any() ||
                    x.Podmiot.Opiekunowie.Any(y => y.UzytkownikId == kontekst.ZalogowanyUzytkownik.Dane.Id);
            }
            else if (!brakOpiekunow)
            {
                // Brak uprawnienia do klientów bez opiekunów, ale dostęp do klientów innych opiekunów.
                return x =>
                    x.Opiekun != null ||
                    x.Podmiot.Opiekunowie.Any();
            }
            else
            {
                // Oba uprawnienia nadane.
                return x => true;
            }
        }
    }
}