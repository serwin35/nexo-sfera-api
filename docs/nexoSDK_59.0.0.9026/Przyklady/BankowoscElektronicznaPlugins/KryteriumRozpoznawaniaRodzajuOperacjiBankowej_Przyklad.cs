using InsERT.Moria.Bank;
using InsERT.Moria.Rozszerzanie;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BankowoscElektronicznaPlugins
{
    class KryteriumRozpoznawaniaRodzajuOperacjiBankowej_Przyklad : IKryteriumRozpoznawaniaRodzajuOperacjiBankowej
    {
        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public Guid Identyfikator => new Guid("C574C7D9-C86F-4AB6-8E0E-D2A38EC8F0FF");

        public string Nazwa => "Szczegóły posiadają wartość";

        public string Opis => "Kryterium sprawdza czy zawartość pola \"Szczegóły\" spełnia zadany warunek";

        public IEnumerable<IParametrKryteriumRozpoznawaniaRodzajuOperacjiBankowej> Parametry => new IParametrKryteriumRozpoznawaniaRodzajuOperacjiBankowej[]
        {
            new ParametrKryteriumRozpoznawaniaRodzajuOperacjiBankowej()
            {
                Id = new Guid("11EE2F22-0AA0-4FF0-866A-7EBF09331FC8"),
                Nazwa = "nazwa parametru",
                TypWartosci = TypWartosciKryteriumRozpoznawaniaRodzajuOperacjiBankwej.String,
                Operatory = new List<OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej>()
                   {
                        OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.RownaSie,
                        OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.RoznyOd,
                        OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.Zawiera,
                        OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.NieZawiera,
                        OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.RozpoczynaSieOd,
                        OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.KonczySieNa
                   }
            }
        };

        public bool Sprawdz(IKontekstKryteriumRozpoznawaniaRodzajuOperacjiBankowych kontekstKryterium)
        {
            if (kontekstKryterium.ElektronicznaOperacjaBankowa == null ||
                   kontekstKryterium.Parametry.Count != 1)
            {
                return false;
            }

            var parametr = kontekstKryterium.Parametry[0];
            var wybranyOperator = (OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej)parametr.Operator;

            if (string.IsNullOrEmpty(parametr.Wartosc))
            {
                return false;
            }
            string wartoscTekstowa = kontekstKryterium.ElektronicznaOperacjaBankowa.TrescOperacji;
            string wartoscKryterium = parametr.Wartosc;

            if (string.IsNullOrEmpty(wartoscTekstowa)) return string.IsNullOrEmpty(wartoscKryterium);

            if (!kontekstKryterium.RozroznianieWielkosciLiter)
            {
                wartoscTekstowa = wartoscTekstowa.ToUpper();
                wartoscKryterium = wartoscKryterium.ToUpper();
            }

            if (!kontekstKryterium.RozroznianieZnakowDiakrytycznych)
            {
                wartoscTekstowa = UsunPolskieZnaki(wartoscTekstowa);
                wartoscKryterium = UsunPolskieZnaki(wartoscKryterium);
            }

            switch (wybranyOperator)
            {
                case OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.RownaSie:
                    return wartoscTekstowa.Equals(wartoscKryterium);
                case OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.RoznyOd:
                    return !wartoscTekstowa.Equals(wartoscKryterium);
                case OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.Zawiera:
                    return wartoscTekstowa.Contains(wartoscKryterium);
                case OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.NieZawiera:
                    return !wartoscTekstowa.Contains(wartoscKryterium);
                case OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.RozpoczynaSieOd:
                    return wartoscTekstowa.StartsWith(wartoscKryterium, StringComparison.Ordinal);
                case OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej.KonczySieNa:
                    return wartoscTekstowa.EndsWith(wartoscKryterium, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        private string UsunPolskieZnaki(string text)
        {
            StringBuilder builder = new StringBuilder();

            foreach (char character in text.Normalize(NormalizationForm.FormD))
            {
                if (character == 'ł')
                    builder.Append('l');
                else if (character == 'Ł')
                    builder.Append('L');
                else if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                    builder.Append(character);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }

    public class ParametrKryteriumRozpoznawaniaRodzajuOperacjiBankowej : IParametrKryteriumRozpoznawaniaRodzajuOperacjiBankowej
    {
        public Guid Id { get; set; }

        public string Nazwa { get; set; }

        public Type TypObiektu { get; set; }

        public IEnumerable<OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej> Operatory { get; set; } = new List<OperatorKryteriumRozpoznawaniaRodzajuOperacjiBankowej>();

        public TypWartosciKryteriumRozpoznawaniaRodzajuOperacjiBankwej TypWartosci { get; set; }
    }
}
