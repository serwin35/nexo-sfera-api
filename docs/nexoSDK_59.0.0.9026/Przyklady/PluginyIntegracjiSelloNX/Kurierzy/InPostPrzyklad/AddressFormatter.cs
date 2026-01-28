using InsERT.Moria.ModelDanych;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InPostPrzyklad
{
    /// <summary>
    /// Klasa pomocnicza, ułatwiająca przetwarzanie adresów nexo z linii na szczegóły - przydatne przy komunikacji z kurierami
    /// Uwaga! Docelowo format ze szczegółami będzie dostarczany przez Sello NX, więc nie będzie potrzeby używać tego kodu
    /// </summary>
    public static class AddressFormatter
    {
        private const int MiejscowoscLength = 64;
        private const int NrDomuLength = 10;
        private const int NrLokaluLength = 10;
        private const int UlicaLength = 64;
        private const int PocztaLength = 64;
        private const string KodPocztowyFormat = @"[0-9][0-9]([-]|[ ])?[0-9][0-9][0-9]";

        #region Ze szczegółów na linie
        public static IEnumerable<string> NaFormatPocztowy(AdresSzczegoly adresSzczegoly)
        {
            return NaFormatPocztowy(adresSzczegoly.Ulica, adresSzczegoly.NrDomu, adresSzczegoly.NrLokalu, adresSzczegoly.KodPocztowy, adresSzczegoly.Miejscowosc, adresSzczegoly.Poczta);
        }

        public static IEnumerable<string> NaFormatPocztowy(
            string ulica,
            string nrDomu,
            string nrLokalu,
            string kodPocztowy,
            string miasto,
            string poczta)
        {
            List<string> Linie = new List<string>();

            if (string.IsNullOrEmpty(poczta) ||//poczta jest null
                string.Compare(poczta, miasto, StringComparison.OrdinalIgnoreCase) == 0)//lub taka sama z dokładnością do wielkości liter
            {
                //poczta w tej samej miejscowości
                if (string.IsNullOrEmpty(ulica))
                {
                    //miejscowość nie ma ulic
                    //adres w miejscowości bez ulicy i z pocztą w tej samej miejscowości ma postać
                    //34-345 Kozia Wólka 4/4
                    Linie.Add(string.Format("{0} {1} {2}", kodPocztowy, miasto, FormatujNumer(nrDomu, nrLokalu)));
                }
                else
                {
                    //miejscowość ma ulice
                    //adres w miejscowości z ulicą i z pocztą w tej samej miejscowości ma postać
                    //ul. Nowa 1/2
                    //12-123 Wrocław
                    Linie.Add(string.Format("{0} {1}", ulica, FormatujNumer(nrDomu, nrLokalu)));
                    Linie.Add(string.Format("{0} {1}", kodPocztowy, miasto));
                }
            }
            else
            {
                //jest poczta
                if (string.IsNullOrEmpty(ulica))
                {
                    //miejscowość nie ma ulic
                    //adres w miejscowości bez ulicy i z pocztą w innej miejscowości ma postać
                    //Kozia Wólka 4/4
                    //34-345 Wrocław
                    Linie.Add(string.Format("{0} {1}", miasto, FormatujNumer(nrDomu, nrLokalu)));
                    Linie.Add(string.Format("{0} {1}", kodPocztowy, poczta));
                }
                else
                {
                    //miejscowość ma ulice
                    //adres w miejscowości z ulicą i z pocztą w innej miejscowości ma postać
                    //ul. Nowa 1/2
                    //Kozia Wólka
                    //12-123 Wrocław
                    Linie.Add(string.Format("{0} {1}", ulica, FormatujNumer(nrDomu, nrLokalu)));
                    Linie.Add(miasto);
                    Linie.Add(string.Format("{0} {1}", kodPocztowy, poczta));
                }
            }
            return Linie;
        }

        public static string FormatujNumer(string numerDomu, string numerLokalu)
        {
            return string.IsNullOrWhiteSpace(numerLokalu) ? numerDomu : string.Format("{0}/{1}", numerDomu, numerLokalu);
        }
        #endregion

        #region Z linii na szczegóły
        public static void NaSzczegolyZLini(Adres adres)
        {
            if (adres == null)
                return;

            if (adres.Szczegoly == null)
                adres.Szczegoly = new AdresSzczegoly();
            else
                WyczyscSzczegoly(adres);

            IEnumerable<string> linie = new string[] { adres.Linia1, adres.Linia2, adres.Linia3 }.Where(s => !string.IsNullOrEmpty(s));

            if (adres.Panstwo == null || adres.Panstwo.KodPanstwaUE == "PL")
            {
                PrzetworzAdresPolski(adres, linie);
            }
            else
            {
                PrzetworzAdresZagraniczny(adres, linie);
            }
        }

        private static void PrzetworzAdresPolski(Adres adres, IEnumerable<string> linie)
        {
            var liczbaLinii = linie.Count();
            if (liczbaLinii == 1)//tu możnaby popracować, na razie wyszukujemy kod pocztowy, a resztę wrzucamy jako miejscowość
            {
                bool wynik = PrzetworzLinieZKodemIMiejscowoscia(linie.First(), adres);
                if (!wynik)
                    PrzetworzLinieZUlica(linie.First(), adres);
            }
            else if (liczbaLinii == 2)
            {
                Przetworz2liniowyAdres(linie, adres);
            }
            else if (liczbaLinii == 3)
            {
                Przetworz3liniowyAdres(linie, adres);
            }
        }

        private static void WyczyscSzczegoly(Adres adres)
        {
            if (adres.Szczegoly != null)
            {
                adres.Szczegoly.Ulica = string.Empty;
                adres.Szczegoly.NrDomu = string.Empty;
                adres.Szczegoly.NrLokalu = string.Empty;
                adres.Szczegoly.KodPocztowy = string.Empty;
                adres.Szczegoly.Miejscowosc = string.Empty;
                adres.Szczegoly.Poczta = string.Empty;
            }
        }

        private static bool Przetworz2liniowyAdres(IEnumerable<string> linie, Adres adres)
        {
            int liniaZKodem = -1;
            string[] tabLinii = linie.ToArray();
            for (int i = 0; i < 2; i++)
            {
                if (LiniaZawieraKodPocztowy(tabLinii[i]))
                {
                    liniaZKodem = i;
                    break;
                }
            }
            if (liniaZKodem < 0)
                return false;
            if (!PrzetworzLinieZKodemIMiejscowoscia(tabLinii[liniaZKodem], adres))
                return false;
            if (!PrzetworzLinieZUlica(tabLinii[1 - liniaZKodem], adres))
                return false;

            return true;
        }

        private static bool Przetworz3liniowyAdres(IEnumerable<string> linie, Adres adres)
        {
            foreach (string linia in linie)
            {
                if (!PrzetworzLinieW3Liniowym(linia, adres))
                    return false;
            }
            return true;
        }

        private static bool PrzetworzLinieW3Liniowym(string linia, Adres adres)
        {
            if (LiniaZawieraKodPocztowy(linia))
                return PrzetworzLinieZKodemIPoczta(linia, adres);
            else
            {
                var match = Regex.Match(linia, @"ul|pl|al");
                if (match.Success)
                    return PrzetworzLinieZUlica(linia, adres);
                else
                {
                    match = Regex.Match(linia, @"[0-9]+", RegexOptions.IgnoreCase);
                    if (match.Success)
                        return PrzetworzLinieZUlica(linia, adres);
                    else
                    {
                        return PrzetworzLinieZMiejscowoscia(linia, adres);
                    }
                }
            }
        }

        private static bool LiniaZawieraKodPocztowy(string linia)
        {
            return Regex.Match(linia, KodPocztowyFormat).Success;
        }

        private static bool PrzetworzLinieZMiejscowoscia(string linia, Adres adres)
        {
            adres.Szczegoly.Miejscowosc = linia.Ogranicz(MiejscowoscLength);
            return true;
        }

        private static bool PrzetworzLinieZUlica(string line, Adres address)
        {
            Match matchComplexNr = Regex.Match(line, @"([0-9][a-z0-9]{0,5})\s*(?:/|m|m\.|lok|lok\.)\s*([a-z0-9]+)", RegexOptions.IgnoreCase);
            if (matchComplexNr.Success && matchComplexNr.Groups.Count == 3)
            {
                address.Szczegoly.NrDomu = matchComplexNr.Groups[1].ToString().TrimEnd().Ogranicz(NrDomuLength);
                address.Szczegoly.NrLokalu = matchComplexNr.Groups[2].ToString().TrimEnd().Ogranicz(NrLokaluLength);
                address.Szczegoly.Ulica = UsunUlice(line.Substring(0, matchComplexNr.Index).Trim()).Ogranicz(UlicaLength);
            }
            else
            {
                Match matchNr = Regex.Match(line, @"[0-9][^,\s]*", RegexOptions.IgnoreCase);
                int index = -1;
                int lenght = 0;
                while (matchNr.Success)
                {
                    index = matchNr.Index;
                    lenght = matchNr.Length;
                    matchNr = matchNr.NextMatch();
                }
                if (index > 0)
                {
                    address.Szczegoly.NrDomu = line.Substring(index, lenght).Trim().Ogranicz(NrDomuLength);
                    address.Szczegoly.Ulica = UsunUlice(line.Substring(0, index).Trim()).Trim().Ogranicz(UlicaLength);
                }
                else
                {
                    address.Szczegoly.Ulica = UsunUlice(line.Trim()).Ogranicz(UlicaLength);
                }

            }
            return true;
        }

        private static string UsunUlice(string ulica)
        {
            string retValue = ulica;
            if (ulica.StartsWith("ul.", StringComparison.Ordinal))
                retValue = ulica.Remove(0, 3);
            else if (ulica.StartsWith("ulica", StringComparison.Ordinal))
                retValue = ulica.Remove(0, 5);
            else if (ulica.StartsWith("ul", StringComparison.Ordinal))
                retValue = ulica.Remove(0, 2);
            return retValue.Trim();
        }

        private static bool PrzetworzLinieZKodemIMiejscowoscia(string line, Adres address)
        {
            Match match = Regex.Match(line, KodPocztowyFormat);
            if (match.Success)
            {
                address.Szczegoly.KodPocztowy = line.Substring(match.Index, match.Length);
                if (match.Index < 3)
                {
                    address.Szczegoly.Miejscowosc = line.Substring(match.Index + match.Length).Trim().Ogranicz(MiejscowoscLength);
                }
                else
                    address.Szczegoly.Miejscowosc = line.Substring(0, match.Index).Trim().Ogranicz(MiejscowoscLength);

                return address.Szczegoly.Miejscowosc.Length > 1;
            }
            else
                return false;
        }

        private static bool PrzetworzLinieZKodemIPoczta(string line, Adres address)
        {
            Match match = Regex.Match(line, KodPocztowyFormat);
            if (match.Success)
            {
                address.Szczegoly.KodPocztowy = line.Substring(match.Index, match.Length);
                if (match.Index < 3)
                {
                    address.Szczegoly.Poczta = line.Substring(match.Index + match.Length).Trim().Ogranicz(PocztaLength);
                }
                else
                    address.Szczegoly.Poczta = line.Substring(0, match.Index).Trim().Ogranicz(PocztaLength);

                return address.Szczegoly.Poczta.Length > 1;
            }
            else
                return false;
        }

        #region adres zagraniczny

        private static void PrzetworzAdresZagraniczny(Adres adres, IEnumerable<string> linie)
        {
            var liczbaLinii = linie.Count();
            if (liczbaLinii == 1)
            {
                // 26579 Baltrum
                PrzetworzLinieZKodemIMiejscowosciaZagraniczna(linie.Last(), adres);
            }
            else if (liczbaLinii == 2)
            {
                // Ruhe st. 10
                // 3746 Monachium
                PrzetworzLinieZUlicaZagraniczna(linie.First(), adres);
                PrzetworzLinieZKodemIMiejscowosciaZagraniczna(linie.Last(), adres);
            }
            else if (liczbaLinii == 3)
            {
                // Ruhe st. 10
                // Monachium
                // 3746 Monachium
                PrzetworzLinieZUlicaZagraniczna(linie.First(), adres);
                PrzetworzLinieZMiejscowoscia(linie.Skip(1).First(), adres);
                PrzetworzLinieZKodemIPocztaZagraniczna(linie.Last(), adres);
            }
        }

        private static void PrzetworzLinieZUlicaZagraniczna(string linia, Adres adres)
        {
            // szukamy czy jest znak "/" pomiędzy numerami (akceptując również numery alfanumreczne, np. 2A/4)
            Match matchNrDomuIMieszkania = Regex.Match(linia, @"([0-9][a-z0-9]{0,5})\s*(?:/)\s*([a-z0-9]+)", RegexOptions.IgnoreCase);
            if (matchNrDomuIMieszkania.Success && matchNrDomuIMieszkania.Groups.Count == 3)
            {
                adres.Szczegoly.NrDomu = matchNrDomuIMieszkania.Groups[1].ToString().TrimEnd().Ogranicz(NrDomuLength);
                adres.Szczegoly.NrLokalu = matchNrDomuIMieszkania.Groups[2].ToString().TrimEnd().Ogranicz(NrLokaluLength);
                adres.Szczegoly.Ulica = linia.Substring(0, matchNrDomuIMieszkania.Index).Trim().Ogranicz(UlicaLength);
            }
            else
            {
                Match matchNrDomu = Regex.Match(linia, @"[0-9][^,]*", RegexOptions.IgnoreCase);
                int index = -1;
                int lenght = 0;
                while (matchNrDomu.Success)
                {
                    index = matchNrDomu.Index;
                    lenght = matchNrDomu.Length;
                    matchNrDomu = matchNrDomu.NextMatch();
                }
                if (index > 0)
                {
                    adres.Szczegoly.NrDomu = linia.Substring(index, lenght).Trim().Ogranicz(NrDomuLength);
                    adres.Szczegoly.Ulica = linia.Substring(0, index).Trim().Ogranicz(UlicaLength);
                }
                else
                {
                    adres.Szczegoly.Ulica = linia.Trim().Ogranicz(UlicaLength);
                }
            }
        }

        /// <summary>
        /// Metoda zakłada, że:
        /// kod pocztowy będzie na początku linii,
        /// będzie się składał z 1 lub 2 "wyrazów" (ciągów znaków oddzielonych białymi znakami),
        /// będzie zawierał cyfry (ale oprócz cyfr mogą być też litery),
        /// w przypadku kodu 2-wyrazowego pierwszy wyraz będzie zawierał cyfrę, a drugi będzie się zaczynał od cyfry.
        /// Algorytm nie zadziała dla niektórych państw, m.in. Malty, Somalii i Montserrat (https://en.wikipedia.org/wiki/List_of_postal_codes).
        /// </summary>
        /// <param name="linia">Ciąg znaków, z których chcemy wyciągnąć kod pocztowy</param>
        /// <returns>Znaleziony kod pocztowy</returns>
        private static string WyciagnijKodPocztowy(string linia)
        {
            string wynik = string.Empty;

            string[] wyrazy = linia.Split();

            string pierwszyWyraz = wyrazy.First();
            Match czyPierwszyZawieraCyfry = Regex.Match(pierwszyWyraz, @"[0-9]", RegexOptions.IgnoreCase);
            if (czyPierwszyZawieraCyfry.Success)
            {
                wynik = pierwszyWyraz;

                // dla kodów 2-wyrazowych (Czechy, Słowacja, Grecja, UK itd.):
                string drugiWyraz = wyrazy.Skip(1).FirstOrDefault(p => !(string.IsNullOrEmpty(p))) ?? string.Empty;
                Match czyDrugiZaczynaSieOdCyfry = Regex.Match(drugiWyraz, @"^\d\w*$");
                if (czyDrugiZaczynaSieOdCyfry.Success)
                {
                    int indeksDrugiego = linia.IndexOf(drugiWyraz, pierwszyWyraz.Length);
                    wynik = linia.Substring(0, indeksDrugiego + drugiWyraz.Length);
                }
            }

            return wynik;
        }

        private static void PrzetworzLinieZKodemIMiejscowosciaZagraniczna(string linia, Adres adres)
        {
            string kod = WyciagnijKodPocztowy(linia);
            if (!(string.IsNullOrEmpty(kod)))
            {
                adres.Szczegoly.KodPocztowy = kod;
                adres.Szczegoly.Miejscowosc = linia.Substring(kod.Length).Trim().Ogranicz(MiejscowoscLength);
            }
            else
            {
                adres.Szczegoly.Miejscowosc = linia;
            }
        }

        private static void PrzetworzLinieZKodemIPocztaZagraniczna(string linia, Adres adres)
        {
            string kod = WyciagnijKodPocztowy(linia);
            if (!string.IsNullOrEmpty(kod))
            {
                adres.Szczegoly.KodPocztowy = kod;
                adres.Szczegoly.Poczta = linia.Substring(kod.Length).Trim().Ogranicz(PocztaLength);
            }
            else
            {
                adres.Szczegoly.Poczta = linia;
            }
        }

        #endregion adres zagraniczny

        #endregion


        public static string ZWieluLiniiDoJednej(Adres adres, string delimeter = ", ")
        {
            if (adres == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder(32);
            sb.Append(adres.Linia1);
            if (!string.IsNullOrEmpty(adres.Linia2))
            {
                if (sb.Length > 0) sb.Append(delimeter);
                sb.Append(adres.Linia2);
            }
            if (!string.IsNullOrEmpty(adres.Linia3))
            {
                if (sb.Length > 0) sb.Append(delimeter);
                sb.Append(adres.Linia3);
            }
            return sb.ToString();
        }

        public static string ZWieluLiniiDoJednej(AdresHistoria adres, string delimeter = ", ")
        {
            if (adres == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder(32);
            sb.Append(adres.Linia1);
            if (!string.IsNullOrEmpty(adres.Linia2))
            {
                if (sb.Length > 0) sb.Append(delimeter);
                sb.Append(adres.Linia2);
            }
            if (!string.IsNullOrEmpty(adres.Linia3))
            {
                if (sb.Length > 0) sb.Append(delimeter);
                sb.Append(adres.Linia3);
            }
            return sb.ToString();
        }

        public static string Ogranicz(this string wartosc, int maxLength)
        {
            if (wartosc.Length <= maxLength)
                return wartosc;
            else
                return wartosc.Substring(0, maxLength);
        }

    }
}
