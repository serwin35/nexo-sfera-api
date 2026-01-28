using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace exportExcel2XML
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            RodzajDanych = rdKlienci;
            DataContext = this;
        }

        private const string rdKlienci = "klienci";
        private const string rdTowary = "towary";
        private const string rdStany = "stany towarów";
        private const string rdRozrachunki = "rozrachunki";

        private string _PlikExcela;
        public string PlikExcela
        {
            get { return _PlikExcela; }
            set
            {
                if (_PlikExcela != value)
                {
                    _PlikExcela = value;
                    OnPropertyChanged("PlikExcela");
                    string nazwa = System.IO.Path.GetFileNameWithoutExtension(PlikExcela).ToLower();
                    if (nazwa.Contains("klien") || nazwa.Contains("kontrahe"))
                        RodzajDanych = rdKlienci;
                    else if (nazwa.Contains("towar") || nazwa.Contains("asort"))
                        RodzajDanych = rdTowary;
                    else if (nazwa.Contains("stan") || nazwa.Contains("inwent"))
                        RodzajDanych = rdStany;
                    else if (nazwa.Contains("rozrach") || nazwa.Contains("nierozlicz"))
                        RodzajDanych = rdRozrachunki;
                }
            }
        }

        private string _RodzajDanych;
        public string RodzajDanych
        {
            get
            { return _RodzajDanych; }
            set
            {
                if (_RodzajDanych != value)
                {
                    _RodzajDanych = value;
                    OnPropertyChanged("RodzajDanych");
                }
            }
        }
        public IEnumerable<string> RodzajeDanych
        {
            get
            {
                yield return rdKlienci;
                yield return rdTowary;
                yield return rdStany;
                yield return rdRozrachunki;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string outputFilenames = null;
                XlsxExporter exp = new XlsxExporter();
                switch (RodzajDanych)
                {
                    case rdKlienci:
                        outputFilenames = exp.ExportKlienci(PlikExcela).Aggregate((p, n) => p += "\n" + n);
                        break;
                    case rdTowary:
                        outputFilenames = exp.ExportujTowary(PlikExcela).Aggregate((p, n) => p += "\n" + n);
                        break;
                    case rdStany:
                        outputFilenames = exp.ExportStanyTowarow(PlikExcela).Aggregate((p, n) => p += "\n" + n);
                        break;
                    case rdRozrachunki:
                        outputFilenames = exp.ExportRozrachunki(PlikExcela).Aggregate((p, n) => p += "\n" + n);
                        break;
                }
                if (!string.IsNullOrWhiteSpace(outputFilenames))
                    MessageBox.Show(this, string.Format("Wygenerowano pliki:\n\n{0}", outputFilenames), this.Title);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format("W trakcie eksportu wystąpił błąd:\n\n{0}\n\n{1}",
                    ex.Message, ex.InnerException != null ? ex.InnerException.Message : string.Empty), this.Title);
            }
        }

        private void Wybierz_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = true;
            openFileDialog.Filter = "Pliki Excela (*.xlsx)|*.xlsx";
            openFileDialog.Title = "Wybierz plik";
            openFileDialog.FileName = PlikExcela;

            if (openFileDialog.ShowDialog() == true)
                PlikExcela = openFileDialog.FileName;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
