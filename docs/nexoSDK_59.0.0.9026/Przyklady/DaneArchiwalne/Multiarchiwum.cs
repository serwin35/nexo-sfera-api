using InsERT.Moria.Archiwa;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DaneArchiwalne
{
    public interface IZrodlo
    {
        int Id { get; }
        XElement Konfiguracja { get; }
        int OryginalnyId { get; }
    }
    public class DokumentSprzedazyEx : DokumentSprzedazy, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class DokumentZakupuEx : DokumentZakupu, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class KorektaDokumentuSprzedazyEx : KorektaDokumentuSprzedazy, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class ParagonEx : Paragon, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class ZwrotDoParagonuEx : ZwrotDoParagonu, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class KorektaDokumentuZakupuEx : KorektaDokumentuZakupu, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class ZamowienieOdKlientaEx : ZamowienieOdKlienta, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class ZamowienieDoDostawcyEx : ZamowienieDoDostawcy, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class WydanieMagazynoweEx : WydanieMagazynowe, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class PrzyjecieMagazynoweEx : PrzyjecieMagazynowe, IZrodlo
    {
        public XElement Konfiguracja { get; set; }
        public int OryginalnyId { get; set; }
        int IZrodlo.Id { get { return Id; } }
    }

    public class TypPodgladuEx : TypPodgladu
    {
        public XElement Konfiguracja { get; set; }
        public int KonfiguracjaHashCode { get { return Konfiguracja.ToString().GetHashCode(); } }
        public int OryginalnyId { get; set; }
    }
}
