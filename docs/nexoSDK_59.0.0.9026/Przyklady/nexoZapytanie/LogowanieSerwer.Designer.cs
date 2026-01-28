namespace nexoZapytanie
{
    partial class LogowanieSerwer
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.textSerwer = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textBaza = new System.Windows.Forms.TextBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.labelLogin = new System.Windows.Forms.Label();
            this.textLogin = new System.Windows.Forms.TextBox();
            this.labelHaslo = new System.Windows.Forms.Label();
            this.textHaslo = new System.Windows.Forms.TextBox();
            this.buttonOk = new System.Windows.Forms.Button();
            this.buttonAnuluj = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(45, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "&Serwer:";
            // 
            // textSerwer
            // 
            this.textSerwer.Location = new System.Drawing.Point(112, 13);
            this.textSerwer.Name = "textSerwer";
            this.textSerwer.Size = new System.Drawing.Size(196, 22);
            this.textSerwer.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 39);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "&Baza danych:";
            // 
            // textBaza
            // 
            this.textBaza.Location = new System.Drawing.Point(112, 39);
            this.textBaza.Name = "textBaza";
            this.textBaza.Size = new System.Drawing.Size(196, 22);
            this.textBaza.TabIndex = 4;
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(16, 73);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(144, 17);
            this.checkBox1.TabIndex = 5;
            this.checkBox1.Text = "Autentykacja &Windows";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // labelLogin
            // 
            this.labelLogin.AutoSize = true;
            this.labelLogin.Location = new System.Drawing.Point(13, 106);
            this.labelLogin.Name = "labelLogin";
            this.labelLogin.Size = new System.Drawing.Size(39, 13);
            this.labelLogin.TabIndex = 6;
            this.labelLogin.Text = "&Login:";
            // 
            // textLogin
            // 
            this.textLogin.Location = new System.Drawing.Point(112, 106);
            this.textLogin.Name = "textLogin";
            this.textLogin.Size = new System.Drawing.Size(196, 22);
            this.textLogin.TabIndex = 7;
            // 
            // labelHaslo
            // 
            this.labelHaslo.AutoSize = true;
            this.labelHaslo.Location = new System.Drawing.Point(13, 132);
            this.labelHaslo.Name = "labelHaslo";
            this.labelHaslo.Size = new System.Drawing.Size(39, 13);
            this.labelHaslo.TabIndex = 8;
            this.labelHaslo.Text = "&Hasło:";
            // 
            // textHaslo
            // 
            this.textHaslo.Location = new System.Drawing.Point(112, 132);
            this.textHaslo.Name = "textHaslo";
            this.textHaslo.PasswordChar = '●';
            this.textHaslo.Size = new System.Drawing.Size(196, 22);
            this.textHaslo.TabIndex = 9;
            // 
            // buttonOk
            // 
            this.buttonOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOk.Location = new System.Drawing.Point(152, 173);
            this.buttonOk.Name = "buttonOk";
            this.buttonOk.Size = new System.Drawing.Size(75, 23);
            this.buttonOk.TabIndex = 10;
            this.buttonOk.Text = "OK";
            this.buttonOk.UseVisualStyleBackColor = true;
            // 
            // buttonAnuluj
            // 
            this.buttonAnuluj.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonAnuluj.Location = new System.Drawing.Point(233, 173);
            this.buttonAnuluj.Name = "buttonAnuluj";
            this.buttonAnuluj.Size = new System.Drawing.Size(75, 23);
            this.buttonAnuluj.TabIndex = 11;
            this.buttonAnuluj.Text = "Anuluj";
            this.buttonAnuluj.UseVisualStyleBackColor = true;
            // 
            // LogowanieSerwer
            // 
            this.AcceptButton = this.buttonOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonAnuluj;
            this.ClientSize = new System.Drawing.Size(320, 212);
            this.Controls.Add(this.buttonAnuluj);
            this.Controls.Add(this.buttonOk);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.textHaslo);
            this.Controls.Add(this.labelHaslo);
            this.Controls.Add(this.textLogin);
            this.Controls.Add(this.labelLogin);
            this.Controls.Add(this.textBaza);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textSerwer);
            this.Controls.Add(this.label1);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LogowanieSerwer";
            this.ShowIcon = false;
            this.Text = "Logowanie do SQLServer";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textSerwer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBaza;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.Label labelLogin;
        private System.Windows.Forms.TextBox textLogin;
        private System.Windows.Forms.Label labelHaslo;
        private System.Windows.Forms.TextBox textHaslo;
        private System.Windows.Forms.Button buttonOk;
        private System.Windows.Forms.Button buttonAnuluj;
    }
}