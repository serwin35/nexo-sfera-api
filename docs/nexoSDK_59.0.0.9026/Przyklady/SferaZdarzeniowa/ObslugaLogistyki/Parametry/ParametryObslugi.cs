using ObslugaLogistyki.Properties;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ObslugaLogistyki.Parametry
{
    public class ParametryObslugi
    {
        private static readonly string Key = "key";
        private static readonly string Value = "value";
        public TypObslugiwanegoDokumentu WymagajSposobuDostawy { get; set; }
        public TypObslugiwanegoDokumentu SprawdzajWymagalnoscSamochoduDostawczego { get; set; }
        public TypObslugiwanegoDokumentu ObslugujRealizacje => TypObslugiwanegoDokumentu.Ds | TypObslugiwanegoDokumentu.Wz;
        public TypObslugiwanejRealizacji PrzepisujSamochodDostawczyPrzyRealizacji { get; set; }
        public TypObslugiwanegoDokumentu AutomatycznieUstawiajSamochodDostawczy { get; set; }
        public TypObslugiwanegoDokumentu RezerwujSamochodDostawczy { get; set; }
        public TypObslugiwanegoDokumentu SprawdzajOplacalnoscTransportuFirmowego { get; set; }
        /// <summary>
        /// W walucie PLN.
        /// </summary>
        public decimal MinimalnaKwotaDlaTransportuFirmowego { get; set; }
        /// <summary>
        /// W kilogramach.
        /// </summary>
        public decimal MinimalnaMasaDlaTransportuFirmowego { get; set; }
        public bool RezerwujSamochodDostawczyNaWz => RezerwujSamochodDostawczy.HasFlag(TypObslugiwanegoDokumentu.Wz);
        public bool RezerwujSamochodDostawczyNaDs => RezerwujSamochodDostawczy.HasFlag(TypObslugiwanegoDokumentu.Ds);

        private ParametryObslugi()
        {
            WymagajSposobuDostawy = TypObslugiwanegoDokumentu.Zk | TypObslugiwanegoDokumentu.Wz | TypObslugiwanegoDokumentu.Ds;
            SprawdzajWymagalnoscSamochoduDostawczego = TypObslugiwanegoDokumentu.Wz | TypObslugiwanegoDokumentu.Ds;
            PrzepisujSamochodDostawczyPrzyRealizacji = TypObslugiwanejRealizacji.ZkWz | TypObslugiwanejRealizacji.ZkDs | TypObslugiwanejRealizacji.WzDs;
            AutomatycznieUstawiajSamochodDostawczy =  TypObslugiwanegoDokumentu.Ds | TypObslugiwanegoDokumentu.Wz | TypObslugiwanegoDokumentu.Zk;
            RezerwujSamochodDostawczy = TypObslugiwanegoDokumentu.Ds | TypObslugiwanegoDokumentu.Wz;
            SprawdzajOplacalnoscTransportuFirmowego = TypObslugiwanegoDokumentu.Zk | TypObslugiwanegoDokumentu.Wz | TypObslugiwanegoDokumentu.Ds;
            MinimalnaKwotaDlaTransportuFirmowego = 500m; // PLN
            MinimalnaMasaDlaTransportuFirmowego = 3000m; // kg
        }

        private static ParametryObslugi _instance;
        public static ParametryObslugi Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ParametryObslugi();
                    _instance.WypelnijNaPodstawiePlikuKonfiguracyjnego();
                }
                return _instance;
            }
        }

        private void WypelnijNaPodstawiePlikuKonfiguracyjnego()
        {
            string configFileName = "Logistyka.config";
            string configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                , "InsERT"
                , "Logistyka");
            if (Directory.Exists(configPath))
            {
                string configFilePath = Path.Combine(configPath, configFileName);
                if (File.Exists(configFilePath))
                {
                    Type parametersType = GetType();
                    using (FileStream configFileStream = new FileStream(configFilePath, FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader configFileReader = new StreamReader(configFileStream))
                        {
                            string line;
                            while ((line = configFileReader.ReadLine()) != null)
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    Match match = Regex.Match(line.Trim(), Resources.ConfigLineRegex);
                                    if (match.Success)
                                    {
                                        string key = match.Groups[Key].Value;
                                        string value = match.Groups[Value].Value;
                                        PropertyInfo keyProperty = parametersType.GetProperty(key, BindingFlags.Instance | BindingFlags.Public);
                                        if (keyProperty != null && int.TryParse(value, out int result))
                                        {
                                            if (keyProperty.PropertyType == typeof(bool))
                                                keyProperty.SetValue(this, result > 0);
                                            else if (keyProperty.PropertyType == typeof(decimal))
                                                keyProperty.SetValue(this, (decimal)result);
                                            else if (keyProperty.PropertyType == typeof(TypObslugiwanegoDokumentu))
                                                keyProperty.SetValue(this, (TypObslugiwanegoDokumentu)Konwertuj<TypObslugiwanegoDokumentu>(result));
                                            else if (keyProperty.PropertyType == typeof(TypObslugiwanejRealizacji))
                                                keyProperty.SetValue(this, (TypObslugiwanejRealizacji)Konwertuj<TypObslugiwanejRealizacji>(result));
                                            else
                                                throw new InvalidOperationException("Niepoprawny typ parametru.");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private int Konwertuj<TEnum>(int wartosc)
            where TEnum : Enum
        {
            int konwersja = 0;
            foreach (TEnum enumValue in Enum.GetValues(typeof(TEnum)).OfType<TEnum>().ToArray())
                if ((wartosc & Convert.ToInt32(enumValue)) > 0)
                    konwersja |= Convert.ToInt32(enumValue);
            return konwersja;
        }

    }
}
