using InsERT.Moria.KsiegowoscPelna;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wspolne;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace ProfilowanieDanych
{
    /// <summary>
    /// Przykładowy plugin ukrywajacy przed użytkownikiem z łańcuchem "szpieg" w nazwie (loginie) [uwaga: wielkość liter ma znaczenie!]:
    /// * wszystkie konta zaczynające się na "13" (np.: 130-01) oraz 
    /// * konta kończące się na "666" (np.: 201-00666) (uwaga: tylko zainstancjonowane!)
    /// * konta kontrahenta z NIPem "2222222222"
    /// * wszystkie konta kończące się na "777" (niezależnie czy zainstancjonowane czy nie)
    /// </summary>
    public class ProfilowaniePlanuKontPlugin :
        IWarunekFiltrujacyDane<DefinicjaKontaKsiegowego>,
        IWarunekFiltrujacyDane<KontoKsiegowe>,
        IWarunekFiltrujacyDane<ElementKartotekiKsiegowej>
    {
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("C738D418-7C0B-4AAC-A19B-C5712F6A1F92");

        public string Nazwa => "Profilowanie planu kont (przykład)";

        public string Opis => "Przykład profilowania planu kont";


        public bool CzyAktywny(IKontekstFiltrowaniaDanych kontekst)
        {
            if (kontekst.ZalogowanyUzytkownik.Dane.Login.Contains("szpieg"))
                return true;

            return false;
        }

        Expression<Func<DefinicjaKontaKsiegowego, bool>> IWarunekFiltrujacyDane<DefinicjaKontaKsiegowego>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            return dkk => 
                !dkk.PelnyNumer.StartsWith("13") && 
                (dkk.Kartoteka_Id.HasValue || dkk.DefinicjePodrzedne.Any() || !dkk.PelnyNumer.EndsWith("777"));
        }

        Expression<Func<KontoKsiegowe, bool>> IWarunekFiltrujacyDane<KontoKsiegowe>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            return kk => !kk.Numer.EndsWith("666") && !kk.Numer.EndsWith("777");
        }

        Expression<Func<ElementKartotekiKsiegowej, bool>> IWarunekFiltrujacyDane<ElementKartotekiKsiegowej>.WygenerujWyrazenieFiltrujace(IKontekstFiltrowaniaDanych kontekst)
        {
            return ek => !ek.NumerAnalityki.EndsWith("777") && !(ek.WszystkieElementyKartotekiZrodlowej.Any(ekz => ekz.Podmiot.NIP == "2222222222"));
        }
    }
}
