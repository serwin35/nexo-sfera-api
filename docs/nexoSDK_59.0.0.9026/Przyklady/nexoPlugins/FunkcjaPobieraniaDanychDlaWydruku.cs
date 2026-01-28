using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wydruki.Enums;
using System.IO;
using InsERT.Moria.Klienci;
using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Wydruki;
using System.Net;

namespace NexoPlugins
{
    public class FunkcjaPobieraniaDanychDlaWydruku : IFunkcjaPobieraniaDanychDlaWydruku
    {
        IPodmiotyDane _podmioty;
        IAsortymentyDane _asortymenty;

        //Konstruktor rozszerzenia, pobierający managery potrzebne przy poźniejszej pracy
        public FunkcjaPobieraniaDanychDlaWydruku(IPodmioty podmioty, IAsortymenty asortymenty)
        {
            _podmioty = podmioty.Dane;
            _asortymenty = asortymenty.Dane;
        }

        public Guid Identyfikator => Guid.Parse("748E2729-3397-4AE8-911B-B735FD7BF23A");

        public string Nazwa => "Przykładowa funkcja pobierania dodatkowych danych na wydruku.";

        public string Opis => "Na podstawie przekazanego kontekstu, w tym przypadku danych obiektu, funkcja dostarcza \"dodatkowe\" dane w postaci nazwy lub nazwy skróconej.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        TypWzorcaWydruku[] IFunkcjaPobieraniaDanychDlaWydruku.TypyWzorcowWydruku => new TypWzorcaWydruku[] { TypWzorcaWydruku.FakturaSprzedazy };

        // Główna metoda zwracająca nazwę lub nazwę skróconą asortymentu lub podmiotu rozpoznając na podstawie opisu lub typu przekazanej encji
        public object Pobierz(object parametr)
        {
            //if (parametr == null)
            //    return "(null)";

            //rozpoznanie na podstawie opisu i identyfikatora, np "Podmiot:120000"
            if (parametr is string)
            {
                var czesci = ((string)parametr).Split(':');
                switch (czesci[0])
                {
                    case "Podmiot":
                        parametr = new Podmiot() { Id = int.Parse(czesci[1]) };
                        break;
                    case "Asortyment":
                        parametr = new Asortyment() { Id = int.Parse(czesci[1]) };
                        break;
                    default:
                        return null;
                }
            }

            //rozpoznanie na podstawie typu przekazanego parametru
            var a = parametr as Asortyment;
            if (a != null && _asortymenty != null)
                return _asortymenty.Wszystkie().Where(i => i.Id == a.Id).Single().Nazwa;

            var p = parametr as Podmiot;
            if (p != null && _podmioty != null)
                return _podmioty.Wszystkie().Where(i => i.Id == p.Id).Single().NazwaSkrocona;

            //if (parametr != null)
            //return parametr.GetType().FullName;

            return null;
        }
    }

    //Dane przekazywane do wzorca wydruku
    public class Dane
    {
        public int Id { get; set; }
        public string Nazwa { get; set; }
        public byte[] Obrazek { get; set; }
    }

    public class DodatkowyObiektDlaWydruku : IDodatkowyObiektDlaWydruku
    {
        public List<TypWzorcaWydruku> TypyWzorcowWydruku => new List<TypWzorcaWydruku>(new TypWzorcaWydruku[] { TypWzorcaWydruku.FakturaSprzedazy });

        public Type TypDanych => typeof(Dane);

        public string NazwaObiektu => "MojeObrazki";

        public Guid Identyfikator => Guid.Parse("E4ECF0F0-3C3C-4494-93BF-7B445C62E23E");

        public string Nazwa => "Przykładowy dodatkowy obiekt dla wydruku faktury sprzedaży.";

        public string Opis => "Funkcja rejestrowana jest w wydruku FS jako dodatkowe źródło danych. Dostarcza obrazki pobrane z internetu.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        TypWzorcaWydruku[] IDodatkowyObiektDlaWydruku.TypyWzorcowWydruku => new TypWzorcaWydruku[] { TypWzorcaWydruku.FakturaSprzedazy };

        public object PobierzDane()
        {
            List<Dane> dane = new List<Dane>();
            int id = 1;
            try
            {
                using (WebClient client = new WebClient())
                {
                    foreach (var file in obrazki)
                        dane.Add(new Dane() { Id = id++, Nazwa = file, Obrazek = client.DownloadData("https://www.insert.com.pl/.grafika/pudelka/" + file) });
                }
            }
            catch (Exception)
            { }
            return dane;
        }

        string[] obrazki = new string[]
        {
            "subiekt_nexo_pro.png",
            "gestor_nexo_pro.png",
            "rachmistrz_nexo_pro.png",
            "rewizor_nexo_pro.png",
            "gratyfikant_nexo_pro.png",
        };
    }
}
