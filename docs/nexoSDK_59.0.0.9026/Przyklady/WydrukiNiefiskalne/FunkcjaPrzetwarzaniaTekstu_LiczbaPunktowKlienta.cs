using InsERT.Moria.ModelDanych;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Wydruki.Autoteksty;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexoPlugins
{
    public class FunkcjaPrzetwarzaniaTekstu_LiczbaPunktowKlienta : IFunkcjaPrzetwarzaniaTekstu
    {
        private readonly Guid _id;

        public FunkcjaPrzetwarzaniaTekstu_LiczbaPunktowKlienta()
        {
            _id = new Guid("F2E30F0F-1065-43AA-B3CA-44EF454243D0");
        }

        public string PrzykladUzycia => "liczba punktow[etykieta]";

        public Guid Identyfikator => _id;

        public string Nazwa => "liczba punktów";

        public string Opis => "Funkcja wyznaczająca liczbę punktów klienta na dokumencie.";

        public IDostawcaPluginow Dostawca => new DostawcaPluginow();

        public string Przetworz(string tekstWejsciowy, object obiektPrzetwarzany)
        {
            string liczbaPunktow = string.Empty;
            if (obiektPrzetwarzany is Dokument dokument
                && dokument.Podmiot != null)
            {
                SqlConnection connection = null;
                SqlCommand command = null;
                SqlDataReader reader = null;
                try
                {
                    string connectionString = string.Empty;
                    string connectionStringFile = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "InsERT", "nexo", "FunkcjaPrzetwarzaniaTekstu_PunktyKlienta_ConnectionString.txt");
                    using (FileStream fStream = new FileStream(connectionStringFile, FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader fReader = new StreamReader(fStream))
                        {
                            connectionString = fReader.ReadToEnd();
                        }
                    }
                    connection = new SqlConnection(connectionString);
                    connection.Open();
                    command = connection.CreateCommand();
                    command.CommandText = @"SELECT LiczbaPunktow FROM PunktyKlientow WHERE PodmiotId = @podmiotId";
                    command.Parameters.AddWithValue("@podmiotId", dokument.Podmiot.Id);
                    reader = command.ExecuteReader();
                    try
                    {
                        if (reader.Read())
                        {
                            int? liczbaPunktowInt = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                            if (liczbaPunktowInt.HasValue)
                                liczbaPunktow = liczbaPunktowInt.Value.ToString();
                        }
                    }
                    finally
                    {
                        reader.Close();
                    }
                }
                catch (Exception)
                {
                    // zaloguj wyjątek...
                }
                finally
                {
                    if (command != null)
                        command.Dispose();
                    if (connection != null)
                    {
                        connection.Close();
                        connection.Dispose();
                    }
                }

            }
            return string.IsNullOrEmpty(liczbaPunktow) ? string.Empty : string.Format("{0} {1}", tekstWejsciowy, liczbaPunktow);
        }
    }
}
