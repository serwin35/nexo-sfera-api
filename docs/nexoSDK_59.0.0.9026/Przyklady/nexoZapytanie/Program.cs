using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace nexoZapytanie
{
    static class Program
    {

        [STAThread]
        static void Main(string[] argv)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MainForm form = new MainForm();
            foreach (string arg in argv)
            {
                if (string.Compare(arg, "/UruchomionePrzezInsLauncher", true) == 0)
                {
                    form.UruchomionePrzezLauncher = true;
                    break;
                }
                else if (arg.StartsWith("/LauncherMessageFile=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] tokens = arg.Split('=');
                    if (tokens.Length > 1 && !string.IsNullOrEmpty(tokens[1]))
                    {
                        form.StartupFile = tokens[1];
                    }
                    else
                        MessageBox.Show("Niepoprawny parametr LauncherMessageFile");
                    break;
                }
            }

            Application.Run(form);
        }

        public static readonly string RegBaseKey = @"HKEY_CURRENT_USER\Software\InsERT\nexoZapytanie";
    }
}
