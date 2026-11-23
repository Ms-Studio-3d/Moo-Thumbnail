using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MooThumbnail
{
    internal interface IDesktopActions
    {
        string ChooseSavePath(IWin32Window owner, string name);
        void Copy(string text);
        void OpenFolder(string filePath);
    }
    internal sealed class DesktopActions : IDesktopActions
    {
        public string ChooseSavePath(IWin32Window owner, string name)
        {
            using (var dialog = new SaveFileDialog { Title = "حفظ الصورة المصغّرة", FileName = name,
                Filter = "JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg", DefaultExt = "jpg", AddExtension = true,
                OverwritePrompt = true, CheckPathExists = true, InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) })
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
        }
        public void Copy(string text) { Clipboard.SetText(text); }
        public void OpenFolder(string filePath) { Process.Start("explorer.exe", "/select,\"" + filePath + "\""); }
    }
    internal sealed class MainForm : Form
    {
        private readonly ThumbnailService service;
        private readonly IDesktopActions desktop;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        internal readonly TextBox UrlInput;
        internal readonly ActionButton ExtractButton, SaveButton, CopyButton, FolderButton;
        internal readonly Label Status, Meta, QualityTag;
        internal readonly PictureBox Preview;
        internal readonly QualityOption[] Options;
        private readonly Label empty, count;
        private readonly ProgressBar progress;
        private LookupResult current;
        internal Thumbnail Selected { get; private set; }
        internal bool Busy { get; private set; }
        internal string LastSaved { get; private set; }
        private bool closing, disposed;

        internal MainForm(ThumbnailService service, IDesktopActions desktop)
        {
            this.service = service; this.desktop = desktop;
            Text = "Moo-Thumbnail"; Name = "MooThumbnail"; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1120, 810); MinimumSize = new Size(820, 680); AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96); BackColor = Theme.Background; ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 10); DoubleBuffered = true;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 15, 28, 10), ColumnCount = 1, RowCount = 6 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 85, 83, 150 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            Controls.Add(root);
            root.Controls.Add(new BrandHeader { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
            var intro = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 10, 0, 10) };
            intro.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); intro.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            intro.Controls.Add(Theme.Label("صورة الفيديو، بأفضل جودة متاحة.", 20, Theme.Text, true), 0, 0);
            intro.Controls.Add(Theme.Label("ألصق الرابط، اختر الجودة، واحفظ الصورة على جهازك.", 10, Theme.Muted), 0, 1);
            root.Controls.Add(intro, 0, 1);
            var inputCard = new Card { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(16, 10, 16, 9) };
            var inputLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Margin = Padding.Empty };
            foreach (int h in new[] { 25, 43, 22 }) inputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            inputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            inputLayout.Controls.Add(Theme.Label("رابط فيديو YouTube", 10, Theme.Text, true), 0, 0);
            var inputRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 166)); inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ExtractButton = new ActionButton("استخراج الصور", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
            UrlInput = new TextBox { Font = new Font("Segoe UI", 11), BackColor = Theme.Background, ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill, Margin = new Padding(0, 7, 0, 4), RightToLeft = RightToLeft.No,
                AccessibleName = "رابط فيديو YouTube", MaxLength = 4096 };
            inputRow.Controls.Add(ExtractButton, 0, 0); inputRow.Controls.Add(UrlInput, 1, 0); inputLayout.Controls.Add(inputRow, 0, 1);
            inputLayout.Controls.Add(Theme.Label("يدعم الفيديوهات وShorts وLive وروابط youtu.be  •  JPG", 8.5f, Theme.Muted), 0, 2);
            Status = Theme.Label("جاهز. أضف رابط الفيديو للبدء.", 9, Theme.Muted); Status.AccessibleName = "حالة العملية";
            inputLayout.Controls.Add(Status, 0, 3); inputCard.Controls.Add(inputLayout); root.Controls.Add(inputCard, 0, 2);
            progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false, Margin = new Padding(5, 6, 5, 6) };
            root.Controls.Add(progress, 0, 3);
            var results = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            var previewCard = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(16) };
            var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            QualityTag = Theme.Label("معاينة الصورة", 12, Theme.Text, true); previewLayout.Controls.Add(QualityTag, 0, 0);
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 13, 18), Margin = Padding.Empty };
            Preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "معاينة الصورة المصغّرة" };
            empty = Theme.Label("▶\r\n\r\nمعاينتك تبدأ هنا\r\n\r\nستظهر الصورة بعد فحص الرابط.", 12, Theme.Muted);
            empty.TextAlign = ContentAlignment.MiddleCenter; empty.AutoEllipsis = false;
            host.Controls.Add(Preview); host.Controls.Add(empty); previewLayout.Controls.Add(host, 0, 1);
            Meta = Theme.Label("6 QUALITY CHECKS  •  JPG", 8, Theme.Muted); Meta.TextAlign = ContentAlignment.MiddleLeft; Meta.RightToLeft = RightToLeft.No;
            previewLayout.Controls.Add(Meta, 0, 2);
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            SaveButton = new ActionButton("حفظ الصورة", true); CopyButton = new ActionButton("نسخ الرابط"); FolderButton = new ActionButton("فتح المجلد");
            ActionButton[] buttons = { SaveButton, CopyButton, FolderButton };
            for (int i = 0; i < buttons.Length; i++) { buttons[i].Dock = DockStyle.Fill; buttons[i].Margin = new Padding(0, 3, i == 2 ? 0 : 7, 0); actions.Controls.Add(buttons[i], i, 0); }
            previewLayout.Controls.Add(actions, 0, 3); previewCard.Controls.Add(previewLayout); results.Controls.Add(previewCard, 0, 0);
            var qualityCard = new Card { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(14, 10, 14, 10) };
            var qualityLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Margin = Padding.Empty };
            qualityLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); qualityLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            count = Theme.Label("اختر الجودة   ·   0 / 6", 12, Theme.Text, true); qualityLayout.Controls.Add(count, 0, 0);
            Options = new QualityOption[6];
            for (int i = 0; i < 6; i++)
            {
                qualityLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));
                var option = new QualityOption { Dock = DockStyle.Fill, Item = new Thumbnail { Quality = Youtube.Qualities[i] }, Enabled = false,
                    AccessibleName = Youtube.Qualities[i].Name };
                option.CheckedChanged += delegate { if (option.Checked && option.Item.Available) SelectImage(option.Item); };
                Options[i] = option; qualityLayout.Controls.Add(option, 0, i + 1);
            }
            qualityLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            qualityLayout.Controls.Add(Theme.Label("الأبعاد فعلية حسب الصورة المتاحة.", 8, Theme.Muted), 0, 7);
            qualityCard.Controls.Add(qualityLayout); results.Controls.Add(qualityCard, 1, 0); root.Controls.Add(results, 0, 4);
            root.Controls.Add(Theme.Label("Moo-Thumbnail   ·   الصور المصغّرة فقط   ·   بدون حساب", 8, Theme.Muted), 0, 5);
            ExtractButton.Click += async delegate { await ExtractAsync(UrlInput.Text); };
            UrlInput.KeyDown += async (sender, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ExtractAsync(UrlInput.Text); } };
            SaveButton.Click += delegate { SaveSelected(); }; CopyButton.Click += delegate { CopySelected(); }; FolderButton.Click += delegate { OpenSavedFolder(); };
            FormClosing += delegate { closing = true; lifetime.Cancel(); };
            SetBusy(false);
        }
        private void SetStatus(string message, Color color) { if (closing) return; Status.Text = message; Status.ForeColor = color; }
        private void SetBusy(bool value)
        {
            Busy = value; if (closing) return;
            ExtractButton.Enabled = !value; UrlInput.Enabled = !value; progress.Visible = value;
            ExtractButton.Text = value ? "جارٍ الفحص…" : "استخراج الصور";
            SaveButton.Enabled = CopyButton.Enabled = !value && Selected != null;
            FolderButton.Enabled = !value && LastSaved != null;
            foreach (var option in Options) option.Enabled = !value && option.Item.Available;
        }
        internal async Task ExtractAsync(string input)
        {
            if (Busy || closing) return;
            current = null; Selected = null; ClearPreview();
            foreach (var option in Options) { option.Checked = false; option.Item = new Thumbnail { Quality = option.Item.Quality }; option.Invalidate(); }
            count.Text = "اختر الجودة   ·   0 / 6"; QualityTag.Text = "معاينة الصورة"; Meta.Text = "6 QUALITY CHECKS  •  JPG";
            SetBusy(true); SetStatus("جارٍ فحص الجودات الست من YouTube…", Theme.Orange);
            try
            {
                current = await service.LookupAsync(input, lifetime.Token);
                if (closing) return;
                for (int i = 0; i < Options.Length; i++)
                {
                    Options[i].Item = current.Images[i]; Options[i].AccessibleName = current.Images[i].Quality.Name + " " + (current.Images[i].Available ? current.Images[i].Dimensions : "غير متاحة");
                    Options[i].Invalidate();
                }
                int available = current.Images.Count(x => x.Available);
                count.Text = "اختر الجودة   ·   " + available + " / 6";
                Options.First(x => x.Item == current.Best).Checked = true;
                SetStatus("تم العثور على " + available + " جودات متاحة. اختر الجودة واحفظ صورتك." +
                    (current.Images.Any(x => x.Failure == "network") ? " تعذّر فحص بعض الجودات؛ يمكنك إعادة المحاولة." : ""), Theme.Green);
            }
            catch (OperationCanceledException) { if (!closing) SetStatus("تم إلغاء العملية.", Theme.Muted); }
            catch (Exception error) { SetStatus(ImageFile.ErrorMessage(error), Theme.Red); }
            finally { SetBusy(false); }
        }
        private void ClearPreview() { Image old = Preview.Image; Preview.Image = null; if (old != null) old.Dispose(); empty.Visible = true; }
        private void SelectImage(Thumbnail image)
        {
            Selected = image;
            using (var stream = new MemoryStream(image.Bytes))
            using (Image decoded = Image.FromStream(stream))
            {
                Image previous = Preview.Image; Preview.Image = new Bitmap(decoded); if (previous != null) previous.Dispose();
            }
            empty.Visible = false; QualityTag.Text = image.Quality.Name + "   ·   \u200e" + image.Dimensions + "\u200e";
            Meta.Text = "VIDEO ID: " + current.VideoId + "     ·     " + (image.Bytes.Length / 1024.0).ToString("0.0") + " KB · JPG";
        }
        internal void SaveSelected()
        {
            if (Busy || Selected == null) return;
            SetBusy(true);
            try
            {
                string chosen = desktop.ChooseSavePath(this, "Moo-Thumbnail-" + current.VideoId + "-" + Selected.Quality.Key + ".jpg");
                if (chosen == null) { SetStatus("تم إلغاء الحفظ. يمكنك اختيار مكان آخر.", Theme.Muted); return; }
                ImageFile.Save(chosen, Selected.Bytes); LastSaved = Path.GetFullPath(chosen);
                SetStatus("تم حفظ الصورة: \u202a" + LastSaved + "\u202c", Theme.Green);
            }
            catch (Exception error) { SetStatus(ImageFile.ErrorMessage(error), Theme.Red); }
            finally { SetBusy(false); }
        }
        internal void CopySelected()
        {
            if (Busy || Selected == null) return;
            try { desktop.Copy(Selected.Url); SetStatus("تم نسخ رابط الصورة المباشر.", Theme.Green); }
            catch (System.Runtime.InteropServices.ExternalException) { SetStatus("الحافظة مستخدمة حاليًا. حاول النسخ مرة أخرى.", Theme.Red); }
        }
        internal void OpenSavedFolder()
        {
            if (Busy || LastSaved == null) return;
            try
            {
                if (!File.Exists(LastSaved)) { SetStatus("الصورة غير موجودة في مكان الحفظ. احفظها مرة أخرى.", Theme.Red); return; }
                desktop.OpenFolder(LastSaved);
            }
            catch (Exception error) { SetStatus(ImageFile.ErrorMessage(error), Theme.Red); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed) { disposed = true; lifetime.Cancel(); if (Preview != null && Preview.Image != null) Preview.Image.Dispose(); lifetime.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
