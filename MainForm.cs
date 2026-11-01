using System.Security.Cryptography;
using System.Text;
using static CachyDualBoot.Theme;

namespace CachyDualBoot;

sealed class MainForm : Form
{
    const long GiB = 1073741824;
    const string IsoLabel = "LINUXISO";

    static readonly string[] StepTitles = { "Sistem kontrolü", "ISO seçimi", "Yapılandırma", "Uygulama", "Kurulum rehberi" };
    static readonly string[] StepSubs = { "UEFI, GPT, BitLocker", "Dosya ve SHA256", "Alan ve tercihler", "Bölüm ve kopyalama", "Sonraki adımlar" };
    static readonly string[] PageHeads =
    {
        "Sistem kontrolü", "ISO seçimi ve doğrulama", "Yapılandırma", "Özet ve uygulama", "Kurulum rehberi",
    };
    static readonly string[] PageSubs =
    {
        "Bilgisayarınızın bu yönteme uygun olup olmadığına bakıyoruz.",
        "Kurmak istediğiniz Linux ISO dosyasını seçin ve bütünlüğünü doğrulayın.",
        "Linux'a ne kadar alan ayıracağınızı ve kurulum tercihlerinizi belirleyin.",
        "Yapılacakları gözden geçirin ve uygulayın.",
        "Seçimlerinize göre hazırlanan adım adım yönergeler.",
    };

    readonly Sidebar _side = new() { Dock = DockStyle.Left };
    readonly Panel _main = new() { Dock = DockStyle.Fill };
    readonly Panel _header = new() { Dock = DockStyle.Top };
    readonly Panel _footer = new() { Dock = DockStyle.Bottom };
    readonly Panel _status = new() { Dock = DockStyle.Bottom };
    readonly Label _statL = new() { Font = Small, ForeColor = Muted, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
    readonly Label _statR = new() { Font = Small, ForeColor = Muted, AutoSize = false, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false };
    Action? _footerLayout;
    readonly Panel _content = new() { Dock = DockStyle.Fill };
    readonly Label _title = new() { Font = H1, ForeColor = Fg, AutoSize = false, UseMnemonic = false };
    readonly Label _sub = new() { Font = Body, ForeColor = Muted, AutoSize = false, UseMnemonic = false };
    readonly Chip _stepChip = new() { Anchor = AnchorStyles.Top | AnchorStyles.Right };
    readonly Label _hint = new() { Font = Small, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
    readonly PillButton _back = new() { Text = "< Geri" };
    readonly PillButton _next = new() { Text = "İleri >", Primary = true };
    readonly PillButton _cancel = new() { Text = "İptal" };
    int _page;
    bool _busy;

    // durum
    SysInfo? _sys;
    string _iso = "";
    long _isoContentBytes;
    string _isoDetectedLabel = "";
    string _bcdGuid = "";
    const string BootEntryName = "Linux Live (LINUXISO)";
    bool _isoOk;
    bool _applied;
    string _hashResult = "";

    // yapılandırma
    decimal _linuxGb = 40;
    string _fs = "ext4";
    string _swap = "zram";
    bool _disableFast = true;
    bool _backup, _bitlockerKey, _secureBoot, _dryRun = true;

    long IsoPartGb => Math.Max(3, (_isoContentBytes + GiB - 1) / GiB + 1);
    long TotalBytes => ((long)_linuxGb + IsoPartGb) * GiB;

    public MainForm()
    {
        Text = "Linux Dual Boot Sihirbazı (USB'siz)";
        BackColor = Bg;
        ForeColor = Fg;
        Font = Body;
        StartPosition = FormStartPosition.CenterScreen;
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
        ClientSize = new Size(Math.Min(Dp(1080), wa.Width - Dp(40)), Math.Min(Dp(700), wa.Height - Dp(48)));
        MinimumSize = new Size(Math.Min(Dp(940), wa.Width), Math.Min(Dp(660), wa.Height));

        _side.Width = Dp(236);
        _side.Titles = StepTitles;
        _side.Subs = StepSubs;
        _side.Footer = "Yönetici olarak çalışıyor";
        _side.CanNavigate = i => i < _page && CanGoBack(_page);
        _side.StepClicked += i => ShowPage(i);

        _main.BackColor = Bg;

        // üst bant: beyaz, altında ince çizgi
        _header.Height = Dp(72);
        _header.BackColor = Paper;
        _title.SetBounds(Dp(24), Dp(10), Dp(600), Dp(28));
        _sub.SetBounds(Dp(24), Dp(40), Dp(760), Dp(22));
        _title.BackColor = Paper; _sub.BackColor = Paper;
        _stepChip.BackColor = Paper;
        _stepChip.Height = Dp(30);
        _header.Controls.AddRange(new Control[] { _title, _sub, _stepChip });
        _header.Resize += (_, _) => _stepChip.Location = new Point(_header.Width - _stepChip.Width - Dp(24), Dp(21));
        _header.Paint += (_, e) => { using var p = new Pen(Line); e.Graphics.DrawLine(p, 0, _header.Height - 1, _header.Width, _header.Height - 1); };

        // durum çubuğu
        _status.Height = Dp(26);
        _status.BackColor = Side;
        _statL.SetBounds(Dp(24), 0, Dp(500), Dp(26));
        _statR.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _statR.SetBounds(0, 0, Dp(320), Dp(26));
        _status.Controls.AddRange(new Control[] { _statL, _statR });
        _status.Resize += (_, _) => _statR.Left = _status.Width - _statR.Width - Dp(24);
        _status.Paint += (_, e) => { using var p = new Pen(Line); e.Graphics.DrawLine(p, 0, 0, _status.Width, 0); };

        // düğme çubuğu
        _footer.Height = Dp(56);
        _footer.BackColor = Bg;
        _back.SetBounds(0, Dp(12), Dp(100), Dp(32));
        _next.SetBounds(0, Dp(12), Dp(100), Dp(32));
        _cancel.SetBounds(0, Dp(12), Dp(100), Dp(32));
        _hint.Top = Dp(6); _hint.Height = Dp(44);
        _footer.Controls.AddRange(new Control[] { _back, _hint, _next, _cancel });
        _footer.Paint += (_, e) => { using var p = new Pen(Line); e.Graphics.DrawLine(p, 0, 0, _footer.Width, 0); };
        void LayoutFooter()
        {
            int x = _footer.Width - Dp(24);
            if (_cancel.Visible) { x -= _cancel.Width; _cancel.Left = x; x -= Dp(14); }
            x -= _next.Width; _next.Left = x;
            x -= Dp(8) + _back.Width; _back.Left = x;
            _hint.Left = Dp(24);
            _hint.Width = Math.Max(50, _back.Left - Dp(16) - _hint.Left);
        }
        _footerLayout = LayoutFooter;
        _footer.Resize += (_, _) => LayoutFooter();

        _content.BackColor = Bg;
        _content.Padding = new Padding(Dp(24), Dp(16), Dp(24), Dp(12));

        _main.Controls.Add(_header); _header.BringToFront();
        _main.Controls.Add(_status); _status.BringToFront();
        _main.Controls.Add(_footer); _footer.BringToFront();
        _main.Controls.Add(_content); _content.BringToFront();

        Controls.Add(_side); _side.BringToFront();
        Controls.Add(_main); _main.BringToFront();

        _back.Click += (_, _) => { if (CanGoBack(_page)) ShowPage(_page - 1); };
        _next.Click += async (_, _) => await NextAsync();
        _cancel.Click += (_, _) =>
        {
            if (_busy) { Notify("İşlem sürerken çıkılamaz. Lütfen bitmesini bekleyin.", Level.Warn); return; }
            if (MessageBox.Show(this, "Sihirbazdan çıkmak istiyor musunuz?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Close();
        };
        Shown += (_, _) => { LayoutFooter(); ShowPage(0); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        LightTitleBar(Handle);
    }

    // ---------- yardımcılar ----------
    static Label Lbl(string t, Font f, Color c, ContentAlignment a = ContentAlignment.MiddleLeft)
        => new() { Text = t, Font = f, ForeColor = c, AutoSize = false, TextAlign = a, UseMnemonic = false };

    static T Stack<T>(Control parent, T c, int h) where T : Control
    {
        c.Dock = DockStyle.Top; c.Height = Dp(h);
        parent.Controls.Add(c); c.BringToFront();
        return c;
    }
    static void Gap(Control parent, int h) => Stack(parent, new Panel(), h);
    static T Fill<T>(Control parent, T c) where T : Control
    {
        c.Dock = DockStyle.Fill;
        parent.Controls.Add(c); c.BringToFront();
        return c;
    }
    static Label CardTitle(Control card, string text) => Stack(card, Lbl(text, Bold, Fg), 28);

    static TableLayoutPanel Grid(params float[] cols)
    {
        var t = new TableLayoutPanel { ColumnCount = cols.Length, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        foreach (var c in cols)
            t.ColumnStyles.Add(c >= 0 ? new ColumnStyle(SizeType.Percent, c) : new ColumnStyle(SizeType.Absolute, Dp((int)-c)));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return t;
    }
    static void Cell(TableLayoutPanel t, Control c, int col)
    {
        c.Dock = DockStyle.Fill; c.Margin = Padding.Empty;
        t.Controls.Add(c, col, 0);
    }

    static string Gb(long bytes) => $"{bytes / (double)GiB:F1} GB";

    void Notify(string msg, Level lvl = Level.Info)
    {
        _hint.Text = msg;
        _hint.ForeColor = lvl switch { Level.Ok => Ok, Level.Warn => Warn, Level.Bad => Bad, _ => Muted };
    }

    bool CanGoBack(int page) => page > 0 && page < 4 && !_busy && !(page == 3 && _applied && !_dryRun);

    void UpdateNav()
    {
        _back.Enabled = CanGoBack(_page);
        _next.Enabled = !_busy;
        _next.Text = _page == 4 ? "Bitir" : "İleri >";
        _cancel.Visible = _page < 4;
        _footerLayout?.Invoke();
        _side.Current = _page;
        _side.Invalidate();
        _statL.Text = _busy ? "Çalışıyor…"
            : _sys == null ? "Hazır"
            : $"Disk {_sys.DiskNumber}  ·  {(_sys.Uefi ? "UEFI" : "Legacy")}  ·  {_sys.PartStyle}  ·  C: {Gb(_sys.CSize)}";
        _statR.Text = _dryRun ? "Mod: deneme (diskte değişiklik yapılmaz)" : "Mod: gerçek uygulama";
        _statR.ForeColor = _dryRun ? Muted : Bad;
    }

    async Task NextAsync()
    {
        if (_busy) return;
        var err = _page switch
        {
            0 => _sys == null || !_sys.Uefi || _sys.PartStyle != "GPT" ? "Sistem UEFI + GPT olmalı. Bu araç yalnızca bu yapıyı destekler." : null,
            1 => !_isoOk ? "Önce ISO'yu seçip «Doğrula» düğmesiyle denetleyin; denetim başarılı olmalı." : null,
            2 => ValidateConfig(),
            3 => !_applied ? "Önce «Uygula» adımını tamamlayın." : null,
            _ => null,
        };
        if (err != null) { Notify(err, Level.Warn); return; }
        if (_page == 4) { Close(); return; }
        ShowPage(_page + 1);
        await Task.CompletedTask;
    }

    string? ValidateConfig()
    {
        if (_sys == null) return "Sistem bilgisi yok.";
        if (_linuxGb < 30) return "Linux için en az 30 GB ayırın.";
        if (!_backup) return "Yedek aldığınızı onaylamadan devam edemezsiniz.";
        if (!_secureBoot) return "Secure Boot'un kapatılması gerektiğini onaylayın.";
        if (_sys.BitLockerActive && !_bitlockerKey) return "BitLocker etkin görünüyor: kurtarma anahtarını kaydettiğinizi onaylayın.";
        var newC = _sys.CSize - TotalBytes;
        if (newC < _sys.CMin + GiB) return $"C: bu kadar küçültülemez. Windows'un izin verdiği en küçük boyut {_sys.CMin / GiB} GB (taşınamaz dosyalar nedeniyle). Daha az alan ayırın veya disk birleştirme yapın.";
        if (_sys.CFreeGb - TotalBytes / (double)GiB < 20) return "Küçültmeden sonra Windows'ta en az 20 GB boş alan kalmalı.";
        return null;
    }

    void ShowPage(int page)
    {
        _page = page;
        _title.Text = PageHeads[page];
        _sub.Text = PageSubs[page];
        _stepChip.Set($"Adım {page + 1} / {StepTitles.Length}", Level.Info);
        _stepChip.Location = new Point(_header.Width - _stepChip.Width - Dp(24), Dp(21));
        Notify("");

        _content.SuspendLayout();
        foreach (var old in _content.Controls.Cast<Control>().ToList()) { _content.Controls.Remove(old); old.Dispose(); }
        Control c = page switch { 0 => BuildSystemPage(), 1 => BuildIsoPage(), 2 => BuildConfigPage(), 3 => BuildApplyPage(), _ => BuildGuidePage() };
        c.BackColor = Bg;
        Fill(_content, c);
        _content.ResumeLayout(true);
        UpdateNav();
    }

    // ---------- Sayfa 1: sistem ----------
    Control BuildSystemPage()
    {
        var root = new Panel();

        var tiles = Grid(33.3f, -12, 33.3f, -12, 33.3f);
        var tSize = new StatTile(); var tFree = new StatTile(); var tMax = new StatTile();
        Cell(tiles, tSize, 0); Cell(tiles, tFree, 2); Cell(tiles, tMax, 4);
        tSize.Set("C: boyutu", "—"); tFree.Set("C: boş alan", "—"); tMax.Set("Ayrılabilecek en fazla", "—");
        Stack(root, tiles, 80);
        Gap(root, 12);

        var card = new Card { Padding = new Padding(Dp(18), Dp(14), Dp(18), Dp(14)) };
        CardTitle(card, "Uyumluluk kontrolleri");
        var prog = Stack(card, new ProgressLine { Indeterminate = true }, 8);
        var rBoot = Stack(card, new StatusRow(), 54);
        var rPart = Stack(card, new StatusRow(), 54);
        var rSb = Stack(card, new StatusRow(), 54);
        var rBl = Stack(card, new StatusRow { Separator = false }, 54);
        Stack(root, card, 14 * 2 + 28 + 8 + 54 * 4 + 4);
        Gap(root, 12);

        var bottom = Grid(100, -12, -200);
        var banner = new Banner();
        var refresh = new PillButton { Text = "Yeniden kontrol et" };
        Cell(bottom, banner, 0); Cell(bottom, refresh, 2);
        banner.Set(Level.Info, "Kontrol ediliyor…");
        Stack(root, bottom, 52);
        root.AutoScroll = true;
        root.AutoScrollMinSize = new Size(0, Dp(80 + 12 + 14 * 2 + 28 + 8 + 54 * 4 + 4 + 12 + 52 + 4));

        async Task Run()
        {
            prog.Visible = true;
            banner.Set(Level.Info, "Kontrol ediliyor…");
            refresh.Enabled = false;
            foreach (var r in new[] { rBoot, rPart, rSb, rBl }) r.Set(Level.Info, "Kontrol ediliyor…", "", "");
            try
            {
                var s = await SystemInfo.GatherAsync();
                if (root.IsDisposed) return;
                _sys = s;
                rBoot.Set(s.Uefi ? Level.Ok : Level.Bad, "Önyükleme modu", s.Uefi ? "UEFI" : "Legacy BIOS",
                    s.Uefi ? "Bu yöntem UEFI sistemlerde çalışır." : "Legacy BIOS desteklenmiyor.");
                rPart.Set(s.PartStyle == "GPT" ? Level.Ok : Level.Bad, $"Disk {s.DiskNumber} bölüm stili", s.PartStyle,
                    s.PartStyle == "GPT" ? "GPT disk — gerekli yapı." : "Yalnızca GPT diskler desteklenir.");
                rSb.Set(s.SecureBoot switch { true => Level.Warn, false => Level.Ok, _ => Level.Info }, "Secure Boot",
                    s.SecureBoot switch { true => "Açık", false => "Kapalı", _ => "Okunamadı" },
                    s.SecureBoot == true ? "Linux'u başlatmadan önce BIOS/UEFI'den kapatmanız gerekecek." : "Sorun yok.");
                rBl.Set(s.BitLockerActive ? Level.Warn : Level.Ok, "BitLocker (C:)", s.BitLocker,
                    s.BitLockerActive ? "Devam etmeden önce kurtarma anahtarınızı kaydedin." : "C: sürücüsü şifreli değil.");
                tSize.Set("C: boyutu", Gb(s.CSize));
                tFree.Set("C: boş alan", $"{s.CFreeGb:F1} GB");
                tMax.Set("Ayrılabilecek en fazla", Gb(s.CSize - s.CMin), $"Windows alt sınırı: {Gb(s.CMin)}");
                if (s.Uefi && s.PartStyle == "GPT")
                    banner.Set(Level.Ok, "Uygun. Bu sihirbaz hiçbir Windows bölümünü silmez ve EFI bölümüne dokunmaz.");
                else
                    banner.Set(Level.Bad, "Uygun DEĞİL. Devam edilemez.");
            }
            catch (Exception ex)
            {
                if (root.IsDisposed) return;
                foreach (var r in new[] { rBoot, rPart, rSb, rBl }) r.Set(Level.Info, "—", "", "");
                banner.Set(Level.Bad, "Hata: " + ex.Message.Split('\n')[0]);
            }
            prog.Visible = false;
            refresh.Enabled = true;
        }
        refresh.Click += async (_, _) => await Run();
        _ = Run();
        return root;
    }

    // ---------- Sayfa 2: ISO ----------
    Control BuildIsoPage()
    {
        var root = new Panel { AutoScroll = true };
        root.AutoScrollMinSize = new Size(0, Dp(112 + 16 + 22 + 44 + 14 + 44 + 14 + 120));
        var zone = new DropZone();
        Stack(root, zone, 96);
        Gap(root, 16);

        Stack(root, Lbl("Resmî sitedeki SHA256 değeri  (isteğe bağlı ama önerilir)", Small, Muted), 22);
        var txtHash = Stack(root, new Field(), 32);
        txtHash.Box.Font = Mono;
        txtHash.Placeholder = "örn. 9f86d081884c7d659a2feaa0c55ad015…";
        Gap(root, 14);

        var row = Grid(-280, 100);
        var verify = new PillButton { Text = "Doğrula  (SHA256 + ISO yapısı)", Primary = true };
        Cell(row, verify, 0);
        var prog = new ProgressLine { Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        row.Controls.Add(prog, 1, 0);
        prog.Dock = DockStyle.None; prog.Height = Dp(8);
        prog.Margin = new Padding(Dp(16), Dp(12), 0, 0);
        prog.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        Stack(root, row, 32);
        Gap(root, 14);

        var logCard = new Card(Input) { Padding = new Padding(Dp(14)) };
        var log = Fill(logCard, new LogBox());
        Fill(root, logCard);
        log.SetText(_hashResult);

        void SetFile(string path)
        {
            var size = File.Exists(path) ? new FileInfo(path).Length : 0;
            zone.Set(Path.GetFileName(path), $"{size / (double)GiB:F2} GB  ·  {path}", _isoOk ? Level.Ok : null);
        }
        if (_iso.Length > 0) SetFile(_iso);

        zone.BrowseRequested += (_, _) =>
        {
            if (_busy) return;
            using var dlg = new OpenFileDialog { Filter = "ISO dosyası|*.iso", Title = "Linux ISO dosyasını seçin" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _iso = dlg.FileName; _isoOk = false; _hashResult = "";
            log.Clear();
            prog.Value = 0;
            SetFile(_iso);
            Notify("");
        };

        verify.Click += async (_, _) =>
        {
            if (_busy) return;
            _isoOk = false;
            if (!File.Exists(_iso) || !_iso.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            {
                zone.SetState(Level.Bad, "Önce geçerli bir .iso dosyası seçin.");
                Notify("Geçerli bir .iso dosyası seçin.", Level.Warn);
                return;
            }
            _busy = true; UpdateNav(); verify.Enabled = false;
            log.Clear(); prog.Value = 0;
            var sb = new StringBuilder();
            void Say(string s) { sb.AppendLine(s); log.Add(s); }
            Level result = Level.Bad;
            try
            {
                var size = new FileInfo(_iso).Length;
                Say($"Dosya: {_iso}\nBoyut: {size / (double)GiB:F2} GB\nSHA256 hesaplanıyor ve ISO yapısı denetleniyor (paralel)…");
                var structTask = Ps.RunAsync(PreflightScript(_iso));
                var hash = await Task.Run(() =>
                {
                    using var fs = new FileStream(_iso, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
                    using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buf = new byte[4 << 20];
                    long done = 0; int n; int lastPct = -1;
                    while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                    {
                        sha.AppendData(buf, 0, n);
                        done += n;
                        var pct = (int)(done * 100 / size);
                        if (pct != lastPct) { lastPct = pct; BeginInvoke(() => prog.Value = pct); }
                    }
                    return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
                });
                prog.Value = 100;
                Say("SHA256: " + hash);
                var expected = txtHash.Text.Trim().ToLowerInvariant();
                if (expected.Length > 0)
                {
                    if (expected != hash) { await structTask; Say("✖ SHA256 EŞLEŞMİYOR. Dosya bozuk veya değiştirilmiş; yeniden indirin."); return; }
                    Say("✔ SHA256 resmî değerle eşleşti.");
                }
                else Say("⚠ Karşılaştırma değeri girilmedi; bütünlük doğrulanmadı.");

                Say("\nISO yapısı sonucu bekleniyor…");
                prog.Indeterminate = true;
                var (code, outp) = await structTask;
                prog.Indeterminate = false;
                if (code != 0) { Say("✖ ISO bağlanamadı/okunamadı:\n" + outp); return; }
                string Get(string key) => outp.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith(key + "=")).Select(l => l[(key.Length + 1)..]).FirstOrDefault() ?? "";
                var big = Get("BIG"); var efi = Get("EFI"); _isoDetectedLabel = Get("LABEL");
                long.TryParse(Get("SIZE"), out _isoContentBytes);
                if (big.Length > 0) { Say($"✖ 4 GB'dan büyük dosya var ({big}); FAT32'ye sığmaz. Bu yöntem bu ISO için çalışmaz."); return; }
                if (!efi.Equals("True", StringComparison.OrdinalIgnoreCase)) { Say("✖ EFI\\BOOT\\BOOTX64.EFI bulunamadı; ISO UEFI'den doğrudan başlatılamaz."); return; }
                Say("✔ EFI\\BOOT\\BOOTX64.EFI mevcut, 4 GB üstü dosya yok.");
                Say(_isoDetectedLabel.Length > 0
                    ? $"ℹ ISO'daki önyükleme etiketi: {_isoDetectedLabel} (kopyada '{IsoLabel}' olarak güncellenecek)"
                    : "⚠ Önyükleme etiketi bulunamadı; yapılandırma elle kontrol edilmeli.");
                Say($"ℹ İçerik: {_isoContentBytes / (double)GiB:F2} GB → geçici bölüm: {IsoPartGb} GB");
                _isoOk = true; result = Level.Ok;
                Say("\n✔ ISO uygun. İleri'ye geçebilirsiniz.");
            }
            catch (Exception ex) { Say("Hata: " + ex.Message); }
            finally
            {
                prog.Indeterminate = false;
                _hashResult = sb.ToString();
                _busy = false; verify.Enabled = true;
                zone.SetState(result, $"{new FileInfo(_iso).Length / (double)GiB:F2} GB  ·  {(result == Level.Ok ? "doğrulandı" : "uygun değil — günlüğe bakın")}");
                Notify(result == Level.Ok ? "ISO doğrulandı. İleri'ye geçebilirsiniz." : "Doğrulama başarısız. Ayrıntılar günlükte.", result);
                UpdateNav();
            }
        };
        return root;
    }

    // Etiket kalıpları: archiso (archisolabel=), dracut/Fedora/openSUSE (CDLABEL=), GRUB (--label)
    const string LabelRegex = "(archisolabel=|CDLABEL=|--label\\s+)([^\\s\"]+)";

    // ISO tek geçişte taranır (boyut + 4 GB üstü dosya + etiket)
    static string PreflightScript(string iso) => $$"""
        $img = Mount-DiskImage -ImagePath '{{Ps.Q(iso)}}' -PassThru
        try {
          $root = [string](($img | Get-Volume).DriveLetter) + ':\'
          "EFI=" + (Test-Path (Join-Path $root 'EFI\BOOT\BOOTX64.EFI'))
          $rx = [regex]'{{LabelRegex}}'
          $size = 0L; $big = $null; $lab = ''
          foreach ($f in [IO.DirectoryInfo]::new($root).EnumerateFiles('*', [IO.SearchOption]::AllDirectories)) {
            $size += $f.Length
            if (-not $big -and $f.Length -ge 4GB) { $big = $f.FullName }
            if (-not $lab -and $f.Length -lt 1MB -and ($f.Extension -eq '.conf' -or $f.Extension -eq '.cfg')) {
              $m = $rx.Match([IO.File]::ReadAllText($f.FullName)); if ($m.Success) { $lab = $m.Groups[2].Value }
            }
          }
          if ($big) { "BIG=" + $big }
          "LABEL=" + $lab
          "SIZE=" + $size
        } finally { Dismount-DiskImage -ImagePath '{{Ps.Q(iso)}}' | Out-Null }
        """;

    DiskBar MakeDiskBar() => new();

    void FillDiskBar(DiskBar bar)
    {
        var s = _sys;
        if (s == null) { bar.Empty = "Sistem bilgisi yok"; bar.Set(); return; }
        bar.Set(
            ("Windows (C:)", Math.Max(0, s.CSize - TotalBytes), WinColor),
            ($"Linux ({_fs})", (long)_linuxGb * GiB, LinuxColor),
            ("Geçici bölüm", IsoPartGb * GiB, IsoColor));
    }

    // ---------- Sayfa 3: yapılandırma ----------
    Control BuildConfigPage()
    {
        var root = new Panel();
        var grid = Grid(56, -16, 44);
        Fill(root, grid);

        // sol sütun
        var left = new Panel { BackColor = Bg };
        Cell(grid, left, 0);

        var tight = new Padding(Dp(18), Dp(14), Dp(18), Dp(14));
        var space = new Card { Padding = tight };
        var titleRow = new Panel { Dock = DockStyle.Top, Height = Dp(40) };
        var big = new Label { Text = "", Font = Big, ForeColor = Accent, AutoSize = false, Dock = DockStyle.Right, Width = Dp(130), TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false };
        titleRow.Controls.Add(big);
        Fill(titleRow, Lbl("Linux için ayrılacak alan", Bold, Fg));
        space.Controls.Add(titleRow); titleRow.BringToFront();
        var slider = Stack(space, new Slider { Minimum = 30, Maximum = 500 }, 34);
        var info = Stack(space, Lbl("", Small, Muted, ContentAlignment.TopLeft), 36);
        var bar = Stack(space, MakeDiskBar(), 82);
        Stack(left, space, 14 * 2 + 40 + 34 + 38 + 82 + 2);
        Gap(left, 12);

        var opts = new Card { Padding = tight };
        CardTitle(opts, "Kurulum tercihleri");
        Stack(opts, Lbl("Dosya sistemi (kök bölüm)", Small, Muted), 20);
        var fs = Stack(opts, new Segmented { Items = new[] { "ext4", "btrfs" } }, 32);
        Gap(opts, 8);
        Stack(opts, Lbl("Takas (swap)", Small, Muted), 20);
        var swap = Stack(opts, new Segmented { Items = new[] { "zram", "takas dosyası", "takas bölümü" } }, 32);
        Fill(left, opts);

        // sağ sütun
        var checks = new Card { Padding = tight };
        CardTitle(checks, "Onaylar ve seçenekler");
        var fast = Stack(checks, new Toggle { Text = "Windows Hızlı Başlatma'yı kapat (powercfg /h off) — önerilir" }, 52);
        Gap(checks, 4);
        Stack(checks, Lbl("Devam edebilmek için aşağıdakileri onaylayın:", Small, Muted), 24);
        var backup = Stack(checks, new Toggle { Text = "Önemli dosyalarımı harici bir yere yedekledim" }, 52);
        var sb = Stack(checks, new Toggle { Text = "Secure Boot açıksa BIOS/UEFI'den kapatmam gerektiğini biliyorum" }, 52);
        var bl = Stack(checks, new Toggle { Text = "BitLocker kurtarma anahtarımı kaydettim (BitLocker etkinse zorunlu)" }, 52);
        Gap(checks, 4);
        var dry = Stack(checks, new Toggle { Text = "DENEME MODU: komutları yalnızca göster, diskte hiçbir değişiklik yapma", OnColor = Warn }, 52);
        Cell(grid, checks, 2);
        root.AutoScroll = true;
        root.AutoScrollMinSize = new Size(0, Dp(14 * 2 + 40 + 34 + 38 + 82 + 2 + 12 + 14 * 2 + 28 + 20 + 38 + 8 + 20 + 38 + 8));

        // başlangıç değerleri
        int maxGb = _sys == null ? 500 : Math.Clamp((int)((_sys.CSize - _sys.CMin) / GiB) - (int)IsoPartGb - 1, 30, 500);
        slider.Maximum = maxGb;
        slider.Value = (int)Math.Min(_linuxGb, maxGb);
        fs.SelectedIndex = Math.Max(0, Array.IndexOf(fs.Items, _fs));
        swap.SelectedIndex = Math.Max(0, Array.IndexOf(swap.Items, _swap));
        fast.Checked = _disableFast; backup.Checked = _backup; sb.Checked = _secureBoot; bl.Checked = _bitlockerKey; dry.Checked = _dryRun;

        void Upd()
        {
            _linuxGb = slider.Value;
            _fs = fs.Selected; _swap = swap.Selected;
            _disableFast = fast.Checked; _backup = backup.Checked; _secureBoot = sb.Checked;
            _bitlockerKey = bl.Checked; _dryRun = dry.Checked;

            big.Text = $"{_linuxGb:0} GB";
            var s = _sys;
            if (s == null) info.Text = "";
            else
            {
                var rem = s.CFreeGb - TotalBytes / (double)GiB;
                var newC = s.CSize - TotalBytes;
                info.Text = $"Geçici ISO bölümü {IsoPartGb} GB eklenir → toplam küçültme {TotalBytes / GiB} GB. Windows'ta kalan boş alan ≈ {rem:F0} GB.";
                bool bad = rem < 20 || newC < s.CMin + GiB;
                info.ForeColor = bad ? Bad : Muted;
                if (bad) info.Text += rem < 20 ? "  Windows için en az 20 GB boş kalmalı." : "  C: bu kadar küçültülemez.";
            }
            FillDiskBar(bar);
        }
        slider.ValueChanged += (_, _) => Upd();
        fs.SelectedChanged += (_, _) => Upd();
        swap.SelectedChanged += (_, _) => Upd();
        foreach (var t in new[] { fast, backup, sb, bl, dry }) t.CheckedChanged += (_, _) => Upd();
        Upd();
        return root;
    }

    // ---------- Sayfa 4: özet + uygula ----------
    List<string> PlannedSteps()
    {
        var s = _sys!;
        var l = new List<string>();
        if (_disableFast) l.Add("Hızlı Başlatma'yı kapat");
        l.Add($"C: küçült  ({s.CSize / GiB} → {(s.CSize - TotalBytes) / GiB} GB)");
        l.Add($"{IsoPartGb} GB FAT32 bölüm oluştur");
        l.Add("ISO içeriğini kopyala");
        if (_isoDetectedLabel.Length > 0 && _isoDetectedLabel != IsoLabel) l.Add("Önyükleme etiketini güncelle");
        l.Add("BOOTX64.EFI doğrula");
        l.Add("Önyükleme girişi ekle");
        return l;
    }

    Control BuildApplyPage()
    {
        _applied = false;
        var s = _sys!;
        var root = new Panel();

        var plan = new Card { Padding = new Padding(Dp(18), Dp(12), Dp(18), Dp(12)) };
        var head = new Panel { Dock = DockStyle.Top, Height = Dp(32) };
        var chip = new Chip { Dock = DockStyle.Right };
        chip.Set(_dryRun ? "DENEME MODU — diskte değişiklik yapılmaz" : "GERÇEK UYGULAMA — disk değişecek", _dryRun ? Level.Warn : Level.Bad);
        head.Controls.Add(chip);
        Fill(head, Lbl("Disk planı", Bold, Fg));
        plan.Controls.Add(head); head.BringToFront();
        var bar = Stack(plan, MakeDiskBar(), 64);
        FillDiskBar(bar);
        Fill(plan, Lbl("Dokunulmayacaklar: EFI sistem bölümü, MSR, Recovery. BCD'ye yalnızca yeni bir giriş eklenir.", Small, Muted, ContentAlignment.MiddleLeft));
        Stack(root, plan, 146);
        Gap(root, 10);
        root.AutoScroll = true;
        root.AutoScrollMinSize = new Size(0, Dp(146 + 10 + 44 + 10 + 8 + 10 + 270));

        var confirmRow = Grid(-260, -12, -200, 100);
        var confirm = new Field { ReadOnly = _dryRun, Placeholder = _dryRun ? "(deneme modunda gerekmez)" : "ONAYLIYORUM yazın" };
        var apply = new PillButton { Text = _dryRun ? "Denemeyi çalıştır" : "Uygula", Primary = true, Tint = _dryRun ? null : Bad };
        Cell(confirmRow, confirm, 0); Cell(confirmRow, apply, 2);
        Stack(root, confirmRow, 32);
        Gap(root, 10);
        var prog = Stack(root, new ProgressLine(), 8);
        Gap(root, 10);

        var bottom = Grid(-290, -12, 100);
        var stepsCard = new Card();
        CardTitle(stepsCard, "Adımlar");
        var steps = Fill(stepsCard, new StepList());
        steps.SetItems(PlannedSteps());
        Cell(bottom, stepsCard, 0);
        var logCard = new Card(Input) { Padding = new Padding(Dp(14)) };
        var log = Fill(logCard, new LogBox());
        Cell(bottom, logCard, 2);
        Fill(root, bottom);

        apply.Click += async (_, _) =>
        {
            if (_busy) return;
            if (!_dryRun && confirm.Text.Trim() != "ONAYLIYORUM") { Notify("Onay metnini tam olarak yazın: ONAYLIYORUM", Level.Warn); return; }
            _busy = true; UpdateNav();
            apply.Enabled = false; confirm.ReadOnly = true;
            log.Clear(); steps.SetItems(PlannedSteps());
            prog.Indeterminate = true;
            Notify("Uygulanıyor… lütfen pencereyi kapatmayın.", Level.Info);
            void Say(string t) { if (log.IsHandleCreated && !log.IsDisposed) log.BeginInvoke(() => log.Add(t)); }
            try { _applied = await ExecuteAsync(Say, (i, st) => steps.Set(i, st)); }
            catch (Exception ex) { Say("HATA: " + ex.Message); steps.FailRunning(); }
            prog.Indeterminate = false;
            prog.Value = _applied ? 100 : 0;
            _busy = false;
            if (!_applied)
            {
                apply.Enabled = true; confirm.ReadOnly = _dryRun;
                Say("\nİşlem tamamlanmadı. Yukarıdaki hatayı okuyun; Disk Yönetimi'nden durumu kontrol edin.");
                Notify("İşlem tamamlanmadı. Günlüğü okuyun.", Level.Bad);
            }
            else
            {
                Say("\n✔ Tamamlandı. İleri ile kurulum rehberine geçin.");
                Notify("Tamamlandı. İleri ile kurulum rehberine geçin.", Level.Ok);
            }
            UpdateNav();
        };
        return root;
    }

    async Task<bool> ExecuteAsync(Action<string> say, Action<int, StepState> mark)
    {
        var s = _sys!;
        int idx = -1;
        async Task<(bool Ok, string Out)> Step(string name, string script)
        {
            idx++;
            mark(idx, StepState.Running);
            say($"\n=== {name} ===");
            (bool Ok, string Out) res;
            if (_dryRun) { say("[deneme] çalıştırılacak betik:\n" + script); res = (true, ""); }
            else
            {
                var (code, outp) = await Ps.RunAsync(script, l => say("  " + l));
                if (code != 0) say($"✖ '{name}' başarısız (kod {code}).");
                res = (code == 0, outp);
            }
            mark(idx, res.Ok ? StepState.Done : StepState.Failed);
            return res;
        }

        if (_disableFast && !(await Step("Hızlı Başlatma kapatılıyor", "powercfg /h off")).Ok) return false;

        var newC = s.CSize - TotalBytes;
        if (!(await Step("C: küçültülüyor", $"Resize-Partition -DriveLetter C -Size {newC}")).Ok) return false;

        var create = await Step($"{IsoPartGb} GB FAT32 bölüm oluşturuluyor", $$"""
            $part = New-Partition -DiskNumber {{s.DiskNumber}} -Size {{IsoPartGb * GiB}} -AssignDriveLetter
            $part | Format-Volume -FileSystem FAT32 -NewFileSystemLabel {{IsoLabel}} -Confirm:$false | Out-Null
            "DRIVE=" + (Get-Partition -DiskNumber {{s.DiskNumber}} -PartitionNumber $part.PartitionNumber).DriveLetter
            """);
        if (!create.Ok) return false;
        var dst = _dryRun ? "X" : create.Out.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("DRIVE=")).Select(l => l[6..]).FirstOrDefault() ?? "";
        if (dst.Length != 1) { say("✖ Yeni bölümün sürücü harfi alınamadı."); mark(idx, StepState.Failed); return false; }

        if (!(await Step("ISO içeriği kopyalanıyor (birkaç dakika sürebilir)", $$"""
            $img = Mount-DiskImage -ImagePath '{{Ps.Q(_iso)}}' -PassThru
            try {
              $root = [string](($img | Get-Volume).DriveLetter) + ':\'
              robocopy $root '{{dst}}:\' /E /MT:16 /R:1 /W:1 /NP /NFL /NDL /NJH
              if ($LASTEXITCODE -ge 8) { throw "robocopy hata kodu $LASTEXITCODE" }
            } finally { Dismount-DiskImage -ImagePath '{{Ps.Q(_iso)}}' | Out-Null }
            """)).Ok) return false;

        if (_isoDetectedLabel.Length > 0 && _isoDetectedLabel != IsoLabel)
        {
            if (!(await Step($"Önyükleme yapılandırmasında etiket → {IsoLabel}", $$"""
                $rx = [regex]'{{LabelRegex}}'
                foreach ($f in [IO.DirectoryInfo]::new('{{dst}}:\').EnumerateFiles('*', [IO.SearchOption]::AllDirectories)) {
                  if ($f.Length -ge 1MB -or ($f.Extension -ne '.conf' -and $f.Extension -ne '.cfg')) { continue }
                  $t = [IO.File]::ReadAllText($f.FullName)
                  $n = $rx.Replace($t, '${1}{{IsoLabel}}')
                  if ($n -ne $t) {
                    if ($f.IsReadOnly) { $f.IsReadOnly = $false }
                    [IO.File]::WriteAllText($f.FullName, $n, (New-Object Text.UTF8Encoding $false))
                    "Güncellendi: " + $f.FullName
                  }
                }
                """)).Ok) return false;
        }

        var verify = await Step("Doğrulama", $$"""
            if (-not (Test-Path '{{dst}}:\EFI\BOOT\BOOTX64.EFI')) { throw "BOOTX64.EFI bulunamadı" }
            "Tamam: {{dst}}:\EFI\BOOT\BOOTX64.EFI mevcut"
            """);
        if (!verify.Ok) return false;

        // Çoğu firmware dahili diskteki bu bölümü boot menüsünde listelemez; Windows Boot Manager'a giriş ekliyoruz.
        var entry = await Step("Önyükleme girişi ekleniyor (Windows Boot Manager)", $$"""
            function Bcd { $o = & bcdedit @args 2>&1 | Out-String; if ($LASTEXITCODE -ne 0) { throw "bcdedit $args başarısız: $o" }; $o }
            $g = [regex]::Match((Bcd /copy '{bootmgr}' /d '{{BootEntryName}}'), '\{[0-9a-fA-F-]{36}\}').Value
            if (-not $g) { throw "Yeni girişin kimliği okunamadı" }
            Bcd /set $g device partition={{dst}}: | Out-Null
            Bcd /set $g path '\EFI\BOOT\BOOTX64.EFI' | Out-Null
            Bcd /displayorder $g /addlast | Out-Null
            Bcd /timeout 15 | Out-Null
            "BCDGUID=$g"
            """);
        if (!entry.Ok) return false;
        _bcdGuid = _dryRun ? "{GUID}" : entry.Out.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("BCDGUID=")).Select(l => l[8..]).FirstOrDefault() ?? "";
        return true;
    }

    // ---------- Sayfa 5: rehber ----------
    Control BuildGuidePage()
    {
        var root = new Panel();
        var banner = new Banner();
        if (_dryRun)
            banner.Set(Level.Warn, "DENEME MODU yapıldı: diskte HİÇBİR değişiklik yapılmadı. Gerçek uygulama için sihirbazı yeniden başlatıp deneme modunu kapatın.");
        else
            banner.Set(Level.Ok, $"Hazırsınız. '{IsoLabel}' bölümü oluşturuldu. Aşağıdaki adımları izleyerek Linux'u kurun.");
        Stack(root, banner, 60);
        Gap(root, 12);

        var tools = Grid(-190, -10, -170, 100);
        var save = new PillButton { Text = "Masaüstüne kaydet" };
        var copy = new PillButton { Text = "Panoya kopyala" };
        Cell(tools, save, 0); Cell(tools, copy, 2);
        Stack(root, tools, 42);
        Gap(root, 12);

        var card = new Card(Input) { Padding = new Padding(Dp(18), Dp(14), Dp(10), Dp(14)) };
        var box = Fill(card, new DocBox());
        box.SetGuide(BuildGuide(false));
        Fill(root, card);

        save.Click += (_, _) =>
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "linux-kurulum-rehberi.txt");
                File.WriteAllText(path, BuildGuide(true), Encoding.UTF8);
                Notify("Kaydedildi: " + path, Level.Ok);
            }
            catch (Exception ex) { Notify("Kaydedilemedi: " + ex.Message, Level.Bad); }
        };
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(BuildGuide(true)); Notify("Rehber panoya kopyalandı.", Level.Ok); }
            catch (Exception ex) { Notify("Kopyalanamadı: " + ex.Message, Level.Bad); }
        };
        return root;
    }

    string BuildGuide(bool withNote)
    {
        var swapNote = _swap switch
        {
            "zram" => "Takas: kurulumda swap bölümü oluşturmayın; zram çoğu modern dağıtımda hazır gelir (yoksa takas dosyası seçin).",
            "takas dosyası" => "Takas: kurulum ekranında 'Swap (dosya)' seçeneğini seçin.",
            _ => "Takas: kurulum ekranında 8 GB'lık ayrı bir swap bölümü oluşturun (kök bölümü bunun kadar küçültün).",
        };
        var note = withNote && _dryRun ? "** DENEME MODU yapıldı: diskte HİÇBİR değişiklik yapılmadı. Gerçek uygulama için sihirbazı yeniden başlatıp deneme modunu kapatın. **\n\n" : "";
        return note + $"""
            ADIM A — Linux'u başlatma
            1. Bilgisayarı yeniden başlatın ve boot menüsünü açın (marka başına F12 / F2 / Esc / Del).
            2. Secure Boot açıksa UEFI ayarlarından kapatın.
            3. Sihirbaz Windows Boot Manager'a '{BootEntryName}' girişini ekledi. Yeniden başlatınca
               açılan mavi/siyah menüden bu girişi seçin (15 sn sonra varsayılan Windows açılır).
               Menü gelmezse firmware boot menüsünde '{IsoLabel}' girişini ya da
               "dosyadan başlat / Boot from file" ile {IsoLabel}:\EFI\BOOT\BOOTX64.EFI dosyasını deneyin.
            4. Canlı sistem açılınca kurucuyu başlatın.

            ADIM B — Kurucuda bölümleme (EN KRİTİK ADIM)
            1. "Manuel bölümleme" seçin. 'Diski sil' / 'Yanına kur' otomatik seçeneklerini KULLANMAYIN.
            2. Ayrılmamış alanı ({_linuxGb} GB) seçip yeni bölüm oluşturun:
               - Bağlama noktası: /    Dosya sistemi: {_fs}    (tüm boş alan)
               - {swapNote}
            3. Mevcut EFI sistem bölümünü (~100 MB) seçin:
               - Bağlama noktası: /boot/efi
               - "Biçimlendir" kutusunu KESİNLİKLE İŞARETLEMEYİN (biçimlendirirseniz Windows açılmaz).
            4. Önyükleyici olarak GRUB seçin (EFI bölümü ~100 MB, systemd-boot için dar olabilir).
            5. Özet ekranında silinecek/biçimlendirilecek bölümler listelenir. C: veya Recovery görürseniz DURUN.
               {IsoLabel} bölümü (canlı sistemin kendisi) elbette kurulum sırasında dokunulmamalıdır.
            (Ekran adları dağıtıma ve sürüme göre değişebilir.)

            ADIM C — Kurulum sonrası
            1. GRUB menüsünde Windows görünmüyorsa:
                 sudo nano /etc/default/grub      → GRUB_DISABLE_OS_PROBER=false
                 sudo grub-mkconfig -o /boot/grub/grub.cfg
            2. İki işletim sistemi de açılıyorsa Windows'ta Disk Yönetimi'nden {IsoLabel} bölümünü silin
               ve boşalan alanı isterseniz Linux'a bırakın (ya da C:'ye genişletin).
            3. Linux'tan C: sürücüsüne yazmayın (NTFS bozulma riski).

            Geri alma (kurulumdan önce vazgeçerseniz): önce yönetici CMD'de
              bcdedit /delete {(_bcdGuid.Length > 0 ? _bcdGuid : "{GUID}")}
            ile boot girişini kaldırın (kimlik: bcdedit /enum all), sonra Disk Yönetimi'nde {IsoLabel}
            bölümünü silin ve C:'ye sağ tık → Birimi Genişlet ile alanı geri verin.
            """;
    }
}
