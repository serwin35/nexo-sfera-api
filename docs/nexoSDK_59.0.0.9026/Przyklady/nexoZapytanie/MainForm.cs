using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using InsERT.Moria;
using InsERT.Moria.Sfera;
using System.Diagnostics;
using InsERT.Mox.DatabaseAccess;
using System.Data.Common;

namespace nexoZapytanie
{
    public partial class MainForm : Form
    {
        #region Infrastruktura
        public string StartupFile { get; set; }
        public bool  UruchomionePrzezLauncher { get; set; }

        public MainForm()
        {
            UruchomionePrzezLauncher = false;
            StartupFile = null;
            InitializeComponent();
            MinimumSize = Size;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Polacz();
        }

        Uchwyt sfera;
        #endregion

        //nawiązuje połączenie ze Sferą i podmiotem na podstawie przekazanych danych lub po zapytaniu o nie w okienku logowania
        private void Polacz()
        {
            try
            {
                var mp = new MenedzerPolaczen();
                DanePolaczenia danePolaczenia;
                //przypadek, gdy rozwiązanie zostało uruchomione przez launcher - odbieramy te dane
                if (UruchomionePrzezLauncher)
                {
                    danePolaczenia = DanePolaczenia.Odbierz();
                }
                //przypadek, gdy podano plik konfiguracyjny
                else if (!string.IsNullOrEmpty(StartupFile))
                {
                    danePolaczenia = DanePolaczenia.WczytajZTestowegoPlikuStartowego(StartupFile);
                }
                //przypadek podania jawnego danych do logowania - dane z okienka logowania
                else if (UruchomionePrzezLauncher == false && StartupFile == null)
                {
                    LogowanieSerwer logowanie = new LogowanieSerwer(Program.RegBaseKey);
                    if (logowanie.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                        danePolaczenia = DanePolaczenia.Jawne(logowanie.Serwer, logowanie.Baza, logowanie.AutentykacjaWindows,
                            logowanie.Login, logowanie.Haslo);
                    else
                    {
                        this.Close();
                        return;
                    }
                }
                else
                {
                    Debug.Assert(false, "Nieustawiona opcja uruchamiania");
                    return;
                }
                sfera = mp.Polacz(danePolaczenia, InsERT.Mox.Product.ProductId.Subiekt);
                if (!sfera.ZalogujOperatora("Szef", "robocze"))
                    MessageBox.Show("Nieprawidłowe dane logowania użytkownika nexo.");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, this.Text);
                this.Close();
                return;
            }
        }

        //wykonuje zapytanie i pokazuje wynik
        private void buttonWykonaj_Click(object sender, EventArgs e)
        {
            var dbcFactory = sfera.PodajObiektTypu<IDbConnectionFactory>();
            using (var connection = dbcFactory.CreateConnection(DbConnectionFlags.NoPooling | DbConnectionFlags.NoEnlist))
            {
                string queryString = textZapytanie.Text;

                try
                {
                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = queryString;

                        connection.Open();

                        using (DbDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                WynikListaForm wynikForm = new WynikListaForm(reader);
                                wynikForm.ShowDialog(this);
                            }
                            else if (reader.RecordsAffected >= 0)
                                MessageBox.Show(this, string.Format("Liczba zaktualizowanych rekordów: {0:d}", reader.RecordsAffected));
                            else
                                MessageBox.Show(this, "Polecenie nie zaktualizowało ani nie zwróciło żadnych danych.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, string.Format("Wystąpił problem:\n{0}", ex.Message));
                }
            }
        }
    }
}
