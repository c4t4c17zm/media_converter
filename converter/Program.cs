using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace C0_0nverter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ConverterWindow());
    }
}

internal sealed class ConverterWindow : Form
{
    private static readonly string[] VideoFormats = ["MP4", "MKV", "MOV", "WebM"];
    private static readonly string[] AudioFormats = ["MP3", "WAV", "FLAC", "OGG", "AAC"];
    private static readonly string[] Qualities = ["Высокое качество", "Оптимальный размер", "Компактный файл"];
    private readonly TextBox sourceBox = new();
    private readonly TextBox outputBox = new();
    private readonly ComboBox formatBox = new();
    private readonly ComboBox qualityBox = new();
    private readonly Button convertButton = new();
    private readonly ProgressBar progress = new();
    private readonly Label status = new();
    private static readonly HttpClient Downloader = new() { Timeout = TimeSpan.FromMinutes(30) };
    private const string FfmpegUrl = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip";
    private const string FfmpegSha256 = "60F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BA";
    private Process? currentProcess;
    private bool closing;

    public ConverterWindow()
    {
        Text = "C0_0nverter — конвертер медиа";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 585);
        MinimumSize = new Size(700, 550);
        BackColor = Color.FromArgb(245, 247, 242);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Icon = SystemIcons.Application;
        BuildUi();
        FormClosing += HandleClosing;
    }

    private void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(28, 17, 28, 12), BackColor = BackColor };
        Controls.Add(header);
        var logo = new Label { Text = "↔", TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(32, 48, 32), BackColor = Color.FromArgb(185, 245, 104), Location = new Point(28, 17), Size = new Size(42, 42) };
        header.Controls.Add(logo);
        header.Controls.Add(new Label { Text = "C0_0nverter", AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(32, 37, 31), Location = new Point(82, 22) });
        header.Controls.Add(new Label { Text = "ЛОКАЛЬНЫЙ КОНВЕРТЕР", AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(125, 135, 121), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(545, 32) });

        var title = new Label { Text = "Ваши файлы. Нужный формат.", AutoSize = true, Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.FromArgb(32, 37, 31), Location = new Point(30, 91) };
        Controls.Add(title);
        Controls.Add(new Label { Text = "Аудио и видео перекодируются через FFmpeg прямо на этом компьютере.", AutoSize = true, Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(116, 125, 112), Location = new Point(32, 137) });

        var card = new Panel { Location = new Point(28, 173), Size = new Size(704, 326), BackColor = Color.White, Padding = new Padding(22), BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(card);
        AddCaption(card, "ИСХОДНЫЙ ФАЙЛ", 22, 18);
        sourceBox.Location = new Point(22, 46); sourceBox.Size = new Size(518, 34); StyleTextBox(sourceBox); card.Controls.Add(sourceBox);
        var browse = MakeButton("Обзор…", true); browse.Location = new Point(550, 44); browse.Size = new Size(126, 37); browse.Click += (_, _) => PickSource(); card.Controls.Add(browse);

        AddCaption(card, "ФОРМАТ РЕЗУЛЬТАТА", 22, 101);
        AddCaption(card, "КАЧЕСТВО", 366, 101);
        formatBox.Location = new Point(22, 128); formatBox.Size = new Size(320, 36); formatBox.DropDownStyle = ComboBoxStyle.DropDownList; formatBox.Font = new Font("Segoe UI", 10); formatBox.Items.AddRange(VideoFormats.Concat(AudioFormats).Cast<object>().ToArray()); formatBox.SelectedIndex = 0; formatBox.SelectedIndexChanged += (_, _) => UpdateOutputExtension(); card.Controls.Add(formatBox);
        qualityBox.Location = new Point(366, 128); qualityBox.Size = new Size(310, 36); qualityBox.DropDownStyle = ComboBoxStyle.DropDownList; qualityBox.Font = new Font("Segoe UI", 10); qualityBox.Items.AddRange(Qualities.Cast<object>().ToArray()); qualityBox.SelectedIndex = 1; card.Controls.Add(qualityBox);

        AddCaption(card, "СОХРАНИТЬ КАК", 22, 185);
        outputBox.Location = new Point(22, 212); outputBox.Size = new Size(518, 34); StyleTextBox(outputBox); card.Controls.Add(outputBox);
        var saveAs = MakeButton("Изменить…", true); saveAs.Location = new Point(550, 210); saveAs.Size = new Size(126, 37); saveAs.Click += (_, _) => PickOutput(); card.Controls.Add(saveAs);
        card.Controls.Add(new Label { Text = "Выберите аудиоформат, чтобы извлечь звук из видео.", AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(139, 148, 133), Location = new Point(22, 269) });

        progress.Location = new Point(30, 516); progress.Size = new Size(700, 8); progress.Style = ProgressBarStyle.Marquee; progress.MarqueeAnimationSpeed = 26; progress.Visible = false; Controls.Add(progress);
        status.Text = "Выберите аудио- или видеофайл для начала"; status.Location = new Point(31, 535); status.Size = new Size(430, 24); status.ForeColor = Color.FromArgb(116, 125, 112); Controls.Add(status);
        convertButton.Text = "Конвертировать  ↗"; convertButton.Size = new Size(190, 45); convertButton.Location = new Point(540, 524); convertButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; convertButton.FlatStyle = FlatStyle.Flat; convertButton.FlatAppearance.BorderSize = 0; convertButton.BackColor = Color.FromArgb(185, 245, 104); convertButton.ForeColor = Color.FromArgb(32, 43, 30); convertButton.Font = new Font("Segoe UI", 10, FontStyle.Bold); convertButton.Cursor = Cursors.Hand; convertButton.Click += async (_, _) => await ConvertAsync(); Controls.Add(convertButton);
        Controls.Add(new Label { Text = "ФАЙЛЫ НЕ ПОКИДАЮТ ВАШ КОМПЬЮТЕР  ·  MP4  MKV  MOV  WEBM  MP3  WAV  FLAC  OGG  AAC", AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(153, 162, 147), Location = new Point(30, 570) });
    }

    private static void AddCaption(Control parent, string text, int x, int y) => parent.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(131, 141, 126), Location = new Point(x, y) });
    private static void StyleTextBox(TextBox box) { box.BorderStyle = BorderStyle.FixedSingle; box.Font = new Font("Segoe UI", 10); box.BackColor = Color.FromArgb(248, 249, 246); }
    private static Button MakeButton(string text, bool light) => new() { Text = text, FlatStyle = FlatStyle.Flat, BackColor = light ? Color.FromArgb(240, 243, 235) : Color.FromArgb(185, 245, 104), ForeColor = Color.FromArgb(41, 53, 42), Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false, FlatAppearance = { BorderSize = 0 } };

    private void PickSource()
    {
        using var dialog = new OpenFileDialog { Title = "Выберите аудио- или видеофайл", Filter = "Медиафайлы|*.mp4;*.mkv;*.mov;*.webm;*.mp3;*.wav;*.flac;*.ogg;*.aac;*.avi;*.m4a;*.wma|Все файлы|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        sourceBox.Text = dialog.FileName;
        UpdateOutputExtension();
        status.Text = $"Выбран файл: {Path.GetFileName(dialog.FileName)}";
    }

    private void PickOutput()
    {
        var ext = GetExtension();
        using var dialog = new SaveFileDialog { Title = "Сохранить результат", FileName = outputBox.Text.Length > 0 ? Path.GetFileName(outputBox.Text) : "converted." + ext, InitialDirectory = Directory.Exists(Path.GetDirectoryName(outputBox.Text)) ? Path.GetDirectoryName(outputBox.Text) : "", DefaultExt = ext, AddExtension = true, Filter = $"{formatBox.Text} файл|*.{ext}|Все файлы|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) outputBox.Text = dialog.FileName;
    }

    private void UpdateOutputExtension()
    {
        if (string.IsNullOrWhiteSpace(sourceBox.Text)) return;
        outputBox.Text = Path.ChangeExtension(sourceBox.Text, GetExtension());
    }

    private string GetExtension() => formatBox.Text switch { "WebM" => "webm", "MP4" => "mp4", "MKV" => "mkv", "MOV" => "mov", "MP3" => "mp3", "WAV" => "wav", "FLAC" => "flac", "OGG" => "ogg", "AAC" => "aac", _ => "mp4" };

    private async Task ConvertAsync()
    {
        var source = sourceBox.Text.Trim().Trim('"');
        var output = outputBox.Text.Trim().Trim('"');
        if (!File.Exists(source)) { MessageBox.Show(this, "Выберите существующий исходный файл.", "Нет файла", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (string.IsNullOrWhiteSpace(output)) { MessageBox.Show(this, "Укажите имя выходного файла.", "Нет пути", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "Выходной файл не может перезаписать исходный.", "Пути совпадают", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (File.Exists(output) && MessageBox.Show(this, $"Файл уже существует:\n{output}\n\nЗаменить его?", "Заменить файл?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Ошибка папки", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        var format = formatBox.Text;
        var (muxer, args) = GetCodecSettings(format, qualityBox.SelectedIndex);
        var temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, $".{Path.GetFileNameWithoutExtension(output)}.c0_0nverter-tmp.{GetExtension()}");
        try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        convertButton.Enabled = false; progress.Visible = true; status.Text = "Идёт перекодирование… Это может занять несколько минут.";
        var errors = new StringBuilder();
        try
        {
            var ffmpeg = await GetFfmpegAsync();
            if (closing) return;
            var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-y", "-i", source, "-map", "0:v:0?", "-map", "0:a:0?" }.Concat(args).Concat(["-f", muxer, temp])) start.ArgumentList.Add(arg);
            status.Text = "Идёт перекодирование… Это может занять несколько минут.";
            using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
            currentProcess = process;
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (errors) errors.AppendLine(e.Data); };
            process.Start(); process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            currentProcess = null;
            if (closing) return;
            if (process.ExitCode != 0 || !File.Exists(temp) || new FileInfo(temp).Length == 0)
            {
                string detail; lock (errors) detail = errors.ToString().Trim();
                var last = detail.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "FFmpeg не смог обработать этот файл.";
                throw new InvalidOperationException(last);
            }
            File.Move(temp, output, true);
            status.Text = "Готово — файл перекодирован и сохранён.";
            MessageBox.Show(this, $"Файл успешно перекодирован:\n{output}", "Конвертация завершена", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            currentProcess = null;
            if (!closing) { status.Text = "Не удалось преобразовать файл."; MessageBox.Show(this, ex.Message, "Ошибка конвертации", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            if (!closing) { progress.Visible = false; convertButton.Enabled = true; }
        }
    }

    private static (string Muxer, string[] Args) GetCodecSettings(string format, int quality)
    {
        string[] levels = ["19", "23", "28"];
        string[] bitrates = ["256k", "192k", "128k"];
        string[] muxer; string[] args;
        switch (format)
        {
            case "MP4": muxer = ["mp4"]; args = ["-c:v", "libx264", "-preset", "medium", "-crf", levels[quality], "-c:a", "aac", "-b:a", bitrates[quality], "-movflags", "+faststart"]; break;
            case "MKV": muxer = ["matroska"]; args = ["-c:v", "libx264", "-preset", "medium", "-crf", levels[quality], "-c:a", "aac", "-b:a", bitrates[quality]]; break;
            case "MOV": muxer = ["mov"]; args = ["-c:v", "libx264", "-preset", "medium", "-crf", levels[quality], "-c:a", "aac", "-b:a", bitrates[quality]]; break;
            case "WebM": muxer = ["webm"]; args = ["-c:v", "libvpx-vp9", "-crf", new[] { "26", "32", "40" }[quality], "-b:v", "0", "-c:a", "libopus", "-b:a", new[] { "192k", "128k", "80k" }[quality]]; break;
            case "MP3": muxer = ["mp3"]; args = ["-vn", "-c:a", "libmp3lame", "-b:a", bitrates[quality]]; break;
            case "WAV": muxer = ["wav"]; args = ["-vn", "-c:a", "pcm_s16le"]; break;
            case "FLAC": muxer = ["flac"]; args = ["-vn", "-c:a", "flac"]; break;
            case "OGG": muxer = ["ogg"]; args = ["-vn", "-c:a", "libvorbis", "-q:a", new[] { "7", "5", "3" }[quality]]; break;
            default: muxer = ["adts"]; args = ["-vn", "-c:a", "aac", "-b:a", bitrates[quality]]; break;
        }
        return (muxer[0], args);
    }

    private async Task<string> GetFfmpegAsync()
    {
        var installed = FindFfmpegOnPath();
        if (installed is not null) return installed;

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C0_0nverter");
        Directory.CreateDirectory(folder);
        var executable = Path.Combine(folder, "ffmpeg.exe");
        if (File.Exists(executable) && new FileInfo(executable).Length > 10_000_000) return executable;

        status.Text = "Первый запуск: загружаю FFmpeg (около 110 МБ)…";
        var archivePath = Path.Combine(folder, "ffmpeg-download.zip");
        var partialPath = archivePath + ".part";
        try
        {
            using (var response = await Downloader.GetAsync(FfmpegUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(partialPath);
                await response.Content.CopyToAsync(file);
            }
            string hash;
            await using (var downloaded = File.OpenRead(partialPath))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(downloaded));
            if (!string.Equals(hash, FfmpegSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Проверка контрольной суммы FFmpeg не прошла. Удалите загруженный файл и повторите попытку.");
            File.Move(partialPath, archivePath, true);
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase) || e.FullName.Equals("bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
            if (entry is null) throw new InvalidDataException("В загруженном архиве не найден ffmpeg.exe.");
            var extracted = executable + ".new";
            await using (var input = entry.Open())
            await using (var output = File.Create(extracted))
                await input.CopyToAsync(output);
            File.Move(extracted, executable, true);
            File.Delete(archivePath);
            status.Text = "FFmpeg готов. Начинаю перекодирование…";
            return executable;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(partialPath)) File.Delete(partialPath); } catch { }
            throw new InvalidOperationException("Не удалось подготовить FFmpeg. Проверьте подключение к интернету и повторите попытку.\n\n" + ex.Message, ex);
        }
    }

    private static string? FindFfmpegOnPath()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), "ffmpeg.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private void HandleClosing(object? sender, FormClosingEventArgs e)
    {
        if (currentProcess is { HasExited: false } process)
        {
            if (MessageBox.Show(this, "Конвертация ещё выполняется. Прервать её и закрыть приложение?", "Выполняется конвертация", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            { e.Cancel = true; return; }
            try { process.Kill(true); } catch { }
        }
        closing = true;
    }
}
