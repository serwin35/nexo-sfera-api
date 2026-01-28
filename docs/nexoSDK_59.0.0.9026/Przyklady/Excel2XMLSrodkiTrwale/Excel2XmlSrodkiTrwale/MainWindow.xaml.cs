using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace Excel2XmlSrodkiTrwale
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            RodzajDanych = rdSrodkiTrwale;
            DataContext = this;
        }

        private const string rdSrodkiTrwale = "środki trwałe";
        private const string rdOperacjeST = "operacje ST";
        private const string rdSrodkiTrwaleIOperacjeST = "środki trwałe i operacje ST";

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
                    if (nazwa.Contains("środki") || nazwa.Contains("trwa"))
                        RodzajDanych = rdSrodkiTrwale;
                    else if (nazwa.Contains("operac") || nazwa.Contains("ST"))
                        RodzajDanych = rdOperacjeST;
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
                yield return rdSrodkiTrwale;
                yield return rdOperacjeST;
                yield return rdSrodkiTrwaleIOperacjeST;
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
                    case rdSrodkiTrwale:
                        outputFilenames = exp.ExportSrodkiTrwale(PlikExcela).Aggregate((p, n) => p += "\n" + n);
                        break;
                    case rdOperacjeST:
                        outputFilenames = exp.ExportOperacjeST(PlikExcela).Aggregate((p, n) => p += "\n" + n);
                        outputFilenames = outputFilenames + "\n" + exp.ExportMiejsceUzytkowania(PlikExcela).Aggregate((p, n) => p += "\n" + n);
                        break;
                    case rdSrodkiTrwaleIOperacjeST:
                        outputFilenames = exp.ExportSrodkiTrwaleIOperacjeST(PlikExcela).Aggregate((p, n) => p += "\n" + n);
                        outputFilenames = outputFilenames + "\n" + exp.ExportMiejsceUzytkowania(PlikExcela).Aggregate((p, n) => p += "\n" + n);
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
