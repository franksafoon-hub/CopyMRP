using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CopyMRP
{
    public partial class Form1 : Form
    {
        // ── State ──────────────────────────────────────
        private List<BuEntry> buList = new List<BuEntry>();
        private bool copying = false;

        public Form1()
        {
            InitializeComponent();
            this.Text = "Copy MRP";
            this.BackColor = Color.FromArgb(26, 26, 46);
            this.ForeColor = Color.FromArgb(224, 224, 224);
            this.MinimumSize = new Size(700, 580);
            RefreshBuList();
            UpdateCopyBtn();
        }

        // ── Destination ─────────────────────────────────
        private void btnPickDest_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "เลือก Folder ปลายทาง";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = dlg.SelectedPath;
                    Log("info", "Destination: " + dlg.SelectedPath);
                    UpdateCopyBtn();
                }
            }
        }

        // ── BU Management ───────────────────────────────
        private void btnAddBU_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = $"เลือก BU {buList.Count + 1} Folder";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    buList.Add(new BuEntry
                    {
                        Label = $"BU{buList.Count + 1}",
                        Path  = dlg.SelectedPath
                    });
                    RefreshBuList();
                    Log("info", $"BU{buList.Count} → {dlg.SelectedPath}");
                    UpdateCopyBtn();
                }
            }
        }

        private void btnChangeBU_Click(object sender, EventArgs e)
        {
            if (listBU.SelectedIndices.Count == 0) { MessageBox.Show("กรุณาเลือก BU ก่อน"); return; }
            int idx = listBU.SelectedIndices[0];
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = $"เปลี่ยน {buList[idx].Label} Folder";
                dlg.SelectedPath = buList[idx].Path;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    buList[idx].Path = dlg.SelectedPath;
                    RefreshBuList();
                    Log("info", $"{buList[idx].Label} → {dlg.SelectedPath}");
                }
            }
        }

        private void btnRemoveBU_Click(object sender, EventArgs e)
        {
            if (listBU.SelectedIndices.Count == 0) { MessageBox.Show("กรุณาเลือก BU ก่อน"); return; }
            int idx = listBU.SelectedIndices[0];
            buList.RemoveAt(idx);
            for (int i = 0; i < buList.Count; i++) buList[i].Label = $"BU{i + 1}";
            RefreshBuList();
            UpdateCopyBtn();
        }

        private void RefreshBuList()
        {
            listBU.Items.Clear();
            foreach (var bu in buList)
            {
                var item = new ListViewItem(bu.Label);
                item.SubItems.Add(bu.Path);
                item.ForeColor = Color.FromArgb(0, 200, 150);
                listBU.Items.Add(item);
            }
        }

        // ── Copy ────────────────────────────────────────
        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (copying) return;
            if (string.IsNullOrEmpty(txtPath.Text)) { MessageBox.Show("กรุณาเลือก Folder ปลายทาง"); return; }
            if (buList.Count == 0) { MessageBox.Show("กรุณาเพิ่ม BU Folder อย่างน้อย 1 folder"); return; }

            copying = true;
            btnCopy.Enabled = false;
            btnCopy.Text = "⏳  กำลัง Copy...";
            progressBar.Value = 0;
            lblSummary.Text = "";

            string dest   = txtPath.Text;
            string filter = txtFilter.Text.Trim();
            var    buses  = buList.ToList();

            Thread t = new Thread(() => DoCopy(dest, filter, buses));
            t.IsBackground = true;
            t.Start();
        }

        private void DoCopy(string dest, string filter, List<BuEntry> buses)
        {
            int ok = 0, err = 0;
            try
            {
                Log("info", $"── scan {buses.Count} BU folders ──");
                var allFiles = new List<FileEntry>();

                foreach (var bu in buses)
                {
                    var found = ScanDir(bu.Path, filter);
                    Log("info", $"{bu.Label} [{Path.GetFileName(bu.Path)}] พบ {found.Count} ไฟล์");
                    foreach (var f in found) f.BuLabel = bu.Label;
                    allFiles.AddRange(found);
                }

                int total = allFiles.Count;
                Log("info", $"รวม {total} ไฟล์");

                if (total == 0) { Log("warn", "ไม่พบไฟล์ที่ตรงเงื่อนไข"); Done(0, 0); return; }

                for (int i = 0; i < total; i++)
                {
                    int pct = (int)Math.Round((i + 1.0) / total * 100);
                    SetProgress(pct);

                    var f        = allFiles[i];
                    string fname = Path.GetFileName(f.Path);
                    string dest2 = Path.Combine(dest, fname);

                    // ถ้าซ้ำ → ต่อท้าย BU label
                    if (File.Exists(dest2))
                    {
                        string ext  = Path.GetExtension(fname);
                        string name = Path.GetFileNameWithoutExtension(fname);
                        dest2 = Path.Combine(dest, $"{name}_{f.BuLabel}{ext}");
                    }

                    try
                    {
                        File.Copy(f.Path, dest2, true);
                        string extra = Path.GetFileName(dest2) != fname
                            ? $"  →  {Path.GetFileName(dest2)}" : "";
                        Log("ok", $"[{f.BuLabel}] {fname}{extra}");
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        Log("err", $"[{f.BuLabel}] {fname} — {ex.Message}");
                        err++;
                    }
                }
            }
            catch (Exception ex) { Log("err", "Fatal: " + ex.Message); }
            finally { Done(ok, err); }
        }

        private List<FileEntry> ScanDir(string root, string filter, int depth = 0)
        {
            var results = new List<FileEntry>();
            if (depth > 4 || !Directory.Exists(root)) return results;
            try
            {
                foreach (var file in Directory.GetFiles(root))
                {
                    string fname = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(filter) ||
                        fname.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        results.Add(new FileEntry { Path = file });
                }
                foreach (var dir in Directory.GetDirectories(root))
                    results.AddRange(ScanDir(dir, filter, depth + 1));
            }
            catch { }
            return results;
        }

        // ── Helpers ─────────────────────────────────────
        private void Log(string tag, string msg)
        {
            string prefix = tag == "ok" ? "✅" : tag == "err" ? "❌" : tag == "warn" ? "⚠️" : "ℹ️";
            string line   = $"{DateTime.Now:HH:mm:ss}  {prefix}  {msg}";
            if (txtLog.InvokeRequired)
                txtLog.Invoke(new Action(() => AppendLog(line)));
            else
                AppendLog(line);
        }

        private void AppendLog(string line)
        {
            txtLog.AppendText(line + Environment.NewLine);
            txtLog.ScrollToCaret();
        }

        private void SetProgress(int pct)
        {
            if (progressBar.InvokeRequired)
                progressBar.Invoke(new Action(() => progressBar.Value = pct));
            else
                progressBar.Value = pct;
        }

        private void Done(int ok, int err)
        {
            if (this.InvokeRequired)
                this.Invoke(new Action(() => DoneUI(ok, err)));
            else
                DoneUI(ok, err);
        }

        private void DoneUI(int ok, int err)
        {
            copying = false;
            btnCopy.Enabled = true;
            btnCopy.Text    = "📋  เริ่ม Copy ไฟล์";
            progressBar.Value = 100;
            lblSummary.Text   = $"✅ Copied: {ok}    ❌ Error: {err}";
            lblSummary.ForeColor = err == 0
                ? Color.FromArgb(0, 200, 150)
                : Color.FromArgb(233, 69, 96);
            Log("info", $"── เสร็จสิ้น: copied={ok}  error={err} ──");
            MessageBox.Show($"เสร็จสิ้น!\n\nCopied: {ok}\nError: {err}",
                            "Copy MRP", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateCopyBtn()
        {
            btnCopy.Enabled = !string.IsNullOrEmpty(txtPath.Text) && buList.Count > 0;
        }

        // ── InitializeComponent (Designer code) ─────────
        private void InitializeComponent()
        {
            // Controls
            var lblTitle    = new Label();
            var lblSub      = new Label();
            var sep1        = new Panel();
            var lblDest     = new Label();
            txtPath         = new TextBox();
            btnPickDest     = new Button();
            var lblBU       = new Label();
            listBU          = new ListView();
            btnAddBU        = new Button();
            btnChangeBU     = new Button();
            btnRemoveBU     = new Button();
            var lblFilter   = new Label();
            txtFilter       = new TextBox();
            var lblFilterHint = new Label();
            btnCopy         = new Button();
            progressBar     = new ProgressBar();
            var lblLog      = new Label();
            txtLog          = new RichTextBox();
            lblSummary      = new Label();

            this.SuspendLayout();

            // Form
            this.ClientSize   = new Size(714, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font          = new Font("Segoe UI", 9f);

            int x = 14, w = 686, y = 10;

            // Title
            lblTitle.Text      = "📋  Copy MRP";
            lblTitle.Font      = new Font("Segoe UI", 13f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(224, 224, 224);
            lblTitle.BackColor = Color.FromArgb(22, 33, 62);
            lblTitle.Location  = new Point(0, 0);
            lblTitle.Size      = new Size(714, 42);
            lblTitle.Padding   = new Padding(14, 10, 0, 0);

            lblSub.Text      = "คัดลอก _AUFTRAG.TXT จากแต่ละ BU มายัง folder ปลายทาง";
            lblSub.ForeColor = Color.FromArgb(136, 136, 136);
            lblSub.BackColor = Color.FromArgb(22, 33, 62);
            lblSub.Location  = new Point(160, 14);
            lblSub.Size      = new Size(540, 20);

            sep1.BackColor = Color.FromArgb(42, 42, 74);
            sep1.Location  = new Point(0, 42);
            sep1.Size      = new Size(714, 1);

            y = 52;

            // Destination
            lblDest.Text      = "📂  Folder ปลายทาง";
            lblDest.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDest.ForeColor = Color.FromArgb(0, 200, 150);
            lblDest.Location  = new Point(x, y); lblDest.Size = new Size(200, 20); y += 22;

            txtPath.Location  = new Point(x, y);
            txtPath.Size      = new Size(560, 24);
            txtPath.ReadOnly  = true;
            txtPath.BackColor = Color.FromArgb(22, 33, 62);
            txtPath.ForeColor = Color.FromArgb(0, 200, 150);
            txtPath.Font      = new Font("Consolas", 9f);
            txtPath.BorderStyle = BorderStyle.FixedSingle;

            btnPickDest.Text      = "เลือก Folder";
            btnPickDest.Location  = new Point(582, y);
            btnPickDest.Size      = new Size(118, 24);
            btnPickDest.BackColor = Color.FromArgb(15, 52, 96);
            btnPickDest.ForeColor = Color.FromArgb(0, 200, 150);
            btnPickDest.FlatStyle = FlatStyle.Flat;
            btnPickDest.FlatAppearance.BorderColor = Color.FromArgb(42, 42, 74);
            btnPickDest.Click += btnPickDest_Click;
            y += 32;

            // BU List
            lblBU.Text      = "🗂  BU Source Folders";
            lblBU.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblBU.ForeColor = Color.FromArgb(0, 200, 150);
            lblBU.Location  = new Point(x, y); lblBU.Size = new Size(300, 20); y += 22;

            listBU.View          = View.Details;
            listBU.FullRowSelect = true;
            listBU.GridLines     = false;
            listBU.BackColor     = Color.FromArgb(22, 33, 62);
            listBU.ForeColor     = Color.FromArgb(224, 224, 224);
            listBU.BorderStyle   = BorderStyle.FixedSingle;
            listBU.Location      = new Point(x, y);
            listBU.Size          = new Size(w, 150);
            listBU.Columns.Add("BU", 50);
            listBU.Columns.Add("Path", 628);
            listBU.HeaderStyle   = ColumnHeaderStyle.Nonclickable;
            y += 158;

            btnAddBU.Text      = "＋ เพิ่ม BU";
            btnAddBU.Location  = new Point(x, y);
            btnAddBU.Size      = new Size(110, 28);
            btnAddBU.BackColor = Color.FromArgb(15, 52, 96);
            btnAddBU.ForeColor = Color.FromArgb(224, 224, 224);
            btnAddBU.FlatStyle = FlatStyle.Flat;
            btnAddBU.FlatAppearance.BorderColor = Color.FromArgb(42, 42, 74);
            btnAddBU.Click += btnAddBU_Click;

            btnChangeBU.Text      = "เปลี่ยน";
            btnChangeBU.Location  = new Point(x + 118, y);
            btnChangeBU.Size      = new Size(90, 28);
            btnChangeBU.BackColor = Color.FromArgb(15, 52, 96);
            btnChangeBU.ForeColor = Color.FromArgb(224, 224, 224);
            btnChangeBU.FlatStyle = FlatStyle.Flat;
            btnChangeBU.FlatAppearance.BorderColor = Color.FromArgb(42, 42, 74);
            btnChangeBU.Click += btnChangeBU_Click;

            btnRemoveBU.Text      = "✕ ลบ";
            btnRemoveBU.Location  = new Point(x + 216, y);
            btnRemoveBU.Size      = new Size(80, 28);
            btnRemoveBU.BackColor = Color.FromArgb(15, 52, 96);
            btnRemoveBU.ForeColor = Color.FromArgb(233, 69, 96);
            btnRemoveBU.FlatStyle = FlatStyle.Flat;
            btnRemoveBU.FlatAppearance.BorderColor = Color.FromArgb(42, 42, 74);
            btnRemoveBU.Click += btnRemoveBU_Click;
            y += 36;

            // Filter
            lblFilter.Text      = "🔍  ตัวกรองชื่อไฟล์:";
            lblFilter.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblFilter.ForeColor = Color.FromArgb(0, 200, 150);
            lblFilter.Location  = new Point(x, y); lblFilter.Size = new Size(130, 24);

            txtFilter.Text      = "_AUFTRAG";
            txtFilter.Location  = new Point(x + 134, y);
            txtFilter.Size      = new Size(180, 24);
            txtFilter.BackColor = Color.FromArgb(22, 33, 62);
            txtFilter.ForeColor = Color.FromArgb(224, 224, 224);
            txtFilter.Font      = new Font("Consolas", 9f);
            txtFilter.BorderStyle = BorderStyle.FixedSingle;

            lblFilterHint.Text      = "(ว่าง = copy ทุกไฟล์)";
            lblFilterHint.ForeColor = Color.FromArgb(100, 100, 120);
            lblFilterHint.Location  = new Point(x + 322, y + 4);
            lblFilterHint.Size      = new Size(200, 20);
            y += 34;

            // Copy Button
            btnCopy.Text      = "📋  เริ่ม Copy ไฟล์";
            btnCopy.Font      = new Font("Segoe UI", 11f, FontStyle.Bold);
            btnCopy.Location  = new Point(x, y);
            btnCopy.Size      = new Size(w, 40);
            btnCopy.BackColor = Color.FromArgb(0, 200, 150);
            btnCopy.ForeColor = Color.Black;
            btnCopy.FlatStyle = FlatStyle.Flat;
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Cursor    = Cursors.Hand;
            btnCopy.Click    += btnCopy_Click;
            y += 48;

            // Progress
            progressBar.Location  = new Point(x, y);
            progressBar.Size      = new Size(w, 6);
            progressBar.Style     = ProgressBarStyle.Continuous;
            progressBar.BackColor = Color.FromArgb(22, 33, 62);
            progressBar.ForeColor = Color.FromArgb(0, 200, 150);
            y += 12;

            // Log
            lblLog.Text      = "📄  Log";
            lblLog.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblLog.ForeColor = Color.FromArgb(0, 200, 150);
            lblLog.Location  = new Point(x, y); lblLog.Size = new Size(100, 20); y += 22;

            txtLog.Location    = new Point(x, y);
            txtLog.Size        = new Size(w, 150);
            txtLog.ReadOnly    = true;
            txtLog.BackColor   = Color.FromArgb(22, 33, 62);
            txtLog.ForeColor   = Color.FromArgb(224, 224, 224);
            txtLog.Font        = new Font("Consolas", 8.5f);
            txtLog.BorderStyle = BorderStyle.FixedSingle;
            txtLog.ScrollBars  = RichTextBoxScrollBars.Vertical;
            y += 158;

            // Summary
            lblSummary.Location  = new Point(x, y);
            lblSummary.Size      = new Size(w, 22);
            lblSummary.Font      = new Font("Consolas", 9f, FontStyle.Bold);
            lblSummary.ForeColor = Color.FromArgb(0, 200, 150);

            // Add controls
            this.Controls.AddRange(new Control[] {
                lblTitle, lblSub, sep1,
                lblDest, txtPath, btnPickDest,
                lblBU, listBU, btnAddBU, btnChangeBU, btnRemoveBU,
                lblFilter, txtFilter, lblFilterHint,
                btnCopy, progressBar,
                lblLog, txtLog, lblSummary
            });

            this.ResumeLayout();
        }

        // ── Fields ──────────────────────────────────────
        private TextBox   txtPath, txtFilter;
        private Button    btnPickDest, btnAddBU, btnChangeBU, btnRemoveBU, btnCopy;
        private ListView  listBU;
        private ProgressBar progressBar;
        private RichTextBox txtLog;
        private Label     lblSummary;
    }

    // ── Data classes ────────────────────────────────────
    public class BuEntry
    {
        public string Label { get; set; }
        public string Path  { get; set; }
    }

    public class FileEntry
    {
        public string Path    { get; set; }
        public string BuLabel { get; set; }
    }

    // ── Entry Point ─────────────────────────────────────
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
