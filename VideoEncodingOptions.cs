using System.Runtime.InteropServices;

namespace C0_0nverter;

internal sealed class VideoEncodingOptions
{
    public string VideoCodec { get; set; } = "libx264";
    public bool UseBitrate { get; set; }
    public int Crf { get; set; } = 23;
    public string VideoBitrate { get; set; } = "5M";
    public string SpeedPreset { get; set; } = "medium";
    public string Height { get; set; } = "Исходное";
    public string FrameRate { get; set; } = "Исходная";
    public string PixelFormat { get; set; } = "Авто";
    public string AudioCodec { get; set; } = "aac";
    public string AudioBitrate { get; set; } = "192k";
    public VideoEncodingOptions Copy() => (VideoEncodingOptions)MemberwiseClone();
}

internal sealed class VideoSettingsDialog : Form
{
    private static readonly Color Surface = Color.FromArgb(36, 38, 41);
    private static readonly Color Field = Color.FromArgb(47, 49, 53);
    private static readonly Color TextColor = Color.FromArgb(237, 238, 240);
    private readonly string container;
    private readonly ComboBox codec = new();
    private readonly ComboBox speed = new();
    private readonly ComboBox rateMode = new();
    private readonly NumericUpDown crf = new();
    private readonly TextBox bitrate = new();
    private readonly ComboBox height = new();
    private readonly ComboBox fps = new();
    private readonly ComboBox pixel = new();
    private readonly ComboBox audioCodec = new();
    private readonly ComboBox audioBitrate = new();
    private readonly Label rateLabel = new();
    private readonly Label speedLabel = new();
    private VideoEncodingOptions current;

    public VideoEncodingOptions Result { get; private set; }

    public VideoSettingsDialog(VideoEncodingOptions settings, string outputContainer)
    {
        current = settings.Copy();
        Result = settings.Copy();
        container = outputContainer;
        Text = "Подробные настройки кодирования";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(700, 600);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(28, 29, 31);
        Font = new Font("Segoe UI", 9F);
        HandleCreated += (_, _) => { try { var enabled = 1; DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)); } catch { } };
        BuildUi();
        LoadSettings(current);
    }

    private void BuildUi()
    {
        Controls.Add(new Label { Text = "Настройки видео", AutoSize = true, Font = new Font("Segoe UI", 19, FontStyle.Bold), ForeColor = TextColor, Location = new Point(26, 20) });
        var card = new Panel { Location = new Point(22, 88), Size = new Size(656, 430), BackColor = Surface, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(card);

        Caption(card, "ВИДЕОКОДЕК", 20, 18);
        speedLabel.AutoSize = true; speedLabel.Font = new Font("Segoe UI", 8, FontStyle.Bold); speedLabel.ForeColor = Color.FromArgb(174, 176, 181); speedLabel.Location = new Point(345, 18); card.Controls.Add(speedLabel);
        Combo(codec, 20, 43, 300); Combo(speed, 345, 43, 285);
        card.Controls.Add(codec); card.Controls.Add(speed);
        codec.Items.AddRange(AvailableVideoCodecs().Cast<object>().ToArray());
        codec.SelectedIndexChanged += (_, _) => UpdateCodecControls(true);

        Caption(card, "РЕЖИМ КАЧЕСТВА", 20, 101); rateLabel.Text = "CRF / CQ"; rateLabel.AutoSize = true; rateLabel.Font = new Font("Segoe UI", 8, FontStyle.Bold); rateLabel.ForeColor = Color.FromArgb(174, 176, 181); rateLabel.Location = new Point(345, 101); card.Controls.Add(rateLabel);
        Combo(rateMode, 20, 126, 300); rateMode.Items.AddRange(["Постоянное качество (CRF)", "Целевой битрейт"]); card.Controls.Add(rateMode);
        crf.Location = new Point(345, 127); crf.Size = new Size(120, 32); crf.Minimum = 0; crf.Maximum = 63; crf.Value = 23; crf.BackColor = Field; crf.ForeColor = TextColor; crf.BorderStyle = BorderStyle.FixedSingle; card.Controls.Add(crf);
        bitrate.Location = new Point(345, 127); bitrate.Size = new Size(285, 32); bitrate.Font = new Font("Segoe UI", 10); bitrate.BackColor = Field; bitrate.ForeColor = TextColor; bitrate.BorderStyle = BorderStyle.FixedSingle; bitrate.Text = "5M"; bitrate.Visible = false; card.Controls.Add(bitrate);
        rateMode.SelectedIndexChanged += (_, _) => UpdateRateMode();
        card.Controls.Add(new Label { Text = "Меньше CRF = выше качество и больше размер. Битрейт укажите, например, 5M или 8000k.", AutoSize = false, Size = new Size(605, 28), Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(155, 157, 162), Location = new Point(20, 166) });

        Caption(card, "ВЫСОТА КАДРА", 20, 211); Caption(card, "ЧАСТОТА КАДРОВ", 226, 211); Caption(card, "ЦВЕТНОСТЬ", 432, 211);
        Combo(height, 20, 236, 185); height.Items.AddRange(["Исходная", "2160 (4K)", "1440 (QHD)", "1080 (Full HD)", "720 (HD)", "480"]); card.Controls.Add(height);
        Combo(fps, 226, 236, 185); fps.Items.AddRange(["Исходная", "23.976", "24", "25", "30", "50", "60"]); card.Controls.Add(fps);
        Combo(pixel, 432, 236, 198); pixel.Items.AddRange(["Авто", "yuv420p (8-bit)", "yuv420p10le (10-bit)"]); card.Controls.Add(pixel);

        Caption(card, "АУДИОКОДЕК", 20, 294); Caption(card, "БИТРЕЙТ АУДИО", 345, 294);
        Combo(audioCodec, 20, 319, 300); audioCodec.Items.AddRange(AvailableAudioCodecs().Cast<object>().ToArray()); card.Controls.Add(audioCodec);
        Combo(audioBitrate, 345, 319, 285); audioBitrate.Items.AddRange(["96k", "128k", "160k", "192k", "256k", "320k"]); card.Controls.Add(audioBitrate);
        audioCodec.SelectedIndexChanged += (_, _) => audioBitrate.Enabled = audioCodec.SelectedItem?.ToString() is not ("Без звука" or "Копировать исходную дорожку" or "FLAC");
        card.Controls.Add(new Label { Text = "Параметры видео применяются к контейнерам MP4, MKV, MOV и WebM. Для WebM доступны AV1/VP9 и Opus.", AutoSize = false, Size = new Size(605, 38), Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(155, 157, 162), Location = new Point(20, 374) });

        var cancel = Button("Отмена", false); cancel.Location = new Point(400, 540); cancel.Size = new Size(120, 40); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel);
        var apply = Button("Применить", true); apply.Location = new Point(533, 540); apply.Size = new Size(145, 40); apply.Click += (_, _) => ApplySettings(); Controls.Add(apply);
        AcceptButton = apply; CancelButton = cancel;
    }

    private string[] AvailableVideoCodecs() => container == "WebM"
        ? ["AV1 (libaom-av1)", "VP9 (libvpx-vp9)"]
        : ["H.264 / AVC (libx264)", "H.265 / HEVC (libx265)", "AV1 (libaom-av1)", "VP9 (libvpx-vp9)"];

    private string[] AvailableAudioCodecs() => container switch
    {
        "WebM" => ["Opus", "Без звука"],
        "MP4" or "MOV" => ["AAC", "MP3", "Без звука", "Копировать исходную дорожку"],
        _ => ["AAC", "Opus", "MP3", "FLAC", "Без звука", "Копировать исходную дорожку"]
    };

    private static void Caption(Control parent, string text, int x, int y) => parent.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(174, 176, 181), BackColor = Surface, Location = new Point(x, y) });

    private static void Combo(ComboBox box, int x, int y, int width)
    {
        box.Location = new Point(x, y); box.Size = new Size(width, 34); box.DropDownStyle = ComboBoxStyle.DropDownList; box.Font = new Font("Segoe UI", 9); box.BackColor = Field; box.ForeColor = TextColor; box.FlatStyle = FlatStyle.Flat; box.DrawMode = DrawMode.OwnerDrawFixed; box.ItemHeight = 23;
        box.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            using var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(68, 70, 75) : Field);
            e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, box.Items[e.Index]?.ToString() ?? "", box.Font, e.Bounds, TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
    }

    private static Button Button(string text, bool primary) => new() { Text = text, FlatStyle = FlatStyle.Flat, BackColor = primary ? Color.FromArgb(67, 69, 74) : Color.FromArgb(54, 56, 60), ForeColor = TextColor, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false, FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(79, 81, 86) } };

    private void LoadSettings(VideoEncodingOptions value)
    {
        var codecs = AvailableVideoCodecs();
        var wanted = value.VideoCodec switch { "libx265" => "H.265 / HEVC (libx265)", "libaom-av1" => "AV1 (libaom-av1)", "libvpx-vp9" => "VP9 (libvpx-vp9)", _ => "H.264 / AVC (libx264)" };
        codec.SelectedItem = codecs.Contains(wanted) ? wanted : codecs[0];
        speed.SelectedItem = value.SpeedPreset;
        rateMode.SelectedIndex = value.UseBitrate ? 1 : 0;
        crf.Value = Math.Min(crf.Maximum, Math.Max(crf.Minimum, value.Crf));
        bitrate.Text = value.VideoBitrate;
        height.SelectedItem = value.Height;
        if (height.SelectedIndex < 0) height.SelectedIndex = 0;
        fps.SelectedItem = value.FrameRate;
        if (fps.SelectedIndex < 0) fps.SelectedIndex = 0;
        pixel.SelectedItem = value.PixelFormat;
        if (pixel.SelectedIndex < 0) pixel.SelectedIndex = 0;
        audioCodec.SelectedItem = value.AudioCodec switch { "libopus" => "Opus", "libmp3lame" => "MP3", "flac" => "FLAC", "none" => "Без звука", "copy" => "Копировать исходную дорожку", _ => "AAC" };
        if (audioCodec.SelectedIndex < 0) audioCodec.SelectedIndex = 0;
        audioBitrate.SelectedItem = value.AudioBitrate;
        if (audioBitrate.SelectedIndex < 0) audioBitrate.SelectedItem = "192k";
        UpdateCodecControls(false);
        UpdateRateMode();
    }

    private void UpdateCodecControls(bool resetSpeed)
    {
        if (codec.SelectedItem is null) return;
        var isAom = codec.SelectedItem.ToString()!.StartsWith("AV1", StringComparison.Ordinal);
        var isVp9 = codec.SelectedItem.ToString()!.StartsWith("VP9", StringComparison.Ordinal);
        speedLabel.Text = isAom || isVp9 ? "CPU-USED · БОЛЬШЕ = БЫСТРЕЕ" : "ПРЕСЕТ КОДЕКА";
        speed.Items.Clear();
        if (isAom || isVp9) speed.Items.AddRange(Enumerable.Range(0, 9).Select(x => (object)x.ToString()).ToArray());
        else speed.Items.AddRange(["ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"]);
        if (resetSpeed || speed.SelectedIndex < 0)
            speed.SelectedItem = isAom || isVp9 ? "5" : "medium";
        crf.Maximum = isAom || isVp9 ? 63 : 51;
    }

    private void UpdateRateMode()
    {
        var byBitrate = rateMode.SelectedIndex == 1;
        crf.Visible = !byBitrate;
        bitrate.Visible = byBitrate;
        rateLabel.Text = byBitrate ? "ВИДЕОБИТРЕЙТ" : "CRF / CQ";
    }

    private void ApplySettings()
    {
        var bitrateValue = bitrate.Text.Trim();
        if (rateMode.SelectedIndex == 1 && !System.Text.RegularExpressions.Regex.IsMatch(bitrateValue, "^[1-9][0-9]*([kKmM])?$"))
        {
            MessageBox.Show(this, "Введите битрейт, например 5M или 8000k.", "Неверный битрейт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var selectedCodec = codec.SelectedItem?.ToString() ?? "H.264 / AVC (libx264)";
        var selectedAudio = audioCodec.SelectedItem?.ToString() ?? "AAC";
        current.VideoCodec = selectedCodec.Contains("libx265") ? "libx265" : selectedCodec.Contains("libaom-av1") ? "libaom-av1" : selectedCodec.Contains("libvpx-vp9") ? "libvpx-vp9" : "libx264";
        current.UseBitrate = rateMode.SelectedIndex == 1;
        current.Crf = (int)crf.Value;
        current.VideoBitrate = bitrateValue;
        current.SpeedPreset = speed.SelectedItem?.ToString() ?? "medium";
        current.Height = height.SelectedItem?.ToString() ?? "Исходная";
        current.FrameRate = fps.SelectedItem?.ToString() ?? "Исходная";
        current.PixelFormat = pixel.SelectedItem?.ToString() ?? "Авто";
        current.AudioCodec = selectedAudio switch { "Opus" => "libopus", "MP3" => "libmp3lame", "FLAC" => "flac", "Без звука" => "none", "Копировать исходную дорожку" => "copy", _ => "aac" };
        current.AudioBitrate = audioBitrate.SelectedItem?.ToString() ?? "192k";
        Result = current.Copy();
        DialogResult = DialogResult.OK;
        Close();
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
