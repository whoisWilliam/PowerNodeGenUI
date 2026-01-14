using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using WinColor = System.Drawing.Color;
using WinFont = System.Drawing.Font;
using WinFontStyle = System.Drawing.FontStyle;
using WinLabel = System.Windows.Forms.Label;

namespace PowerNodeGenUI
{
    public class Form1 : Form
    {
        // ---- controls ----
        TextBox txtNets = new() { Width = 520 };
        TextBox txtNails = new() { Width = 520 };
        TextBox txtOutCsv = new() { Width = 520 };

        TextBox txtInclude = new() { Width = 260 };
        TextBox txtExclude = new() { Width = 260 };
        ListBox lstInclude = new() { Width = 260, Height = 110 };
        ListBox lstExclude = new() { Width = 260, Height = 110 };

        // Only show nets with nails (nail_id != 0)
        CheckBox chkOnlyWithNails = new() { Text = "Only show nets with nails", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 6, 0, 0) };

        Button btnRemoveInclude = new() { Text = "Remove", Width = 90, Height = 28, Margin = new Padding(0) };
        Button btnRemoveExclude = new() { Text = "Remove", Width = 90, Height = 28, Margin = new Padding(0) };
        Button btnClearInclude = new() { Text = "Clear", Width = 90, Height = 28, Margin = new Padding(0) };
        Button btnClearExclude = new() { Text = "Clear", Width = 90, Height = 28, Margin = new Padding(0) };

        Button btnSubmit = new() { Text = "Submit", Width = 140, Height = 36, Margin = new Padding(0, 2, 0, 2) };
        WinLabel lblStatus = new() { AutoSize = false, Text = "Ready", Width = 300, Height = 36, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Margin = new Padding(8, 2, 0, 2) };

        Button btnOpenCsv = new() { Text = "Open CSV", Width = 120, Height = 36, Margin = new Padding(8, 2, 0, 2) };

        // NEW: import old CSV to compare
        Button btnImportOldCsv = new() { Text = "Import Old CSV", Width = 150, Height = 36, Margin = new Padding(8, 2, 0, 2) };

        DataGridView dgv = new() { Dock = DockStyle.Fill };

        readonly string generatorExeFile = "PowerNodeGen.exe";

        public Form1()
        {
            Text = "Power Node List Generator";
            Width = 1920;
            Height = 1080;
            this.Font = new WinFont("Segoe UI", 10);

            // Enter 直接送出
            //this.AcceptButton = btnSubmit;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 135));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 260)); // keywords (taller so list area is more usable)
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));  // run bar (Submit + status)
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildFilePanel(), 0, 0);
            root.Controls.Add(BuildKeywordPanel(), 0, 1);
            root.Controls.Add(BuildRunPanel(), 0, 2);
            root.Controls.Add(BuildGridPanel(), 0, 3);
            Controls.Add(root);

            lstInclude.BorderStyle = BorderStyle.FixedSingle;
            lstInclude.IntegralHeight = false;
            //lstInclude.Dock = DockStyle.Fill;
            lstInclude.Height = 1220;

            lstExclude.BorderStyle = BorderStyle.FixedSingle;
            lstExclude.IntegralHeight = false;
            //lstExclude.Dock = DockStyle.Fill;
            lstExclude.Height = 1220;

            // default output
            txtOutCsv.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "PowerNodeList.csv"
            );

            // Enter to add keyword
            txtInclude.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { AddKeyword(txtInclude, lstInclude); e.SuppressKeyPress = true; }
            };
            txtExclude.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { AddKeyword(txtExclude, lstExclude); e.SuppressKeyPress = true; }
            };

            btnRemoveInclude.Click += (s, e) => RemoveSelected(lstInclude);
            btnRemoveExclude.Click += (s, e) => RemoveSelected(lstExclude);
            btnClearInclude.Click += (s, e) => lstInclude.Items.Clear();
            btnClearExclude.Click += (s, e) => lstExclude.Items.Clear();

            btnSubmit.Click += async (s, e) => await RunAsync();

            btnOpenCsv.Click += (s, e) => OpenCsv();
            btnOpenCsv.Enabled = File.Exists(txtOutCsv.Text.Trim());
            txtOutCsv.TextChanged += (s, e) => btnOpenCsv.Enabled = File.Exists(txtOutCsv.Text.Trim());

            // NEW
            btnImportOldCsv.Click += (s, e) => ImportOldCsvAndCompare();

            SetupGridStyle();
        }

        // ---------------- UI panels ----------------

        Control BuildFilePanel()
        {
            var panel = new GroupBox { Text = "Input / Output", Dock = DockStyle.Fill, Padding = new Padding(10) };

            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            //t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            //t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            Button btnBrowseNets = new() { Text = "Browse", Anchor = AnchorStyles.Left, Height = 28, Margin = new Padding(0) };
            Button btnBrowseNails = new() { Text = "Browse", Anchor = AnchorStyles.Left, Height = 28, Margin = new Padding(0) };
            Button btnBrowseOut = new() { Text = "Browse", Anchor = AnchorStyles.Left, Height = 28, Margin = new Padding(0) };

            btnBrowseNets.Click += (s, e) => PickFile(txtNets, "ASC files (*.asc)|*.asc|All files (*.*)|*.*");
            btnBrowseNails.Click += (s, e) => PickFile(txtNails, "ASC files (*.asc)|*.asc|All files (*.*)|*.*");
            btnBrowseOut.Click += (s, e) => PickSaveFile(txtOutCsv, "CSV files (*.csv)|*.csv|All files (*.*)|*.*");

            // Use fixed-size labels docked and vertically centered to align with textboxes
            var lblNets = new WinLabel
            {
                Text = "Nets.asc",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            t.Controls.Add(lblNets, 0, 0);

            t.Controls.Add(txtNets, 1, 0);
            t.Controls.Add(btnBrowseNets, 2, 0);

            var lblNails = new WinLabel
            {
                Text = "Nails.asc",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            t.Controls.Add(lblNails, 0, 1);

            t.Controls.Add(txtNails, 1, 1);
            t.Controls.Add(btnBrowseNails, 2, 1);

            var lblOut = new WinLabel
            {
                Text = "Output",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            t.Controls.Add(lblOut, 0, 2);

            t.Controls.Add(txtOutCsv, 1, 2);
            t.Controls.Add(btnBrowseOut, 2, 2);

            panel.Controls.Add(t);
            return panel;
        }

        Control BuildKeywordPanel()
        {
            var panel = new GroupBox { Text = "Keywords", Dock = DockStyle.Fill, Padding = new Padding(10) };

            // inputs + lists + button row
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 3 };

            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));       // inputs
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));       // list
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));       // remove buttons

            // labels
            var lblInclude = new WinLabel
            {
                Text = "Include",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            t.Controls.Add(lblInclude, 0, 0);
            t.Controls.Add(txtInclude, 1, 0);

            var lblExclude = new WinLabel
            {
                Text = "Exclude",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            t.Controls.Add(lblExclude, 2, 0);
            t.Controls.Add(txtExclude, 3, 0);

            // NEW: checkbox to the right of exclude input
            t.Controls.Add(chkOnlyWithNails, 4, 0);

            t.Controls.Add(lstInclude, 1, 1);
            t.Controls.Add(lstExclude, 3, 1);

            var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 6), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            leftButtons.Controls.Add(btnRemoveInclude);
            leftButtons.Controls.Add(btnClearInclude);
            t.Controls.Add(leftButtons, 1, 2);

            var rightButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 6), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            rightButtons.Controls.Add(btnRemoveExclude);
            rightButtons.Controls.Add(btnClearExclude);
            t.Controls.Add(rightButtons, 3, 2);

            panel.Controls.Add(t);
            return panel;
        }

        Control BuildRunPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 4) };

            var runPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Padding = new Padding(0, 2, 0, 2)
            };

            runPanel.Controls.Add(btnSubmit);
            runPanel.Controls.Add(lblStatus);
            runPanel.Controls.Add(btnOpenCsv);

            // NEW
            runPanel.Controls.Add(btnImportOldCsv);

            panel.Controls.Add(runPanel);
            return panel;
        }

        Control BuildGridPanel()
        {
            var panel = new GroupBox { Text = "Result (CSV Preview)", Dock = DockStyle.Fill, Padding = new Padding(10) };
            panel.Controls.Add(dgv);
            return panel;
        }

        void SetupGridStyle()
        {
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly = true;
            dgv.RowHeadersVisible = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = WinColor.FromArgb(248, 248, 248);
            dgv.ColumnHeadersDefaultCellStyle.Font = new WinFont("Segoe UI", 10, WinFontStyle.Bold);

            dgv.CellContentClick += Dgv_CellContentClick;
            dgv.CellDoubleClick += (s, e) => ShowPinsForRow(e.RowIndex);
        }

        // ---------------- keyword actions ----------------

        //void AddKeyword(TextBox box, ListBox list)
        //{
        //    var key = box.Text.Trim();
        //    if (string.IsNullOrEmpty(key)) return;

        //    foreach (var item in list.Items)
        //    {
        //        if (string.Equals(item.ToString(), key, StringComparison.OrdinalIgnoreCase))
        //        {
        //            box.Clear();
        //            return;
        //        }
        //    }

        //    list.Items.Add(key);
        //    box.Clear();
        //}

        void AddKeyword(TextBox box, ListBox list)
        {
            var key = box.Text.Trim();
            if (string.IsNullOrEmpty(key)) return;

            foreach (var item in list.Items)
            {
                if (string.Equals(item.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    box.Clear();
                    return;
                }
            }

            list.Items.Add(key);
            box.Clear();
        }

        void RemoveSelected(ListBox list)
        {
            if (list.SelectedIndex >= 0)
                list.Items.RemoveAt(list.SelectedIndex);
        }

        // ---------------- file pickers ----------------

        void PickFile(TextBox target, string filter)
        {
            using var dlg = new OpenFileDialog { Filter = filter };
            if (dlg.ShowDialog() == DialogResult.OK)
                target.Text = dlg.FileName;
        }

        void PickSaveFile(TextBox target, string filter)
        {
            using var dlg = new SaveFileDialog { Filter = filter, FileName = Path.GetFileName(target.Text) };
            if (dlg.ShowDialog() == DialogResult.OK)
                target.Text = dlg.FileName;
        }

        // ---------------- submit -> generate csv -> display ----------------

        void OpenCsv()
        {
            string path = txtOutCsv.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Output CSV path is empty.");
                return;
            }

            if (!File.Exists(path))
            {
                MessageBox.Show("CSV file not found. Please run Submit first (or check the Output path).");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open CSV.\n\n" + ex.Message);
            }
        }

        // NEW: import old CSV, compare, pop out result
        void ImportOldCsvAndCompare()
        {
            if (dgv.DataSource is not DataTable newDt || newDt.Rows.Count == 0)
            {
                MessageBox.Show("Please generate/load the NEW power node list first (so the grid has data), then import the old CSV to compare.");
                return;
            }

            using var dlg = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Import Old Power Node List CSV"
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            DataTable oldDt;
            try
            {
                oldDt = ReadCsvToDataTable(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to read old CSV.\n\n" + ex.Message);
                return;
            }

            if (oldDt.Rows.Count == 0)
            {
                MessageBox.Show("Old CSV is empty.");
                return;
            }

            DataTable diff;
            try
            {
                diff = BuildDiffTable(oldDt, newDt);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Compare failed.\n\n" + ex.Message);
                return;
            }

            using var diffDlg = new DiffDialog(diff, Path.GetFileName(dlg.FileName));
            diffDlg.ShowDialog(this);
        }

        static string GetCell(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col)) return "";
            return row[col]?.ToString() ?? "";
        }

        static DataTable BuildDiffTable(DataTable oldDt, DataTable newDt)
        {
            string keyCol =
                oldDt.Columns.Contains("net_id") && newDt.Columns.Contains("net_id") ? "net_id" :
                oldDt.Columns.Contains("net_name") && newDt.Columns.Contains("net_name") ? "net_name" :
                "";

            if (string.IsNullOrEmpty(keyCol))
                throw new InvalidOperationException("Cannot compare: both CSVs must contain net_id or net_name column.");

            var diff = new DataTable();
            diff.Columns.Add("Status");
            diff.Columns.Add("net_id");
            diff.Columns.Add("net_name");
            diff.Columns.Add("changed_fields");
            diff.Columns.Add("old_nail_id");
            diff.Columns.Add("new_nail_id");
            diff.Columns.Add("old_related_pins_cnt");
            diff.Columns.Add("new_related_pins_cnt");
            diff.Columns.Add("pins_list_changed");

            var oldMap = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in oldDt.Rows)
            {
                var k = GetCell(r, keyCol).Trim();
                if (k.Length == 0) continue;
                if (!oldMap.ContainsKey(k)) oldMap[k] = r;
            }

            var newMap = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in newDt.Rows)
            {
                var k = GetCell(r, keyCol).Trim();
                if (k.Length == 0) continue;
                if (!newMap.ContainsKey(k)) newMap[k] = r;
            }

            var oldKeys = new HashSet<string>(oldMap.Keys, StringComparer.OrdinalIgnoreCase);
            var newKeys = new HashSet<string>(newMap.Keys, StringComparer.OrdinalIgnoreCase);

            // Added
            foreach (var k in newKeys.Where(k => !oldKeys.Contains(k)))
            {
                var nr = newMap[k];
                var dr = diff.NewRow();
                dr["Status"] = "Added";
                dr["net_id"] = GetCell(nr, "net_id");
                dr["net_name"] = GetCell(nr, "net_name");
                dr["new_nail_id"] = GetCell(nr, "nail_id");
                dr["new_related_pins_cnt"] = GetCell(nr, "related_pins_cnt");
                diff.Rows.Add(dr);
            }

            // Removed
            foreach (var k in oldKeys.Where(k => !newKeys.Contains(k)))
            {
                var orow = oldMap[k];
                var dr = diff.NewRow();
                dr["Status"] = "Removed";
                dr["net_id"] = GetCell(orow, "net_id");
                dr["net_name"] = GetCell(orow, "net_name");
                dr["old_nail_id"] = GetCell(orow, "nail_id");
                dr["old_related_pins_cnt"] = GetCell(orow, "related_pins_cnt");
                diff.Rows.Add(dr);
            }

            // Common -> Modified / Unchanged
            foreach (var k in newKeys.Where(k => oldKeys.Contains(k)))
            {
                var orow = oldMap[k];
                var nrow = newMap[k];

                string oldNail = GetCell(orow, "nail_id");
                string newNail = GetCell(nrow, "nail_id");

                string oldPinsCnt = GetCell(orow, "related_pins_cnt");
                string newPinsCnt = GetCell(nrow, "related_pins_cnt");

                string oldPinsList = GetCell(orow, "related_pins_list");
                string newPinsList = GetCell(nrow, "related_pins_list");

                var changed = new List<string>();
                if (!string.Equals(oldNail, newNail, StringComparison.OrdinalIgnoreCase)) changed.Add("nail_id");
                if (!string.Equals(oldPinsCnt, newPinsCnt, StringComparison.OrdinalIgnoreCase)) changed.Add("related_pins_cnt");

                bool pinsListChanged = !string.Equals(oldPinsList, newPinsList, StringComparison.Ordinal);
                if (pinsListChanged) changed.Add("related_pins_list");

                var dr = diff.NewRow();
                dr["Status"] = changed.Count == 0 ? "Unchanged" : "Modified";
                dr["net_id"] = GetCell(nrow, "net_id");
                dr["net_name"] = GetCell(nrow, "net_name");
                dr["changed_fields"] = string.Join(",", changed);
                dr["old_nail_id"] = oldNail;
                dr["new_nail_id"] = newNail;
                dr["old_related_pins_cnt"] = oldPinsCnt;
                dr["new_related_pins_cnt"] = newPinsCnt;
                dr["pins_list_changed"] = pinsListChanged ? "Yes" : "No";
                diff.Rows.Add(dr);
            }

            return diff;
        }

        async Task RunAsync()
        {
            if (!File.Exists(txtNets.Text)) { MessageBox.Show("Nets.asc not found."); return; }
            if (!File.Exists(txtNails.Text)) { MessageBox.Show("Nails.asc not found."); return; }
            if (lstInclude.Items.Count == 0) { MessageBox.Show("Include keywords is empty."); return; }

            string outCsv = txtOutCsv.Text.Trim();
            if (string.IsNullOrEmpty(outCsv)) { MessageBox.Show("Output CSV path is empty."); return; }

            string exePath = Path.Combine(AppContext.BaseDirectory, generatorExeFile);
            if (!File.Exists(exePath))
            {
                MessageBox.Show($"Cannot find {generatorExeFile} in:\n{AppContext.BaseDirectory}\n\nPlease copy PowerNodeGen.exe here.");
                return;
            }

            // cfg temp file
            string cfgPath = Path.Combine(Path.GetTempPath(), $"power_keywords_{Guid.NewGuid():N}.txt");
            WriteKeywordConfig(cfgPath);

            lblStatus.Text = "Running...";
            btnOpenCsv.Enabled = false;

            try
            {
                // append nails filter flag if checked
                string args = $"--nets \"{txtNets.Text}\" --nails \"{txtNails.Text}\" --cfg \"{cfgPath}\" --out \"{outCsv}\"";
                if (chkOnlyWithNails.Checked) args += " --only-nails";

                var (code, so, se) = await Task.Run(() =>
                    RunProcess(exePath, args));

                if (code != 0)
                {
                    MessageBox.Show($"Generator failed (code {code}).\n\nSTDERR:\n{se}\n\nSTDOUT:\n{so}");
                    lblStatus.Text = "Failed";
                    return;
                }

                if (!File.Exists(outCsv))
                {
                    MessageBox.Show("CSV not generated.");
                    lblStatus.Text = "Failed";
                    return;
                }

                LoadCsvToGrid(outCsv);
                lblStatus.Text = $"Done. Rows: {dgv.Rows.Count}";
                btnOpenCsv.Enabled = File.Exists(outCsv);
            }
            finally
            {
                try { File.Delete(cfgPath); } catch { }
            }
        }

        void WriteKeywordConfig(string path)
        {
            var sb = new StringBuilder();
            foreach (var item in lstInclude.Items) sb.AppendLine("+" + item.ToString());
            foreach (var item in lstExclude.Items) sb.AppendLine("-" + item.ToString());

            // IMPORTANT: UTF-8 WITHOUT BOM (prevents C++ parser from rejecting first line)
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        static (int exitCode, string stdOut, string stdErr) RunProcess(string exe, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi)!;
            string o = p.StandardOutput.ReadToEnd();
            string e = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, o, e);
        }

        void LoadCsvToGrid(string csvPath)
        {
            var dt = ReadCsvToDataTable(csvPath);

            dgv.Columns.Clear();
            dgv.DataSource = dt;

            // pins list 太長：先隱藏
            if (dgv.Columns.Contains("related_pins_list"))
                dgv.Columns["related_pins_list"].Visible = false;

            // Add Pins button column once
            if (!dgv.Columns.Contains("Pins"))
            {
                var btnCol = new DataGridViewButtonColumn
                {
                    Name = "Pins",
                    HeaderText = "Pins",
                    Text = "View",
                    UseColumnTextForButtonValue = true,
                    Width = 70
                };
                dgv.Columns.Add(btnCol);
            }
        }

        void Dgv_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv.Columns[e.ColumnIndex].Name == "Pins")
                ShowPinsForRow(e.RowIndex);
        }

        void ShowPinsForRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgv.Rows.Count) return;
            if (!dgv.Columns.Contains("related_pins_list")) return;

            var row = dgv.Rows[rowIndex];

            string netId = dgv.Columns.Contains("net_id") ? row.Cells["net_id"].Value?.ToString() ?? "" : "";
            string netName = dgv.Columns.Contains("net_name") ? row.Cells["net_name"].Value?.ToString() ?? "" : "";
            string pins = row.Cells["related_pins_list"].Value?.ToString() ?? "";

            using var dlg = new PinsDialog(netId, netName, pins);
            dlg.ShowDialog(this);
        }

        // ---------------- CSV parser (quoted csv supported) ----------------

        static DataTable ReadCsvToDataTable(string path)
        {
            var dt = new DataTable();

            using var sr = new StreamReader(path, Encoding.UTF8);
            string? header = sr.ReadLine();
            if (header == null) return dt;

            var headerCols = ParseCsvLine(header);
            foreach (var c in headerCols) dt.Columns.Add(c);

            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine();
                if (string.IsNullOrEmpty(line)) continue;

                var cols = ParseCsvLine(line);
                var row = dt.NewRow();
                for (int i = 0; i < dt.Columns.Count && i < cols.Count; i++)
                    row[i] = cols[i];
                dt.Rows.Add(row);
            }

            return dt;
        }

        static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"'); i++;
                        }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }
    }

    // ✅ Compare old vs new power node list (diff view)
    public class DiffDialog : Form
    {
        readonly DataTable diffTable;
        readonly DataView view;

        ComboBox cbo = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        WinLabel lbl = new() { Dock = DockStyle.Top, AutoSize = false, Height = 26, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        DataGridView dgv = new() { Dock = DockStyle.Fill };

        public DiffDialog(DataTable diffTable, string oldFileName)
        {
            this.diffTable = diffTable;
            view = new DataView(diffTable);

            Text = $"Compare Result (Old: {oldFileName})";
            Width = 1100;
            Height = 720;

            cbo.Items.AddRange(new object[] { "All", "Added", "Removed", "Modified", "Unchanged" });
            cbo.SelectedIndex = 0;
            cbo.SelectedIndexChanged += (s, e) => ApplyFilter();

            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.RowHeadersVisible = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;

            dgv.DataSource = view;

            Controls.Add(dgv);
            Controls.Add(lbl);
            Controls.Add(cbo);

            UpdateSummary();
        }

        void ApplyFilter()
        {
            string sel = cbo.SelectedItem?.ToString() ?? "All";
            if (sel == "All") view.RowFilter = "";
            else view.RowFilter = $"Status = '{sel.Replace("'", "''")}'";

            UpdateSummary();
        }

        void UpdateSummary()
        {
            int total = diffTable.Rows.Count;
            int added = 0, removed = 0, modified = 0, unchanged = 0;

            foreach (DataRow r in diffTable.Rows)
            {
                string s = r["Status"]?.ToString() ?? "";
                if (s == "Added") added++;
                else if (s == "Removed") removed++;
                else if (s == "Modified") modified++;
                else if (s == "Unchanged") unchanged++;
            }

            lbl.Text = $"Total: {total}   Added: {added}   Removed: {removed}   Modified: {modified}   Unchanged: {unchanged}";
        }
    }

    // ✅ pins 太長時，用彈窗展開
    public class PinsDialog : Form
    {
        TextBox txtSearch = new() { Dock = DockStyle.Top, PlaceholderText = "Search..." };
        ListBox lst = new() { Dock = DockStyle.Fill };
        WinLabel lbl = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 6) };

        readonly string[] allPins;

        public PinsDialog(string netId, string netName, string pinsCsvCell)
        {
            Text = $"Pins - #{netId} {netName}";
            Width = 800;
            Height = 520;
            this.Font = new WinFont("Segoe UI", 10);

            allPins = (pinsCsvCell ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();

            lbl.Text = $"Net: #{netId} {netName}   Pins: {allPins.Length}";

            Controls.Add(lst);
            Controls.Add(txtSearch);
            Controls.Add(lbl);

            txtSearch.TextChanged += (s, e) => ApplyFilter();
            LoadPins(allPins);
        }

        void ApplyFilter()
        {
            var q = (txtSearch.Text ?? "").Trim();
            if (q.Length == 0) { LoadPins(allPins); return; }

            var filtered = allPins.Where(p => p.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            LoadPins(filtered);
        }

        void LoadPins(string[] pins)
        {
            lst.BeginUpdate();
            lst.Items.Clear();
            foreach (var p in pins) lst.Items.Add(p);
            lst.EndUpdate();
        }
    }
}
