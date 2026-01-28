using InsERT.Moria.PolaWlasne2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace ObslugaLogistyki.UI
{
    /// <summary>
    /// Interaction logic for WyborSamochoduDostawczego.xaml
    /// </summary>
    public partial class WyborSamochoduDostawczego : INotifyPropertyChanged
    {
        private ElementSlownikowegoZrodlaDanych<int> _item;

        public event PropertyChangedEventHandler PropertyChanged;

        public WyborSamochoduDostawczego(List<ElementSlownikowegoZrodlaDanych<int>> dataSource, string nazwaSposobuDostawy)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));
            if (dataSource.Count == 0)
                throw new ArgumentException("Źródło danych nie może być puste.", nameof(dataSource));
            if (string.IsNullOrEmpty(nazwaSposobuDostawy))
                throw new ArgumentException("Nazwa sposobu dostawy nie może być pusta.", nameof(nazwaSposobuDostawy));
            DataSource = dataSource;
            NazwaSposobuDostawy = nazwaSposobuDostawy;
            InitializeComponent();
        }

        public string NazwaSposobuDostawy { get; }

        public List<ElementSlownikowegoZrodlaDanych<int>> DataSource { get; }

        public ElementSlownikowegoZrodlaDanych<int> Item
        {
            get => _item ?? (_item = DataSource.FirstOrDefault());
            set
            {
                _item = value;
                OnPropertyChanged(nameof(Item));
            }
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void OnOkClick(object sender, RoutedEventArgs _)
        {
            DialogResult = true;
            Close();
        }
    }
}
