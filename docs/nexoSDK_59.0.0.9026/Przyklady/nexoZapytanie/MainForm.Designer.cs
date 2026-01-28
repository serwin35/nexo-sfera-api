namespace nexoZapytanie
{
    partial class MainForm
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
            this.textZapytanie = new nexoZapytanie.SyntaxRichTextBox();
            this.buttonWykonaj = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textZapytanie
            // 
            this.textZapytanie.AcceptsTab = true;
            this.textZapytanie.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textZapytanie.CaseSensitive = false;
            this.textZapytanie.Location = new System.Drawing.Point(12, 12);
            this.textZapytanie.Name = "textZapytanie";
            this.textZapytanie.Size = new System.Drawing.Size(491, 354);
            this.textZapytanie.TabIndex = 0;
            this.textZapytanie.Text = "SELECT Id, NazwaSkrocona FROM ModelDanychContainer.Podmioty;";
            // 
            // buttonWykonaj
            // 
            this.buttonWykonaj.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonWykonaj.Location = new System.Drawing.Point(428, 372);
            this.buttonWykonaj.Name = "buttonWykonaj";
            this.buttonWykonaj.Size = new System.Drawing.Size(75, 23);
            this.buttonWykonaj.TabIndex = 1;
            this.buttonWykonaj.Text = "Wykonaj";
            this.buttonWykonaj.UseVisualStyleBackColor = true;
            this.buttonWykonaj.Click += new System.EventHandler(this.buttonWykonaj_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(515, 407);
            this.Controls.Add(this.buttonWykonaj);
            this.Controls.Add(this.textZapytanie);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.Name = "MainForm";
            this.Text = "nexo Zapytanie";
            this.ResumeLayout(false);

        }

        #endregion

        private SyntaxRichTextBox textZapytanie;
        private System.Windows.Forms.Button buttonWykonaj;
    }
}

