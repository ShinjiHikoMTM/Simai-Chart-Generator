using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SiMaiGenerator
{
    public partial class Form1 : Form
    {
        // UI Controls
        private TextBox txtFilePath;
        private Button btnSelectFile;
        private TextBox txtImagePath;
        private Button btnSelectImage;
        private TextBox txtTitle;
        private TextBox txtArtist;
        private TextBox txtDesigner;

        private NumericUpDown numBpm;
        private CheckBox chkManualBpm;
        private CheckBox chkLinkDiffBpm;

        private CheckBox[] chkDifficulties;
        private NumericUpDown[] numDiffBpms;
        private Button btnGenerate;
        private Button btnSave;
        private RichTextBox rtbOutput;

        private ComboBox cmbLanguage;
        private Label lblLanguage;

        private readonly string[] difficultyNames = { "EASY", "BASIC", "ADVANCED", "EXPERT", "MASTER", "Re:MASTER" };
        private readonly double[] bpmRatios = { 0.5, 0.6, 0.8, 1.0, 1.0, 1.0 };

        private string generatedSimaiContent = "";

        private ComponentResourceManager resources;

        public Form1()
        {
            InitializeComponent();
            InitLanguage();
            CustomInitializeUI();
            ApplyLanguage();
        }

        private void InitLanguage()
        {
            string savedLang = Properties.Settings.Default.UserLanguage;
            if (string.IsNullOrEmpty(savedLang) || savedLang == "auto")
            {
                string sysLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (sysLang == "zh")
                {
                    if (CultureInfo.CurrentUICulture.Name.Contains("CN") || CultureInfo.CurrentUICulture.Name.Contains("Hans"))
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-CN");
                    else
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-TW");
                }
                else if (sysLang == "ja")
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
                }
                else
                {
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture; 
                }
            }
            else
            {

                try
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(savedLang);
                }
                catch
                {
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                }
            }

            resources = new ComponentResourceManager(typeof(Form1));
        }

        private void CustomInitializeUI()
        {

            this.Size = new Size(680, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;


            lblLanguage = new Label() { Location = new Point(460, 10), AutoSize = true };
            this.Controls.Add(lblLanguage);

            cmbLanguage = new ComboBox() { Location = new Point(530, 7), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLanguage.Items.Add(new { Text = "繁體中文", Value = "zh-TW" });
            cmbLanguage.Items.Add(new { Text = "简体中文", Value = "zh-CN" });
            cmbLanguage.Items.Add(new { Text = "English", Value = "en" });
            cmbLanguage.Items.Add(new { Text = "日本語", Value = "ja-JP" });

            cmbLanguage.DisplayMember = "Text";
            cmbLanguage.ValueMember = "Value";

            string currentLang = Thread.CurrentThread.CurrentUICulture.Name;
            if (currentLang.StartsWith("zh-TW") || currentLang.StartsWith("zh-HK")) cmbLanguage.SelectedIndex = 0;
            else if (currentLang.StartsWith("zh-CN") || currentLang.StartsWith("zh-SG")) cmbLanguage.SelectedIndex = 1;
            else if (currentLang.StartsWith("ja")) cmbLanguage.SelectedIndex = 3;
            else cmbLanguage.SelectedIndex = 2;

            cmbLanguage.SelectedIndexChanged += CmbLanguage_SelectedIndexChanged;
            this.Controls.Add(cmbLanguage);


            int yOffset = 40;
            int groupWidth = 620; 


            GroupBox grpFile = new GroupBox() { Name = "grpFile", Location = new Point(20, yOffset), Size = new Size(groupWidth, 60) };
            Label lblMp3 = new Label() { Name = "lblMp3", Location = new Point(15, 25), AutoSize = true };
            txtFilePath = new TextBox() { Location = new Point(110, 22), Width = 400, ReadOnly = true }; // TextBox 加寬
            btnSelectFile = new Button() { Name = "btnSelectFile", Location = new Point(520, 20), Width = 90 }; // Button 往右移且加寬

            btnSelectFile.Click += BtnSelectFile_Click;
            grpFile.Controls.Add(lblMp3);
            grpFile.Controls.Add(txtFilePath);
            grpFile.Controls.Add(btnSelectFile);
            this.Controls.Add(grpFile);

            GroupBox grpImage = new GroupBox() { Name = "grpImage", Location = new Point(20, yOffset + 70), Size = new Size(groupWidth, 60) };
            Label lblImg = new Label() { Name = "lblImg", Location = new Point(15, 25), AutoSize = true };
            txtImagePath = new TextBox() { Location = new Point(110, 22), Width = 400, ReadOnly = true };
            btnSelectImage = new Button() { Name = "btnSelectImage", Location = new Point(520, 20), Width = 90 };

            btnSelectImage.Click += BtnSelectImage_Click;
            grpImage.Controls.Add(lblImg);
            grpImage.Controls.Add(txtImagePath);
            grpImage.Controls.Add(btnSelectImage);
            this.Controls.Add(grpImage);


            GroupBox grpInfo = new GroupBox() { Name = "grpInfo", Location = new Point(20, yOffset + 140), Size = new Size(groupWidth, 100) };
            Label lblTitle = new Label() { Name = "lblTitle", Location = new Point(15, 25), AutoSize = true };
            txtTitle = new TextBox() { Location = new Point(70, 22), Width = 220 }; 

            Label lblBpm = new Label() { Name = "lblBpm", Location = new Point(310, 25), AutoSize = true }; 

            numBpm = new NumericUpDown() { Location = new Point(390, 22), Width = 60, Minimum = 1, Maximum = 999, Value = 120, Enabled = false };
            numBpm.ValueChanged += NumBpm_ValueChanged;

            chkManualBpm = new CheckBox() { Name = "chkManualBpm", Location = new Point(460, 22), AutoSize = true };
            chkManualBpm.CheckedChanged += (s, e) => { numBpm.Enabled = chkManualBpm.Checked; };

            chkLinkDiffBpm = new CheckBox() { Name = "chkLinkDiffBpm", Location = new Point(460, 50), AutoSize = true, Checked = true };
            chkLinkDiffBpm.CheckedChanged += ChkLinkDiffBpm_CheckedChanged;


            Label lblArtist = new Label() { Name = "lblArtist", Location = new Point(15, 60), AutoSize = true };
            txtArtist = new TextBox() { Location = new Point(80, 57), Width = 180 };

            Label lblDes = new Label() { Name = "lblDes", Location = new Point(270, 60), AutoSize = true };
            txtDesigner = new TextBox() { Location = new Point(340, 57), Width = 110, Text = "AutoGen" };

            grpInfo.Controls.Add(lblTitle);
            grpInfo.Controls.Add(txtTitle);
            grpInfo.Controls.Add(lblBpm);
            grpInfo.Controls.Add(numBpm);
            grpInfo.Controls.Add(chkManualBpm);
            grpInfo.Controls.Add(chkLinkDiffBpm);
            grpInfo.Controls.Add(lblArtist);
            grpInfo.Controls.Add(txtArtist);
            grpInfo.Controls.Add(lblDes);
            grpInfo.Controls.Add(txtDesigner);
            this.Controls.Add(grpInfo);


            GroupBox grpDiff = new GroupBox() { Name = "grpDiff", Location = new Point(20, yOffset + 250), Size = new Size(groupWidth, 90) };
            chkDifficulties = new CheckBox[6];
            numDiffBpms = new NumericUpDown[6];

            int startX = 15;
            int stepX = 98;

            for (int i = 0; i < difficultyNames.Length; i++)
            {

                chkDifficulties[i] = new CheckBox() { Text = difficultyNames[i], Location = new Point(startX, 25), Width = 90, AutoSize = false, Checked = true };
                grpDiff.Controls.Add(chkDifficulties[i]);

                Label lblB = new Label() { Name = "lblDiffBpm", Text = "BPM:", Location = new Point(startX, 50), AutoSize = true, Font = new Font(this.Font.FontFamily, 7) };
                grpDiff.Controls.Add(lblB);

                numDiffBpms[i] = new NumericUpDown() { Location = new Point(startX + 35, 48), Width = 45, Minimum = 10, Maximum = 999, Value = 120 };
                grpDiff.Controls.Add(numDiffBpms[i]);

                startX += stepX;
            }
            this.Controls.Add(grpDiff);

            UpdateDifficultyBpms((int)numBpm.Value);

            btnGenerate = new Button() { Name = "btnGenerate", Location = new Point(20, yOffset + 350), Size = new Size(300, 40), Font = new Font("Microsoft JhengHei", 12, FontStyle.Bold), BackColor = Color.LightSkyBlue };
            btnGenerate.Click += BtnGenerate_Click;
            this.Controls.Add(btnGenerate);

            btnSave = new Button() { Name = "btnSave", Location = new Point(340, yOffset + 350), Size = new Size(300, 40), Font = new Font("Microsoft JhengHei", 12, FontStyle.Bold), BackColor = Color.LightGreen };
            btnSave.Click += BtnSave_Click;
            btnSave.Enabled = false;
            this.Controls.Add(btnSave);

            rtbOutput = new RichTextBox() { Location = new Point(20, yOffset + 410), Size = new Size(groupWidth, 310), ReadOnly = true, BackColor = Color.White, Font = new Font("Consolas", 9) };
            this.Controls.Add(rtbOutput);
        }

        private void CmbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbLanguage.SelectedItem == null) return;

            dynamic item = cmbLanguage.SelectedItem;
            string langCode = item.Value;

            if (langCode == "en")
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            else
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(langCode);

            ApplyLanguage();

            Properties.Settings.Default.UserLanguage = langCode;
            Properties.Settings.Default.Save();
        }

        private void ApplyLanguage()
        {
            this.Text = GetString("Title");
            lblLanguage.Text = GetString("LangLabel");

            ((GroupBox)this.Controls["grpFile"]).Text = GetString("GroupAudio");
            ((GroupBox)this.Controls["grpImage"]).Text = GetString("GroupImage");
            ((GroupBox)this.Controls["grpInfo"]).Text = GetString("GroupInfo");
            ((GroupBox)this.Controls["grpDiff"]).Text = GetString("GroupDifficulty");

            ((GroupBox)this.Controls["grpFile"]).Controls["lblMp3"].Text = GetString("LabelAudioPath");
            ((GroupBox)this.Controls["grpImage"]).Controls["lblImg"].Text = GetString("LabelImagePath");

            GroupBox gInfo = (GroupBox)this.Controls["grpInfo"];
            gInfo.Controls["lblTitle"].Text = GetString("LabelSongTitle");
            gInfo.Controls["lblBpm"].Text = GetString("LabelBpm");
            gInfo.Controls["lblArtist"].Text = GetString("LabelArtist");
            gInfo.Controls["lblDes"].Text = GetString("LabelDesigner");
            gInfo.Controls["chkManualBpm"].Text = GetString("CheckManualBpm");
            gInfo.Controls["chkLinkDiffBpm"].Text = GetString("CheckLinkBpm");

            ((GroupBox)this.Controls["grpFile"]).Controls["btnSelectFile"].Text = GetString("ButtonBrowse");
            ((GroupBox)this.Controls["grpImage"]).Controls["btnSelectImage"].Text = GetString("ButtonBrowse");

            ((Button)this.Controls["btnGenerate"]).Text = GetString("ButtonGenerate");
            ((Button)this.Controls["btnSave"]).Text = GetString("ButtonSave");
        }

        private string GetString(string name)
        {
            string str = resources.GetString(name);
            if (string.IsNullOrEmpty(str)) return name; 
            return str;
        }

        private void NumBpm_ValueChanged(object sender, EventArgs e)
        {
            if (chkLinkDiffBpm.Checked)
            {
                UpdateDifficultyBpms((int)numBpm.Value);
            }
        }

        private void ChkLinkDiffBpm_CheckedChanged(object sender, EventArgs e)
        {
            if (chkLinkDiffBpm.Checked)
            {
                UpdateDifficultyBpms((int)numBpm.Value);
            }
        }

        private void UpdateDifficultyBpms(int baseBpm)
        {
            for (int i = 0; i < 6; i++)
            {
                int scaled = (int)(baseBpm * bpmRatios[i]);
                if (scaled < 10) scaled = 10;
                numDiffBpms[i].Value = scaled;
            }
        }

        private void BtnSelectFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "MP3 Files|*.mp3";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtFilePath.Text = ofd.FileName;
                txtTitle.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                Log(string.Format(GetString("LogLoaded"), ofd.FileName));
            }
        }

        private void BtnSelectImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Images|*.jpg;*.png";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtImagePath.Text = ofd.FileName;
                Log(string.Format(GetString("LogLoaded"), ofd.FileName));
            }
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtFilePath.Text)) return;

            rtbOutput.Clear();
            Log(GetString("LogStart"));

            AudioAnalyzer analyzer = new AudioAnalyzer();
            ChartGenerator chartGen = new ChartGenerator();

            try
            {
                Application.DoEvents();
                analyzer.LoadAudio(txtFilePath.Text);

                if (!chkManualBpm.Checked)
                {
                    int autoBpm = analyzer.DetectBPM();
                    numBpm.Value = autoBpm;
                    Log(string.Format(GetString("LogDetectBpm"), autoBpm));
                }
                else
                {
                    Log(string.Format(GetString("LogManualBpm"), numBpm.Value));
                }

                int mainBpm = (int)numBpm.Value;
                double duration = analyzer.TotalSeconds > 0 ? analyzer.TotalSeconds : 120;

                string[] generatedCharts = new string[6];
                string[] calculatedLevels = new string[6];

                for (int i = 0; i < 6; i++)
                {
                    if (chkDifficulties[i].Checked)
                    {
                        int targetDiffBpm = (int)numDiffBpms[i].Value;
                        Log(string.Format(GetString("LogGenerating"), difficultyNames[i], targetDiffBpm));

                        string chart = chartGen.Generate(analyzer, targetDiffBpm, duration, i);
                        generatedCharts[i] = chart;

                        int totalCombo = CalculateTotalCombo(chart);
                        calculatedLevels[i] = CalculateRealLevel(chart, duration, targetDiffBpm, i, totalCombo);

                        Log(string.Format(GetString("LogResult"), difficultyNames[i], calculatedLevels[i], totalCombo));
                    }
                }

                string content = "";
                content += $"&title={txtTitle.Text}\n";
                content += $"&artist={txtArtist.Text}\n";
                content += $"&wholebpm={mainBpm}\n";
                content += "&first=0\n&pv_nome=bg.jpg\n\n";

                for (int i = 0; i < 6; i++)
                {
                    string lv = chkDifficulties[i].Checked ? calculatedLevels[i] : "0";
                    content += $"&lv_{i + 1}={lv}\n";
                }
                content += "\n";

                for (int i = 0; i < 6; i++)
                {
                    content += $"&des_{i + 1}={txtDesigner.Text}\n";
                }
                content += "\n";

                for (int i = 0; i < 6; i++)
                {
                    if (chkDifficulties[i].Checked)
                    {
                        content += $"&inote_{i + 1}=\n{generatedCharts[i]}\n";
                    }
                }

                generatedSimaiContent = content;
                btnSave.Enabled = true;
                Log(GetString("LogDone"));
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
        }

        private int CalculateTotalCombo(string chart)
        {
            string[] rawNotes = chart.Split(new char[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int combo = 0;

            char[] slideShapes = { '-', '^', 'v', '<', '>', 'p', 'q', 's', 'z', 'w', 'V' };

            foreach (var n in rawNotes)
            {
                string t = n.Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith("(") || t.StartsWith("{") || t == "E") continue;

                string[] simultaneous = t.Split('/');
                foreach (var note in simultaneous)
                {
                    if (string.IsNullOrEmpty(note)) continue;
                    if (note.IndexOfAny(slideShapes) >= 0) combo += 2;
                    else combo += 1;
                }
            }
            return combo;
        }

        private string CalculateRealLevel(string chart, double duration, int bpm, int diffIndex, int totalCombo)
        {
            double bpmFactor = 1.0 + ((bpm - 140.0) / 300.0);
            bpmFactor = Math.Max(0.85, Math.Min(1.15, bpmFactor));

            double standardDuration = 120.0;
            double densityCombo = totalCombo * (standardDuration / Math.Max(45.0, duration));

            double staminaBonus = 1.0;
            if (duration > 120)
            {
                staminaBonus = 1.0 + ((duration - 120.0) / 1500.0);
            }

            double weightedCombo = (densityCombo * 0.6) + (totalCombo * 0.4);
            int effectiveCombo = (int)(weightedCombo * bpmFactor * staminaBonus);

            if (diffIndex == 0) // EASY
            {
                if (effectiveCombo <= 100) return "1";
                if (effectiveCombo <= 150) return effectiveCombo < 125 ? "2" : "3";
                if (effectiveCombo <= 200) return "4";
                return "5";
            }

            if (diffIndex == 1) // BASIC
            {
                if (effectiveCombo <= 150) return effectiveCombo < 125 ? "2" : "3";
                if (effectiveCombo <= 250) return effectiveCombo < 200 ? "4" : "5";
                if (effectiveCombo <= 350) return effectiveCombo < 300 ? "6" : "7";
                return "7+";
            }

            if (diffIndex == 2) // ADVANCED
            {
                if (effectiveCombo <= 350) return effectiveCombo < 275 ? "7" : "8";
                if (effectiveCombo <= 500) return effectiveCombo < 425 ? "9" : "10";
                if (effectiveCombo <= 600) return "10+";
                return "11";
            }

            if (diffIndex == 3) // EXPERT
            {
                if (effectiveCombo <= 300) return "8";
                if (effectiveCombo <= 500) return effectiveCombo < 400 ? "9" : "10";
                if (effectiveCombo <= 700)
                {
                    if (effectiveCombo < 600) return "11";
                    return "12";
                }
                if (effectiveCombo <= 900)
                {
                    if (effectiveCombo < 800) return "12+";
                    return "13";
                }
                return "13+";
            }

            if (diffIndex == 4) // MASTER
            {
                if (effectiveCombo <= 400) return "11+";
                if (effectiveCombo <= 600)
                {
                    if (effectiveCombo < 500) return "12";
                    return "12+";
                }
                if (effectiveCombo <= 800)
                {
                    if (effectiveCombo < 700) return "13";
                    return "13+";
                }
                if (effectiveCombo < 900) return "14";
                return "14+";
            }

            if (diffIndex == 5) // Re:MASTER
            {
                if (effectiveCombo <= 500) return "13";
                if (effectiveCombo <= 700)
                {
                    if (effectiveCombo < 600) return "13+";
                    return "14";
                }
                if (effectiveCombo < 850) return "14+";
                return "15";
            }

            return "0";
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(generatedSimaiContent)) return;

            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string safeTitle = string.Join("_", txtTitle.Text.Split(Path.GetInvalidFileNameChars()));
                    if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "AutoSimai_Song";

                    string targetFolder = Path.Combine(fbd.SelectedPath, safeTitle);
                    if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

                    Log(string.Format(GetString("LogSaving"), targetFolder));

                    File.Copy(txtFilePath.Text, Path.Combine(targetFolder, "track.mp3"), true);

                    if (!string.IsNullOrEmpty(txtImagePath.Text) && File.Exists(txtImagePath.Text))
                    {
                        File.Copy(txtImagePath.Text, Path.Combine(targetFolder, "bg.jpg"), true);
                    }

                    File.WriteAllText(Path.Combine(targetFolder, "maidata.txt"), generatedSimaiContent, Encoding.UTF8);

                    MessageBox.Show(GetString("MsgSuccess"), GetString("Title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    System.Diagnostics.Process.Start("explorer.exe", targetFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(GetString("MsgSaveError"), ex.Message));
                }
            }
        }

        private void Log(string msg)
        {
            rtbOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            rtbOutput.ScrollToCaret();
        }
    }
}