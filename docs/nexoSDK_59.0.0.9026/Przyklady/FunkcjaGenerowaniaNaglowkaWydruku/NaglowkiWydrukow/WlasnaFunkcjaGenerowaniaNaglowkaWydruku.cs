using System;
using System.Text;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wydruki.FunkcjeGenerowaniaNaglowka;

namespace NaglowkiWydrukow
{
    public class WlasnaFunkcjaGenerowaniaNaglowkaWydruku : IFunkcjaGenerowaniaNaglowka
    {
        public Guid Identyfikator => new Guid("60C78A1D-8CF5-4BBF-8F60-48CB0ED10FC0");

        public string Nazwa => "Własny generator";

        public string Opis => "Generuje nagłówek wg własnego przepisu";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string WygenerujNaglowek(NaglowekWydruku naglowek, WzorzecWydruku wzorzec, object obiektDrukowany)
        {
            if (obiektDrukowany is Dokument dokument)
            {
                bool angielski = wzorzec.Nazwa.Contains("angielski");
                bool niemiecki = wzorzec.Nazwa.Contains("niemiecki");
                // generujemy nagłówek dla dokumentu.
                StringBuilder builder = new StringBuilder();
                if (angielski)
                    builder.Append("Header generated with external solution");
                else if (niemiecki)
                    builder.Append("Header, der von einer externen Lösung generiert wird");
                else
                    builder.Append("Nagłówek wygenerowany rozwiązaniem zewnętrznym");
                builder.Append(Environment.NewLine);
                if (angielski)
                    builder.Append("For company: ");
                else if (niemiecki)
                    builder.Append("Für die Firma: ");
                else
                    builder.Append("Dla firmy: ");
                builder.Append(dokument.MojaFirma.Firma.Nazwa);
                builder.Append(Environment.NewLine);
                if (angielski)
                    builder.Append("Placed ");
                else if (niemiecki)
                    builder.Append("Befindet sich ");
                else
                    builder.Append("Znajdującej się przy ");
                builder.Append(dokument.AdresMojejFirmy.LiniaCalosc);
                builder.Append(Environment.NewLine);
                if (angielski)
                    builder.Append("Branch: ");
                else if (niemiecki)
                    builder.Append("Ast: ");
                else
                    builder.Append("Oddział: ");
                builder.Append(dokument.MiejsceWprowadzenia is Oddzial oddzial ? oddzial.Nazwa : (angielski ? "Headquarter" : niemiecki ? "Hauptquartier" : "Centrala"));
                return builder.ToString();
            }
            return naglowek.Wartosc; // dla obiektu innego niż dokument zwracamy zapisaną treść nagłówka
        }
    }
}
