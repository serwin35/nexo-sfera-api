using System;
using System.Windows;

namespace ObslugaLogistyki.UI
{
    /// <summary>
    /// Interaction logic for WyborSamochoduDostawczego.xaml
    /// </summary>
    public partial class Komunikat
    {
        public Komunikat(string komunikat)
        {
            KomunikatBledu = string.IsNullOrEmpty(komunikat) ? "Wystąpił nieoczekiwany błąd." : komunikat;
            InitializeComponent();
        }

        public string KomunikatBledu { get; }

        private void OnOkClick(object sender, RoutedEventArgs _)
        {
            DialogResult = true;
            Close();
        }
    }
}
