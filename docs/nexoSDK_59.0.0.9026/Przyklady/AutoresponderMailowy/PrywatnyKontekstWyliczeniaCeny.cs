using System;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.CennikiICeny;

namespace AutoresponderMailowy
{
	internal class PrywatnyKontekstWyliczeniaCeny : IKontekstWyliczeniaCeny
    {
        public Dokument Dokument { get; set; }

        public Podmiot Podmiot { get; set; }

        public PozycjaDokumentu PozycjaDokumentu { get; set; }

        public ZlecenieSerwisowe ZlecenieSerwisowe => throw new NotImplementedException();
	}
}
