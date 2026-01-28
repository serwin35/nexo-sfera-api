using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace nexoZapytanie
{
    public partial class LogowanieSerwer : Form
    {
        public LogowanieSerwer(string regBaseKey)
        {
            InitializeComponent();

            string key = regBaseKey;
            string haslo = System.Reflection.Assembly.GetExecutingAssembly().FullName;

            try
            {
                textSerwer.Text = (string)Registry.GetValue(key, "Serwer", "(local)");
                textBaza.Text = (string)Registry.GetValue(key, "Baza", "");
                checkBox1.Checked = (int?)Registry.GetValue(key, "AutentykacjaWindows", 1) != 0;

                textLogin.Text = Szyfrowanie.Odszyfruj((byte[])Registry.GetValue(key, "Login", new byte[0]), haslo);
                textHaslo.Text = Szyfrowanie.Odszyfruj((byte[])Registry.GetValue(key, "Haslo", new byte[0]), haslo);
            }
            catch (Exception e)
            { }

            this.FormClosing += (o, e) =>
            {
                try
                {
                    Registry.SetValue(key, "Serwer", textSerwer.Text);
                    Registry.SetValue(key, "Baza", textBaza.Text);
                    Registry.SetValue(key, "AutentykacjaWindows", checkBox1.Checked ? 1 : 0);
                    Registry.SetValue(key, "Login", Szyfrowanie.Zaszyfruj(textLogin.Text, haslo));
                    Registry.SetValue(key, "Haslo", Szyfrowanie.Zaszyfruj(textHaslo.Text, haslo));
                }
                catch (Exception ex) 
                { }
            };

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            labelHaslo.Enabled =
                labelLogin.Enabled =
                textLogin.Enabled =
                textHaslo.Enabled = !checkBox1.Checked;
        }

        public string Serwer { get { return textSerwer.Text; } }
        public string Baza { get { return textBaza.Text; } }
        public bool AutentykacjaWindows { get { return checkBox1.Checked; } }
        public string Login { get { return textLogin.Text; } }
        public string Haslo { get { return textHaslo.Text; } }
    }

}
