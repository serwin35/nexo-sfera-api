using InsERT.Moria.Dokumenty.Logistyka;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using System;
using System.Linq;


namespace PrzykladyKsef.Operacje
{
    /// <summary>
    /// Przykład przedstawia sposób importu odebranej e-Faktury jako dokument Subiekta (faktura zakupu lub korekta faktury zakupu).
    /// </summary>
    public class ImportOdebranejEFaktury : BazowaOperacja
    {
        public ImportOdebranejEFaktury(Uchwyt sfera) : base(sfera) { }

        public override void Wykonaj()
        {
            ImportEFakturyJakoDokumentSubiekta("2273161387-20220502-45B2A2-6E8ECE-01");
            ImportEFakturyJakoDokumentSubiekta("2273161387-20220419-1D5286-7FFF0A-B6");
            ImportEFakturyJakoDokumentSubiekta();
        }

        private void ImportEFakturyJakoDokumentSubiekta()
        {
            DokumentElektroniczny importowany = ZnajdzEFaktureDoPrzetworzenia();
            if (importowany == null)
            {
                Console.WriteLine("Nie znaleziono dokumentu do przetworzenia.");
                return;
            }
            ImportEFakturyJakoDokumentSubiekta(importowany);
        }

        private void ImportEFakturyJakoDokumentSubiekta(string ksefId)
        {
            IDokumentyElektroniczne dokumentyElektroniczne = _sfera.DokumentyElektroniczne();
            DokumentElektroniczny importowany = dokumentyElektroniczne.Dane.Wszystkie().Where(d => d.NumerKSeF == ksefId).FirstOrDefault();
            if (importowany == null)
            {
                Console.WriteLine("Nie znaleziono dokumentu o podanym numerze KSeF.");
                return;
            }
            ImportEFakturyJakoDokumentSubiekta(importowany);
        }

        private void ImportEFakturyJakoDokumentSubiekta(DokumentElektroniczny importowany)
        {
            if (importowany.RodzajFaktury == (byte)RodzajEFaktury.KOR
                || importowany.RodzajFaktury == (byte)RodzajEFaktury.KOR_ROZ
                || importowany.RodzajFaktury == (byte)RodzajEFaktury.KOR_ZAL)
            {
                Console.WriteLine("Import e-Faktury jako korekta zakupu.");
                IKorektyDokumentowZakupu korektyZakupu = _sfera.KorektyDokumentowZakupu();
                using (IKorektaDokumentuZakupu korekta = korektyZakupu.UtworzKorekteFakturyZakupu())
                {
                    korekta.ObslugaImportuEFaktur.WypelnijNaPodstawieDokumentuElektronicznego(importowany);
                    if (korekta.Zapisz())
                        Console.WriteLine($"Poprawnie zapisano importowaną korektę dokumentu zakupu: {korekta.Dane.NumerWewnetrzny.PelnaSygnatura}");
                    else
                        korekta.WypiszBledy();
                }
            }
            else
            {
                Console.WriteLine("Import e-Faktury jako faktura zakupu.");
                IDokumentyZakupu dokumentyZakupu = _sfera.DokumentyZakupu();
                using (IDokumentZakupu dokument = dokumentyZakupu.UtworzFaktureZakupu())
                {
                    dokument.ObslugaImportuEFaktur.WypelnijNaPodstawieDokumentuElektronicznego(importowany);
                    if (dokument.Zapisz())
                        Console.WriteLine($"Poprawnie zapisano importowany dokument zakupu: {dokument.Dane.NumerWewnetrzny.PelnaSygnatura}");
                    else
                        dokument.WypiszBledy();
                }
            }
        }
    }
}
