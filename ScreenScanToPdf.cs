using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Kindle to PDF")]
[assembly: AssemblyProduct("Kindle to PDF")]
[assembly: AssemblyDescription("Kindle viewer screen capture to PDF with optional OCR")]

internal static class ScanNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);

    public static void FocusWindowAt(int x, int y)
    {
        var p = new POINT { X = x, Y = y };
        IntPtr hwnd = WindowFromPoint(p);
        if (hwnd == IntPtr.Zero) return;
        IntPtr root = GetAncestor(hwnd, 2);
        if (root == IntPtr.Zero) root = hwnd;
        SetForegroundWindow(root);
    }

    public static void WheelDown(int notches)
    {
        mouse_event(0x0800, 0, 0, -120 * Math.Abs(notches), UIntPtr.Zero);
    }
}

internal static class CaptureHelpers
{
    public static string DefaultPdfPath()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop))
            desktop = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(desktop, "viewer_scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf");
    }

    public static Rectangle? SelectRegion()
    {
        Rectangle virtualScreen = SystemInformation.VirtualScreen;
        bool down = false;
        int startX = 0, startY = 0, currentX = 0, currentY = 0;
        Rectangle? result = null;

        using (var form = new Form())
        {
            form.FormBorderStyle = FormBorderStyle.None;
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = virtualScreen;
            form.TopMost = true;
            form.BackColor = Color.Black;
            form.Opacity = 0.28;
            form.Cursor = Cursors.Cross;
            form.KeyPreview = true;

            var hint = new Label
            {
                Text = "キャプチャする範囲をドラッグしてください。Esc でキャンセル。",
                ForeColor = Color.White,
                BackColor = Color.Black,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Bounds = new Rectangle(24, 24, 900, 40)
            };
            form.Controls.Add(hint);

            form.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    result = null;
                    form.Close();
                }
            };

            form.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                down = true;
                startX = e.X;
                startY = e.Y;
                currentX = e.X;
                currentY = e.Y;
                form.Invalidate();
            };

            form.MouseMove += (_, e) =>
            {
                if (!down) return;
                currentX = e.X;
                currentY = e.Y;
                form.Invalidate();
            };

            form.MouseUp += (_, e) =>
            {
                if (!down) return;
                down = false;

                int x = Math.Min(startX, e.X);
                int y = Math.Min(startY, e.Y);
                int w = Math.Abs(e.X - startX);
                int h = Math.Abs(e.Y - startY);

                if (w < 30 || h < 30)
                {
                    MessageBox.Show("選択範囲が小さすぎます。もう一度ドラッグしてください。");
                    return;
                }

                result = new Rectangle(virtualScreen.Left + x, virtualScreen.Top + y, w, h);
                form.Close();
            };

            form.Paint += (_, e) =>
            {
                if (!down) return;
                int x = Math.Min(startX, currentX);
                int y = Math.Min(startY, currentY);
                int w = Math.Abs(currentX - startX);
                int h = Math.Abs(currentY - startY);
                if (w <= 0 || h <= 0) return;

                var rect = new Rectangle(x, y, w, h);
                using (var fill = new SolidBrush(Color.FromArgb(80, 0, 150, 255)))
                using (var pen = new Pen(Color.DeepSkyBlue, 4))
                {
                    e.Graphics.FillRectangle(fill, rect);
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };

            form.ShowDialog();
        }

        return result;
    }

    public static void SaveRegionJpeg(Rectangle region, string path, long quality)
    {
        using (var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format24bppRgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size);
            ImageCodecInfo enc = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bmp.Save(path, enc, parameters);
            }
        }
    }
}

internal static class PdfBuilder
{
    public static void CreateFromJpegs(IList<string> images, string outputPath)
    {
        if (images.Count == 0)
            throw new InvalidOperationException("PDF に変換する画像がありません。");

        int objectCount = 2 + (images.Count * 3);
        var offsets = new long[objectCount + 1];
        using (var ms = new MemoryStream())
        {
            WriteAscii(ms, "%PDF-1.4\n");

            AddObjectHeader(ms, offsets, 1);
            WriteAscii(ms, "<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            var kids = new List<string>();
            for (int i = 0; i < images.Count; i++)
                kids.Add((i * 3 + 3) + " 0 R");

            AddObjectHeader(ms, offsets, 2);
            WriteAscii(ms, "<< /Type /Pages /Count " + images.Count + " /Kids [ " + string.Join(" ", kids) + " ] >>\nendobj\n");

            for (int i = 0; i < images.Count; i++)
            {
                int pageId = i * 3 + 3;
                int imageId = pageId + 1;
                int contentId = pageId + 2;
                byte[] jpeg = File.ReadAllBytes(images[i]);

                int w, h;
                using (var img = Image.FromFile(images[i]))
                {
                    w = img.Width;
                    h = img.Height;
                }

                AddObjectHeader(ms, offsets, pageId);
                WriteAscii(ms, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + w + " " + h + "] /Resources << /XObject << /Im" + i + " " + imageId + " 0 R >> >> /Contents " + contentId + " 0 R >>\nendobj\n");

                AddObjectHeader(ms, offsets, imageId);
                WriteAscii(ms, "<< /Type /XObject /Subtype /Image /Width " + w + " /Height " + h + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpeg.Length + " >>\nstream\n");
                ms.Write(jpeg, 0, jpeg.Length);
                WriteAscii(ms, "\nendstream\nendobj\n");

                string content = "q\n" + w + " 0 0 " + h + " 0 0 cm\n/Im" + i + " Do\nQ\n";
                byte[] contentBytes = Encoding.ASCII.GetBytes(content);
                AddObjectHeader(ms, offsets, contentId);
                WriteAscii(ms, "<< /Length " + contentBytes.Length + " >>\nstream\n");
                ms.Write(contentBytes, 0, contentBytes.Length);
                WriteAscii(ms, "endstream\nendobj\n");
            }

            long xref = ms.Position;
            WriteAscii(ms, "xref\n0 " + (objectCount + 1) + "\n");
            WriteAscii(ms, "0000000000 65535 f \n");
            for (int id = 1; id <= objectCount; id++)
                WriteAscii(ms, offsets[id].ToString("0000000000") + " 00000 n \n");
            WriteAscii(ms, "trailer\n<< /Size " + (objectCount + 1) + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");

            File.WriteAllBytes(outputPath, ms.ToArray());
        }
    }

    private static void AddObjectHeader(MemoryStream ms, long[] offsets, int id)
    {
        offsets[id] = ms.Position;
        WriteAscii(ms, id + " 0 obj\n");
    }

    private static void WriteAscii(MemoryStream ms, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        ms.Write(bytes, 0, bytes.Length);
    }
}

internal static class OcrRunner
{
    public static void Run(string template, string imagesDir, string ocrDir, Label status)
    {
        if (string.IsNullOrWhiteSpace(template))
            throw new InvalidOperationException("OCR コマンドが空です。");

        Directory.CreateDirectory(ocrDir);
        string command = template.Replace("{images}", imagesDir).Replace("{out}", ocrDir);
        status.Text = "OCR を実行中...";
        Application.DoEvents();

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = Process.Start(psi))
        {
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            File.WriteAllText(Path.Combine(ocrDir, "ocr_stdout.txt"), stdout, Encoding.UTF8);
            File.WriteAllText(Path.Combine(ocrDir, "ocr_stderr.txt"), stderr, Encoding.UTF8);

            if (process.ExitCode != 0)
                throw new InvalidOperationException("OCR コマンドが失敗しました。ocr_stderr.txt を確認してください。");
        }

        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ocr_stdout.txt", "ocr_stderr.txt", "merged_text.txt"
        };

        var txtFiles = Directory.GetFiles(ocrDir, "*.txt", SearchOption.AllDirectories)
            .Where(f => !skip.Contains(Path.GetFileName(f)))
            .OrderBy(f => f)
            .ToList();

        if (txtFiles.Count > 0)
        {
            var merged = new StringBuilder();
            foreach (string file in txtFiles)
            {
                merged.AppendLine("===== " + Path.GetFileName(file) + " =====");
                merged.AppendLine(File.ReadAllText(file, Encoding.UTF8));
                merged.AppendLine();
            }
            File.WriteAllText(Path.Combine(ocrDir, "merged_text.txt"), merged.ToString(), Encoding.UTF8);
        }
    }
}

internal sealed class MainForm : Form
{
    private readonly NumericUpDown _pages = CreateNumber(140, 22, 10, 1, 9999);
    private readonly NumericUpDown _quality = CreateNumber(460, 22, 92, 50, 100);
    private readonly NumericUpDown _wait = CreateNumber(140, 58, 900, 100, 30000);
    private readonly NumericUpDown _wheel = CreateNumber(460, 58, 6, 1, 80);
    private readonly ComboBox _mode = new ComboBox();
    private readonly TextBox _pdfPath = new TextBox();
    private readonly CheckBox _ocrCheck = new CheckBox();
    private readonly TextBox _ocrTemplate = new TextBox();
    private readonly ProgressBar _progress = new ProgressBar();
    private readonly Label _status = new Label();
    private readonly Button _start = new Button();

    public MainForm()
    {
        Text = "Kindle to PDF";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(680, 430);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9);

        _wait.Increment = 100;
        _mode.SetBounds(140, 94, 160, 24);
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange(new object[] { "マウスホイール", "PageDown キー" });
        _mode.SelectedIndex = 0;

        _pdfPath.SetBounds(140, 132, 420, 24);
        _pdfPath.Text = CaptureHelpers.DefaultPdfPath();

        var browse = new Button { Text = "参照", Bounds = new Rectangle(570, 131, 76, 26) };
        browse.Click += (_, __) =>
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "PDF ファイル (*.pdf)|*.pdf";
                dialog.FileName = Path.GetFileName(_pdfPath.Text);
                string dir = Path.GetDirectoryName(_pdfPath.Text);
                if (Directory.Exists(dir)) dialog.InitialDirectory = dir;
                if (dialog.ShowDialog() == DialogResult.OK)
                    _pdfPath.Text = dialog.FileName;
            }
        };

        _ocrCheck.Text = "PDF 作成後に OCR を実行";
        _ocrCheck.SetBounds(140, 172, 260, 24);

        _ocrTemplate.SetBounds(140, 204, 506, 24);
        string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string ocrBat = Path.Combine(appDir ?? "", "run_ndlocr_lite_for_scanner.bat");
        _ocrTemplate.Text = "\"" + ocrBat + "\" \"{images}\" \"{out}\"";

        _progress.SetBounds(24, 300, 622, 18);
        _status.Text = "準備完了";
        _status.AutoEllipsis = true;
        _status.SetBounds(24, 326, 622, 42);

        _start.Text = "範囲を選択して開始";
        _start.SetBounds(390, 380, 256, 30);
        _start.Click += Start_Click;

        Controls.AddRange(new Control[]
        {
            CreateLabel("ページ数", 24, 22, 100), _pages,
            CreateLabel("画質", 344, 22, 80), _quality,
            CreateLabel("待機 (ms)", 24, 58, 100), _wait,
            CreateLabel("ホイール刻み", 344, 58, 100), _wheel,
            CreateLabel("ページ送り", 24, 94, 100), _mode,
            CreateLabel("出力 PDF", 24, 132, 100), _pdfPath, browse,
            _ocrCheck,
            CreateLabel("OCR コマンド", 24, 204, 100), _ocrTemplate,
            CreateLabel("{images} は画像フォルダ、{out} は OCR 出力フォルダに置き換わります。", 140, 236, 506),
            CreateLabel("開始前にビューアで最初のページを開いてください。範囲選択後にキャプチャが始まります。", 24, 266, 622),
            _progress, _status, _start
        });
    }

    private void Start_Click(object sender, EventArgs e)
    {
        string outPdf = _pdfPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(outPdf))
        {
            MessageBox.Show("出力 PDF のパスを指定してください。");
            return;
        }
        if (!".pdf".Equals(Path.GetExtension(outPdf), StringComparison.OrdinalIgnoreCase))
        {
            outPdf += ".pdf";
            _pdfPath.Text = outPdf;
        }
        string outDir = Path.GetDirectoryName(outPdf);
        if (!Directory.Exists(outDir))
        {
            MessageBox.Show("出力フォルダが存在しません。");
            return;
        }

        _start.Enabled = false;
        Hide();
        Thread.Sleep(250);

        try
        {
            Rectangle? region = CaptureHelpers.SelectRegion();
            if (!region.HasValue)
            {
                Show();
                _status.Text = "キャンセルしました";
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = Path.GetFileNameWithoutExtension(outPdf);
            string imageDir = Path.Combine(outDir, baseName + "_images_" + stamp);
            Directory.CreateDirectory(imageDir);

            int count = (int)_pages.Value;
            _progress.Minimum = 0;
            _progress.Maximum = count;
            _progress.Value = 0;

            Rectangle captureRegion = region.Value;
            int cx = captureRegion.Left + captureRegion.Width / 2;
            int cy = captureRegion.Top + captureRegion.Height / 2;
            Cursor.Position = new Point(cx, cy);
            Thread.Sleep(250);
            ScanNative.FocusWindowAt(cx, cy);
            Thread.Sleep(700);

            var images = new List<string>();
            for (int i = 1; i <= count; i++)
            {
                _status.Text = "キャプチャ中: " + i + " / " + count;
                Application.DoEvents();

                string imgPath = Path.Combine(imageDir, "page_" + i.ToString("D4") + ".jpg");
                CaptureHelpers.SaveRegionJpeg(captureRegion, imgPath, (long)_quality.Value);
                images.Add(imgPath);
                _progress.Value = i;
                Application.DoEvents();

                if (i < count)
                {
                    if (_mode.SelectedIndex == 1)
                        SendKeys.SendWait("{PGDN}");
                    else
                    {
                        Cursor.Position = new Point(cx, cy);
                        ScanNative.WheelDown((int)_wheel.Value);
                    }
                    Thread.Sleep((int)_wait.Value);
                }
            }

            _status.Text = "PDF を作成中...";
            Application.DoEvents();
            PdfBuilder.CreateFromJpegs(images, outPdf);

            if (_ocrCheck.Checked)
            {
                string ocrDir = Path.Combine(outDir, baseName + "_ocr_" + stamp);
                OcrRunner.Run(_ocrTemplate.Text, imageDir, ocrDir, _status);
                _status.Text = "完了: PDF と OCR 出力を保存しました。";
                Show();
                MessageBox.Show("完了しました。\nPDF: " + outPdf + "\n画像: " + imageDir + "\nOCR: " + ocrDir);
            }
            else
            {
                _status.Text = "完了: PDF を保存しました。";
                Show();
                MessageBox.Show("完了しました。\nPDF: " + outPdf + "\n画像: " + imageDir);
            }
        }
        catch (Exception ex)
        {
            Show();
            _status.Text = "エラー: " + ex.Message;
            MessageBox.Show(ex.Message, "エラー");
        }
        finally
        {
            _start.Enabled = true;
            Activate();
        }
    }

    private static Label CreateLabel(string text, int x, int y, int w)
    {
        return new Label { Text = text, Bounds = new Rectangle(x, y, w, 24) };
    }

    private static NumericUpDown CreateNumber(int x, int y, decimal value, decimal min, decimal max)
    {
        return new NumericUpDown
        {
            Bounds = new Rectangle(x, y, 90, 24),
            Minimum = min,
            Maximum = max,
            Value = value
        };
    }
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try { ScanNative.SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
