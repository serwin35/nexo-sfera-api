using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace nexoZapytanie
{
    public partial class WynikListaForm : Form
    {
        public WynikListaForm(IDataReader reader)
        {
            InitializeComponent();
            DataTable table = new DataTable();
            table.Load(reader);
            dataGrid.DataSource = table;
            label1.Text = string.Format("Liczba rekordów: {0:d}", table.Rows.Count);
        }

        private void WynikListaForm_Load(object sender, EventArgs e)
        {

        }
    }
}
