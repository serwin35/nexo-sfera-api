using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Rozszerzanie.Operacje;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OperacjeSferyczne.OperacjeNaDokumentach
{
    public class UsunPozycje : OperacjaWOknieObiektu<IDokument>
    {
        public override string Nazwa => "Usuń wszystkie pozycje";

        protected override void Wykonaj(
            IDokument obiekt,
            IKontekstOknaObiektu kontekstOknaObiektu,
            IKontekstOperacji kontekstOperacji)
        {
            Dokument dokument = obiekt.Dokument;
            List<PozycjaDokumentu> pozycjeDoUsuniecia = PodajPozycjeDoUsuniecia(dokument);
            List<(PozycjaDokumentu pozycja, Exception blad)> bledy = new List<(PozycjaDokumentu pozycja, Exception blad)>();

            foreach (PozycjaDokumentu pozycja in pozycjeDoUsuniecia)
            {
                try
                {
                    if (dokument.Pozycje.Contains(pozycja))
                    {
                        dokument.Pozycje.Remove(pozycja);
                    }
                }
                catch (Exception e)
                {
                    bledy.Add((pozycja, e));
                }
            }

            if (bledy.Count > 0)
            {
                PokazOknoZInformacjaOBledach(kontekstOperacji.Uchwyt, bledy);
            }

            if (pozycjeDoUsuniecia.Count != bledy.Count) // jeśli jakakolwiek pozycja została usunięta, to odświeżamy formatkę
            {
                kontekstOknaObiektu.OdswiezZawartosc();
            }
        }

        private static List<PozycjaDokumentu> PodajPozycjeDoUsuniecia(
            Dokument dokument)
        {
            return dokument
                .Pozycje
                .OrderBy(p => p.LP)
                .ToList();
        }

        private static void PokazOknoZInformacjaOBledach(
            IUchwyt uchwyt,
            List<(PozycjaDokumentu pozycja, Exception blad)> bledy)
        {
            Okna.PokazOknoZBledem(
                uchwyt,
                $"Nie udało się usunąć niektórych pozycji:\r\n{string.Join("\r\n", bledy.Select(b => $"{b.pozycja.LP}: {b.blad.Message}"))}");
        }
    }
}
