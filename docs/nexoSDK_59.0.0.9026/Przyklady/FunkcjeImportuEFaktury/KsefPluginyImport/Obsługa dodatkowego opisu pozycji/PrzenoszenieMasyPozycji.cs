using InsERT.Moria.Asortymenty;
using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KsefPluginyImport
{
    public class PrzenoszenieMasyPozycji : IFunkcjaObslugiDodatkowegoOpisuPozycji
    {
        private readonly Guid _id = new Guid("C6802376-72E1-4654-B9AD-561C2C6C099F");
        private readonly IAsortymenty _asortymenty;
        public PrzenoszenieMasyPozycji(IAsortymenty asortymenty)
        {
            _asortymenty = asortymenty ?? throw new ArgumentNullException(nameof(asortymenty));
        }

        public string DomyslnyKlucz => "MASA";

        public Guid Identyfikator => _id;

        public string Nazwa => "Przepisywanie masy pozycji";

        public string Opis => "Funkcja przepisuje wartość z dodatkowego opisu do pola Masa na pozycji dokumentu.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public bool CzyMoznaUzyc(IDokument dokument, PozycjaDokumentu pozycja) => pozycja.JednostkaMiaryAs != null && pozycja.JednostkaMiaryAs.JednostkaMiaryMasy != null;

        public void Obsluz(IDokument dokument, PozycjaDokumentu pozycja, IEnumerable<DodatkowyOpisEFaktury> dodatkowyOpis)
        {
            if (dodatkowyOpis.Count() == 1)
            {
                string wartosc = dodatkowyOpis.Single().Wartosc;
                if (!string.IsNullOrWhiteSpace(wartosc)
                    && decimal.TryParse(wartosc, out decimal masa))
                {
                    if (pozycja.AsortymentAktualny != null)
                    {
                        // na pozycji rozpoznano asortyment więc możemy go zaktualizować:
                        decimal masaJednostkowa = Math.Round(masa / pozycja.Ilosc, pozycja.JednostkaMiaryAs.JednostkaMiaryMasy?.Precyzja ?? 3, MidpointRounding.AwayFromZero);
                        if (pozycja.JednostkaMiaryAs.Masa != masaJednostkowa)
                        {                            
                            using (IAsortyment asortyment = _asortymenty.Znajdz(pozycja.AsortymentAktualny))
                            {
                                JednostkaMiaryAsortymentu jednostkaMiary = asortyment.Dane.JednostkiMiar.FirstOrDefault(jma => jma.Id == pozycja.JednostkaMiaryAsId);
                                if (jednostkaMiary != null)
                                {
                                    jednostkaMiary.Masa = masaJednostkowa;
                                    if (!asortyment.Zapisz())
                                        throw new InvalidOperationException("Nie udało się zapisać masy jednostki miary asortymentu.");
                                }
                            }
                        }
                    }

                    // aktualizujemy masę na pozycji:
                    if (pozycja.JednostkaMasy == null)
                        pozycja.JednostkaMasy = pozycja.JednostkaMiaryAs.JednostkaMiaryMasy;
                    pozycja.Masa = masa;
                }
            }
        }
    }
}
