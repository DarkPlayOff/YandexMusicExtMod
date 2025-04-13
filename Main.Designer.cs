namespace YandexMusicPatcherGui
{
    partial class Main
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            label1 = new Label();
            groupBoxMods = new GroupBox();
            tableLayoutPanel1 = new TableLayoutPanel();
            airCheckBox2 = new ReaLTaiizor.Controls.AirCheckBox();
            airCheckBox1 = new ReaLTaiizor.Controls.AirCheckBox();
            airCheckBox5 = new ReaLTaiizor.Controls.AirCheckBox();
            button1 = new ReaLTaiizor.Controls.Button();
            button2 = new ReaLTaiizor.Controls.Button();
            button3 = new ReaLTaiizor.Controls.Button();
            richTextBox1 = new RichTextBox();
            groupBoxMods.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold);
            label1.Location = new Point(15, 1);
            label1.Name = "label1";
            label1.Size = new Size(117, 17);
            label1.TabIndex = 2;
            label1.Text = "Доступные моды";
            // 
            // groupBoxMods
            // 
            groupBoxMods.Controls.Add(tableLayoutPanel1);
            groupBoxMods.Controls.Add(label1);
            groupBoxMods.Location = new Point(19, 10);
            groupBoxMods.Margin = new Padding(0);
            groupBoxMods.Name = "groupBoxMods";
            groupBoxMods.Padding = new Padding(2);
            groupBoxMods.Size = new Size(307, 138);
            groupBoxMods.TabIndex = 3;
            groupBoxMods.TabStop = false;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(airCheckBox2, 0, 2);
            tableLayoutPanel1.Controls.Add(airCheckBox1, 0, 0);
            tableLayoutPanel1.Controls.Add(airCheckBox5, 0, 4);
            tableLayoutPanel1.Location = new Point(12, 32);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 6;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666666F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666666F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666666F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666666F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666666F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666666F));
            tableLayoutPanel1.Size = new Size(284, 83);
            tableLayoutPanel1.TabIndex = 13;
            // 
            // airCheckBox2
            // 
            airCheckBox2.AccessibleDescription = "Отключает аналитику и лишнюю слежку. Не влияет на производительность";
            airCheckBox2.Checked = false;
            airCheckBox2.Customization = "7e3t//Ly8v/r6+v/5ubm/+vr6//f39//p6en/zw8PP8=";
            airCheckBox2.Dock = DockStyle.Fill;
            airCheckBox2.Font = new Font("Segoe UI", 9.75F);
            airCheckBox2.Image = null;
            airCheckBox2.Location = new Point(3, 29);
            airCheckBox2.Name = "airCheckBox2";
            airCheckBox2.NoRounding = false;
            airCheckBox2.Size = new Size(278, 17);
            airCheckBox2.TabIndex = 13;
            airCheckBox2.Tag = "disableTracking";
            airCheckBox2.Text = "Отключить аналитику";
            airCheckBox2.Transparent = false;
            // 
            // airCheckBox1
            // 
            airCheckBox1.AccessibleDescription = "Позволяет пользоваться программой без подписки Яндекс Плюс";
            airCheckBox1.BackColor = Color.WhiteSmoke;
            airCheckBox1.Checked = false;
            airCheckBox1.Customization = "7e3t//Ly8v/r6+v/5ubm/+vr6//f39//p6en/zw8PP8=";
            airCheckBox1.Dock = DockStyle.Fill;
            airCheckBox1.Font = new Font("Segoe UI", 9.75F);
            airCheckBox1.Image = null;
            airCheckBox1.Location = new Point(3, 3);
            airCheckBox1.Name = "airCheckBox1";
            airCheckBox1.NoRounding = false;
            airCheckBox1.Size = new Size(278, 17);
            airCheckBox1.TabIndex = 12;
            airCheckBox1.Tag = "usePlusUnlocker";
            airCheckBox1.Text = "Активировать Яндекс Плюс";
            airCheckBox1.Transparent = false;
            // 
            // airCheckBox5
            // 
            airCheckBox5.AccessibleDescription = "Меняет все шрифты в приложении на моноширинные";
            airCheckBox5.Checked = false;
            airCheckBox5.Customization = "7e3t//Ly8v/r6+v/5ubm/+vr6//f39//p6en/zw8PP8=";
            airCheckBox5.Dock = DockStyle.Fill;
            airCheckBox5.Font = new Font("Segoe UI", 9.75F);
            airCheckBox5.Image = null;
            airCheckBox5.Location = new Point(3, 55);
            airCheckBox5.Name = "airCheckBox5";
            airCheckBox5.NoRounding = false;
            airCheckBox5.Size = new Size(278, 17);
            airCheckBox5.TabIndex = 16;
            airCheckBox5.Tag = "useJetBrainsFont";
            airCheckBox5.Text = "Заменить шрифт на JetBrains Mono";
            airCheckBox5.Transparent = false;
            // 
            // button1
            // 
            button1.BackColor = Color.Transparent;
            button1.BorderColor = Color.FromArgb(63, 104, 249);
            button1.EnteredBorderColor = Color.FromArgb(37, 84, 248);
            button1.EnteredColor = Color.FromArgb(37, 84, 248);
            button1.Font = new Font("Segoe UI", 9.75F);
            button1.Image = null;
            button1.ImageAlign = ContentAlignment.MiddleLeft;
            button1.InactiveColor = Color.FromArgb(63, 104, 249);
            button1.Location = new Point(19, 151);
            button1.Name = "button1";
            button1.PressedBorderColor = Color.FromArgb(13, 64, 247);
            button1.PressedColor = Color.FromArgb(13, 64, 247);
            button1.Size = new Size(121, 32);
            button1.TabIndex = 11;
            button1.Text = "Установить мод";
            button1.TextAlignment = StringAlignment.Center;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.BackColor = Color.Transparent;
            button2.BorderColor = Color.FromArgb(215, 215, 218);
            button2.Enabled = false;
            button2.EnteredBorderColor = Color.FromArgb(37, 84, 248);
            button2.EnteredColor = Color.FromArgb(37, 84, 248);
            button2.Font = new Font("Segoe UI", 9.75F);
            button2.Image = null;
            button2.ImageAlign = ContentAlignment.MiddleLeft;
            button2.InactiveColor = Color.FromArgb(215, 215, 218);
            button2.Location = new Point(144, 151);
            button2.Name = "button2";
            button2.PressedBorderColor = Color.FromArgb(13, 64, 247);
            button2.PressedColor = Color.FromArgb(13, 64, 247);
            button2.Size = new Size(180, 32);
            button2.TabIndex = 11;
            button2.Text = "Запустить Яндекс Музыку";
            button2.TextAlignment = StringAlignment.Center;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.BackColor = Color.Transparent;
            button3.BackgroundImageLayout = ImageLayout.Zoom;
            button3.BorderColor = Color.FromArgb(30, 30, 30);
            button3.CausesValidation = false;
            button3.EnteredBorderColor = Color.FromArgb(45, 45, 45);
            button3.EnteredColor = Color.FromArgb(45, 45, 45);
            button3.Font = new Font("Segoe UI", 9.75F);
            button3.Image = (Image)resources.GetObject("button3.Image");
            button3.ImageAlign = ContentAlignment.MiddleLeft;
            button3.ImeMode = ImeMode.Alpha;
            button3.InactiveColor = Color.FromArgb(30, 30, 30);
            button3.Location = new Point(12, 286);
            button3.Name = "button3";
            button3.PressedBorderColor = Color.FromArgb(15, 15, 15);
            button3.PressedColor = Color.FromArgb(15, 15, 15);
            button3.Size = new Size(118, 28);
            button3.TabIndex = 19;
            button3.Text = "Сообщить об ошибке  ";
            button3.TextAlignment = StringAlignment.Far;
            button3.Click += button3_Click;
            // 
            // richTextBox1
            // 
            richTextBox1.BackColor = SystemColors.Control;
            richTextBox1.BorderStyle = BorderStyle.None;
            richTextBox1.Font = new Font("Segoe UI", 9F);
            richTextBox1.ForeColor = SystemColors.WindowFrame;
            richTextBox1.Location = new Point(19, 189);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.ReadOnly = true;
            richTextBox1.Size = new Size(307, 91);
            richTextBox1.TabIndex = 20;
            richTextBox1.Text = "";
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(344, 341);
            Controls.Add(richTextBox1);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(button1);
            Controls.Add(groupBoxMods);
            Font = new Font("Segoe UI", 9.75F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Main";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Yandex Music Patcher";
            groupBoxMods.ResumeLayout(false);
            groupBoxMods.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private Label label1;
        private GroupBox groupBoxMods;
        private ReaLTaiizor.Controls.Button button1;
        private ReaLTaiizor.Controls.Button button2;
        private ReaLTaiizor.Controls.AirCheckBox airCheckBox1;
        private TableLayoutPanel tableLayoutPanel1;
        private ReaLTaiizor.Controls.AirCheckBox airCheckBox5;
        private ReaLTaiizor.Controls.AirCheckBox airCheckBox2;
        private ReaLTaiizor.Controls.Button button3;
        private RichTextBox richTextBox1;
    }
}