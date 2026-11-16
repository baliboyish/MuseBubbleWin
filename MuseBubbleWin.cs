using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MuseBubbleWin
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BubbleForm());
        }
    }

    // ---------- Win32 ----------
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; public POINT(int x, int y) { this.x = x; this.y = y; } }
        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp; public byte BlendFlags;
            public byte SourceConstantAlpha; public byte AlphaFormat;
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        public const int ULW_ALPHA = 0x02;
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;
        public const int SW_RESTORE = 9;
    }

    // ---------- Settings ----------
    class Settings
    {
        public int X = -1, Y = -1, Size = 112;
        public double Opacity = 1.0;
        public string Theme = "dark";
        static string Dir { get { return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MuseBubbleWin"); } }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }
        public void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] kv = line.Split('=');
                    if (kv.Length != 2) continue;
                    int iv; double dv;
                    if (kv[0] == "x" && int.TryParse(kv[1], out iv)) X = iv;
                    if (kv[0] == "y" && int.TryParse(kv[1], out iv)) Y = iv;
                    if (kv[0] == "size" && int.TryParse(kv[1], out iv)) Size = Math.Max(72, Math.Min(200, iv));
                    if (kv[0] == "opacity" && double.TryParse(kv[1],
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out dv))
                        Opacity = Math.Max(0.5, Math.Min(1.0, dv));
                    if (kv[0] == "theme") Theme = kv[1] == "light" ? "light" : "dark";
                }
            }
            catch { }
        }
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new string[] {
                    "x=" + X, "y=" + Y, "size=" + Size,
                    "opacity=" + Opacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "theme=" + Theme });
            }
            catch { }
        }
    }

    // ---------- Bubble ----------
    public static class Ui
    {
        public static readonly string FontName = Detect();
        static string Detect()
        {
            try
            {
                using (System.Drawing.Text.InstalledFontCollection ifc =
                    new System.Drawing.Text.InstalledFontCollection())
                {
                    foreach (FontFamily f in ifc.Families)
                        if (f.Name == "Inter") return "Inter";
                }
            }
            catch { }
            return "Segoe UI";
        }
    }

    public class BubbleForm : Form
    {
        readonly Settings settings = new Settings();
        readonly Image avatar;
        PanelForm panel;
        ChatForm chat;
        NotifyIcon tray;
        ContextMenuStrip menu;
        ToolStripMenuItem themeDarkItem, themeLightItem;
        public bool Light { get { return settings.Theme == "light"; } }
        Timer clickTimer, hoverOpenTimer, hoverCloseTimer, fgTimer;
        bool dragging, moved, allowExit;
        Point dragOffset;
        IntPtr lastExternal = IntPtr.Zero;
        DateTime suppressHoverUntil = DateTime.MinValue;
        Icon trayIconKeep;
        Bitmap trayBmpKeep;

        public BubbleForm()
        {
            settings.Load();
            using (Stream s = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("avatar.png"))
                avatar = Image.FromStream(s);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            Width = Height = settings.Size;
            StartPosition = FormStartPosition.Manual;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int x = settings.X >= 0 ? settings.X : wa.Right - settings.Size - 24;
            int y = settings.Y >= 0 ? settings.Y : wa.Top + (wa.Height - settings.Size) / 3;
            Location = new Point(x, y);

            BuildMenu();
            BuildTray();
            panel = new PanelForm(this);
            chat = new ChatForm(this);

            clickTimer = new Timer(); clickTimer.Interval = 280;
            clickTimer.Tick += delegate { clickTimer.Stop(); ToggleChat(); };

            hoverOpenTimer = new Timer(); hoverOpenTimer.Interval = 420;
            hoverOpenTimer.Tick += delegate
            {
                hoverOpenTimer.Stop();
                if (DateTime.Now > suppressHoverUntil && !panel.Visible && !chat.Visible) ShowPanel();
            };

            hoverCloseTimer = new Timer(); hoverCloseTimer.Interval = 450;
            hoverCloseTimer.Tick += delegate
            {
                if (!panel.Visible) return;
                Point mp = Cursor.Position;
                if (!Bounds.Contains(mp) && !panel.Bounds.Contains(mp)) HidePanel();
            };
            hoverCloseTimer.Start();

            fgTimer = new Timer(); fgTimer.Interval = 400;
            fgTimer.Tick += delegate { TrackForeground(); };
            fgTimer.Start();

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            DoubleClick += delegate
            {
                clickTimer.Stop();
                HidePanel();
                OpenClaude();
            };
            MouseWheel += OnWheel;
            MouseEnter += delegate { hoverOpenTimer.Start(); };
            FormClosing += OnClosing;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000; // WS_EX_LAYERED
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Render();
            chat.InitWeb();
        }

        // ----- rendering -----
        Bitmap RenderBubbleBitmap()
        {
            int s = settings.Size;
            Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                RectangleF circle = new RectangleF(4, 4, s - 8, s - 8);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(circle);
                    GraphicsState st = g.Save();
                    g.SetClip(path);
                    g.DrawImage(avatar, circle);
                    g.Restore(st);
                }
                if (Light)
                {
                    RectangleF shadow = new RectangleF(
                        circle.X + 1, circle.Y + 3, circle.Width, circle.Height);
                    using (Pen sh = new Pen(Color.FromArgb(45, 0, 0, 0), 8))
                        g.DrawEllipse(sh, shadow);
                    using (Pen ring = new Pen(Color.FromArgb(255, 255, 255, 255), 4))
                        g.DrawEllipse(ring, circle);
                    using (Pen edge = new Pen(Color.FromArgb(70, 0, 0, 0), 1))
                        g.DrawEllipse(edge, new RectangleF(
                            circle.X - 2, circle.Y - 2, circle.Width + 4, circle.Height + 4));
                }
                else
                {
                    using (Pen glow = new Pen(Color.FromArgb(80, 125, 226, 255), 7))
                        g.DrawEllipse(glow, circle);
                    using (Pen ring = new Pen(Color.FromArgb(255, 125, 226, 255), 2.5f))
                        g.DrawEllipse(ring, circle);
                }
            }
            return bmp;
        }

        void SetBitmap(Bitmap bitmap, byte alpha)
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            IntPtr old = Native.SelectObject(memDc, hBitmap);
            Native.SIZE size = new Native.SIZE(bitmap.Width, bitmap.Height);
            Native.POINT src = new Native.POINT(0, 0);
            Native.POINT dst = new Native.POINT(Left, Top);
            Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION();
            blend.BlendOp = Native.AC_SRC_OVER;
            blend.BlendFlags = 0;
            blend.SourceConstantAlpha = alpha;
            blend.AlphaFormat = Native.AC_SRC_ALPHA;
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size,
                memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
            Native.SelectObject(memDc, old);
            Native.DeleteObject(hBitmap);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }

        void Render()
        {
            if (!IsHandleCreated) return;
            Width = Height = settings.Size;
            using (Bitmap bmp = RenderBubbleBitmap())
                SetBitmap(bmp, (byte)(255 * settings.Opacity));
        }

        // ----- menu & tray -----
        void BuildMenu()
        {
            menu = new ContextMenuStrip();
            ToolStripMenuItem open = new ToolStripMenuItem("Buka Muse full");
            open.Click += delegate { OpenClaude(); };
            ToolStripMenuItem chatItem = new ToolStripMenuItem("Chat Muse");
            chatItem.Click += delegate { ShowChat(); };
            ToolStripMenuItem showPanel = new ToolStripMenuItem("Panel");
            showPanel.Click += delegate { ShowPanel(); };

            ToolStripMenuItem sizeMenu = new ToolStripMenuItem("Ukuran");
            foreach (int sz in new int[] { 84, 112, 148 })
            {
                int captured = sz;
                ToolStripMenuItem it = new ToolStripMenuItem(
                    sz == 84 ? "Kecil" : sz == 112 ? "Sedang" : "Besar");
                it.Click += delegate
                {
                    settings.Size = captured;
                    Render(); Persist();
                };
                sizeMenu.DropDownItems.Add(it);
            }
            ToolStripMenuItem opMenu = new ToolStripMenuItem("Opasitas");
            foreach (double op in new double[] { 1.0, 0.85, 0.7 })
            {
                double captured = op;
                ToolStripMenuItem it = new ToolStripMenuItem(((int)(op * 100)) + "%");
                it.Click += delegate
                {
                    settings.Opacity = captured;
                    Render(); Persist();
                };
                opMenu.DropDownItems.Add(it);
            }
            ToolStripMenuItem themeMenu = new ToolStripMenuItem("Tema");
            themeDarkItem = new ToolStripMenuItem("Gelap (charcoal)");
            themeDarkItem.Checked = !Light;
            themeDarkItem.Click += delegate { SetTheme("dark"); };
            themeLightItem = new ToolStripMenuItem("Terang (Apple)");
            themeLightItem.Checked = Light;
            themeLightItem.Click += delegate { SetTheme("light"); };
            themeMenu.DropDownItems.Add(themeDarkItem);
            themeMenu.DropDownItems.Add(themeLightItem);
            ToolStripMenuItem auto = new ToolStripMenuItem("Auto-start Windows");
            auto.Checked = IsAutoStart();
            auto.Click += delegate
            {
                SetAutoStart(!auto.Checked);
                auto.Checked = IsAutoStart();
            };
            ToolStripMenuItem hide = new ToolStripMenuItem("Sembunyikan ke tray");
            hide.Click += delegate { HideAll(); };
            ToolStripMenuItem show = new ToolStripMenuItem("Tampilkan");
            show.Click += delegate { ShowBubble(); };
            ToolStripMenuItem exit = new ToolStripMenuItem("Keluar");
            exit.Click += delegate { allowExit = true; Close(); };

            menu.Items.Add(open);
            menu.Items.Add(chatItem);
            menu.Items.Add(showPanel);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(sizeMenu);
            menu.Items.Add(opMenu);
            menu.Items.Add(themeMenu);
            menu.Items.Add(auto);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(show);
            menu.Items.Add(hide);
            menu.Items.Add(exit);
            ContextMenuStrip = menu;
        }

        void BuildTray()
        {
            trayBmpKeep = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(trayBmpKeep))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(1, 1, 30, 30);
                    GraphicsState st = g.Save(); g.SetClip(p);
                    g.DrawImage(avatar, new Rectangle(0, 0, 32, 32));
                    g.Restore(st);
                }
            }
            trayIconKeep = Icon.FromHandle(trayBmpKeep.GetHicon());
            tray = new NotifyIcon();
            tray.Icon = trayIconKeep;
            tray.Text = "Muse Bubble";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowBubble(); };
        }

        public void SetTheme(string t)
        {
            settings.Theme = t;
            if (themeDarkItem != null) themeDarkItem.Checked = t == "dark";
            if (themeLightItem != null) themeLightItem.Checked = t == "light";
            Render();
            if (panel != null) panel.ApplyTheme();
            if (chat != null) chat.ApplyTheme();
            Persist();
        }

        bool IsAutoStart()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                return k != null && k.GetValue("MuseBubbleWin") != null;
        }
        void SetAutoStart(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (k == null) return;
                if (on) k.SetValue("MuseBubbleWin", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("MuseBubbleWin", false);
            }
        }

        // ----- foreground tracking -----
        void TrackForeground()
        {
            IntPtr h = Native.GetForegroundWindow();
            if (h == IntPtr.Zero) return;
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            if (pid != (uint)Process.GetCurrentProcess().Id)
                lastExternal = h;
        }

        public void GoBack()
        {
            HidePanel();
            HideChat();
            if (lastExternal != IntPtr.Zero)
            {
                if (Native.IsIconic(lastExternal))
                    Native.ShowWindow(lastExternal, Native.SW_RESTORE);
                if (Native.SetForegroundWindow(lastExternal)) return;
            }
            SendKeys.Send("%{TAB}");
        }

        // ----- actions -----
        public void OpenClaude()
        {
            string url = "https://muse.ai";
            string[] candidates = new string[] {
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\BraveSoftware\Brave-Browser\Application\brave.exe"),
                Environment.ExpandEnvironmentVariables(@"%LocalAppData%\BraveSoftware\Brave-Browser\Application\brave.exe"),
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\BraveSoftware\Brave-Browser\Application\brave.exe") };
            foreach (string c in candidates)
            {
                if (File.Exists(c))
                {
                    Process.Start(c, url);
                    return;
                }
            }
            Process.Start(url);
        }

        public void ShowPanel()
        {
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int px = Left - panel.Width - 12;
            if (px < wa.Left) px = Right + 12;
            int py = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - panel.Height));
            panel.Location = new Point(px, py);
            panel.Show();
            panel.Activate();
        }
        public void HidePanel() { if (panel.Visible) panel.Hide(); }
        void TogglePanel()
        {
            if (panel.Visible) { HidePanel(); suppressHoverUntil = DateTime.Now.AddSeconds(2); }
            else ShowPanel();
        }
        public void ShowChat()
        {
            HidePanel();
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int px = Left - chat.Width - 12;
            if (px < wa.Left) px = Right + 12;
            int py = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - chat.Height));
            chat.Location = new Point(px, py);
            chat.Show();
            chat.Activate();
        }
        public void HideChat() { if (chat != null && chat.Visible) chat.Hide(); }
        void ToggleChat()
        {
            if (chat.Visible) { HideChat(); suppressHoverUntil = DateTime.Now.AddSeconds(2); }
            else ShowChat();
        }
        public void HideAll()
        {
            HidePanel();
            HideChat();
            Hide();
            tray.ShowBalloonTip(2000, "Muse Bubble",
                "Aku ngumpet di tray â€” double click ikonku buat nampilin lagi",
                ToolTipIcon.Info);
        }
        public void ShowBubble()
        {
            Show();
            Render();
        }

        void Persist()
        {
            settings.X = Left; settings.Y = Top;
            settings.Save();
        }

        // ----- mouse -----
        void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true; moved = false;
                dragOffset = new Point(e.X, e.Y);
            }
        }
        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging && e.Button == MouseButtons.Left)
            {
                Point p = Cursor.Position;
                int nx = p.X - dragOffset.X, ny = p.Y - dragOffset.Y;
                if (Math.Abs(nx - Left) + Math.Abs(ny - Top) > 4) moved = true;
                Location = new Point(nx, ny);
            }
        }
        void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = false;
            if (moved) { Persist(); moved = false; return; }
            clickTimer.Stop();
            clickTimer.Start();
        }
        void OnWheel(object sender, MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                settings.Opacity = Math.Max(0.5, Math.Min(1.0,
                    settings.Opacity + (e.Delta > 0 ? 0.05 : -0.05)));
            }
            else
            {
                settings.Size = Math.Max(72, Math.Min(200,
                    settings.Size + (e.Delta > 0 ? 8 : -8)));
            }
            Render(); Persist();
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!allowExit)
            {
                e.Cancel = true;
                HideAll();
                return;
            }
            Persist();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
        }
    }

    // ---------- Mini panel ----------
    public class PanelForm : Form
    {
        readonly BubbleForm owner2;
        Label titleLbl;
        readonly List<Button> btns = new List<Button>();
        static readonly Color Bg = Color.FromArgb(42, 39, 36);
        static readonly Color BtnBg = Color.FromArgb(58, 53, 47);
        static readonly Color Ivory = Color.FromArgb(245, 240, 230);
        static readonly Color Gold = Color.FromArgb(212, 175, 105);
        static readonly Color Gray = Color.FromArgb(160, 152, 140);

        public PanelForm(BubbleForm ownerForm)
        {
            owner2 = ownerForm;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Bg;
            Size = new Size(250, 216);
            StartPosition = FormStartPosition.Manual;

            Label title = new Label();
            title.Text = "\u2726 Muse";
            title.ForeColor = Gold;
            title.Font = new Font(Ui.FontName, 12, FontStyle.Bold);
            title.Location = new Point(14, 10);
            title.AutoSize = true;
            Controls.Add(title);
            titleLbl = title;

            AddButton("Chat Muse", 48, delegate { owner2.ShowChat(); });
            AddButton("Buka Muse full", 88, delegate { owner2.OpenClaude(); owner2.HidePanel(); });
            AddButton("Balik ke app sebelumnya", 128, delegate { owner2.GoBack(); });
            AddButton("Sembunyikan", 168, delegate { owner2.HideAll(); });

            Deactivate += delegate { Hide(); };
            VisibleChanged += delegate
            {
                if (Visible) ApplyRegion();
            };
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            bool light = owner2.Light;
            Color bg = light ? Color.FromArgb(245, 245, 247) : Color.FromArgb(42, 39, 36);
            Color btn = light ? Color.FromArgb(232, 232, 237) : Color.FromArgb(58, 53, 47);
            Color txt = light ? Color.FromArgb(29, 29, 31) : Color.FromArgb(245, 240, 230);
            Color acc = light ? Color.FromArgb(0, 113, 227) : Color.FromArgb(212, 175, 105);
            BackColor = bg;
            if (titleLbl != null) titleLbl.ForeColor = acc;
            foreach (Button b in btns) { b.BackColor = btn; b.ForeColor = txt; }
        }

        void ApplyRegion()
        {
            IntPtr rgn = Native.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 16, 16);
            Region = Region.FromHrgn(rgn);
        }

        void AddButton(string text, int y, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = BtnBg;
            b.ForeColor = Ivory;
            b.Font = new Font(Ui.FontName, 10);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Location = new Point(14, y);
            b.Size = new Size(222, 34);
            b.Click += onClick;
            Controls.Add(b);
            btns.Add(b);
        }

    }

    // ---------- Embedded Muse chat (WebView2) ----------
    public class ChatForm : Form
    {
        bool traceEnabled = true;
        readonly List<FilePickerBridge> filePickers = new List<FilePickerBridge>();
        void ConfigureFilePicker(CoreWebView2 core, Form form)
        {
            EventHandler<CoreWebView2NavigationCompletedEventArgs> ready = null;
            ready = async delegate
            {
                core.NavigationCompleted -= ready;
                try
                {
                    var picker = new FilePickerBridge(core, form, Trace);
                    await picker.EnableAsync();
                    filePickers.Add(picker);
                }
                catch (Exception ex) { Trace("File picker initialization failed: " + ex); }
            };
            // CDP needs a loaded document/renderer; configuring before first navigation can stall.
            core.NavigationCompleted += ready;
        }
        string TracePath { get { return Path.Combine(Application.StartupPath, "attachment-trace.log"); } }
        void Trace(string message)
        {
            if (!traceEnabled) return;
            try { File.AppendAllText(TracePath, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine); }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }

        async System.Threading.Tasks.Task OpenPopup(CoreWebView2Environment env,
            CoreWebView2NewWindowRequestedEventArgs e, Form parent)
        {
            Trace("NewWindowRequested; userInitiated=" + e.IsUserInitiated);
            CoreWebView2Deferral deferral = e.GetDeferral();
            Form pop = null;
            try
            {
                pop = new Form();
                pop.Text = "Muse";
                pop.Size = new Size(880, 660);
                pop.StartPosition = FormStartPosition.CenterParent;
                WebView2 child = new WebView2();
                child.Dock = DockStyle.Fill;
                pop.Controls.Add(child);
                // Blank and cross-origin windows also need their own browsing context.
                // Do not navigate child: the runtime must perform the requested navigation.
                await child.EnsureCoreWebView2Async(env);
                ConfigureFilePicker(child.CoreWebView2, pop);
                child.CoreWebView2.NewWindowRequested += async delegate(object sender, CoreWebView2NewWindowRequestedEventArgs next)
                { await OpenPopup(env, next, child.FindForm()); };
                child.CoreWebView2.WindowCloseRequested += delegate { child.FindForm().Close(); };
                e.NewWindow = child.CoreWebView2;
                e.Handled = true;
                pop.Show(parent);
                pop.Activate();
                child.Focus();
            }
            catch (Exception ex)
            {
                if (pop != null) pop.Dispose();
                e.Handled = true;
                Trace("Popup creation failed: " + ex);
                MessageBox.Show(parent, "Could not open the Muse window: " + ex.Message,
                    "Muse", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { deferral.Complete(); }
        }

        readonly BubbleForm owner2;
        WebView2 web;
        Label loading;
        bool inited;
        Panel barPanel;
        Label titleLbl;
        readonly List<Button> btns = new List<Button>();
        static readonly Color Bg = Color.FromArgb(42, 39, 36);
        static readonly Color BtnBg = Color.FromArgb(58, 53, 47);
        static readonly Color Ivory = Color.FromArgb(245, 240, 230);
        static readonly Color Gold = Color.FromArgb(212, 175, 105);

        public ChatForm(BubbleForm ownerForm)
        {
            owner2 = ownerForm;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Bg;
            Size = new Size(392, 560);
            StartPosition = FormStartPosition.Manual;

            Panel bar = new Panel();
            bar.BackColor = Bg;
            bar.Location = new Point(0, 0);
            bar.Size = new Size(392, 40);
            bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(bar);
            barPanel = bar;

            Label title = new Label();
            title.Text = "\u2726 Muse Chat";
            title.ForeColor = Gold;
            title.Font = new Font(Ui.FontName, 11, FontStyle.Bold);
            title.Location = new Point(12, 9);
            title.AutoSize = true;
            bar.Controls.Add(title);
            titleLbl = title;

            ContextMenuStrip diagnostics = new ContextMenuStrip();
            diagnostics.Items.Add("Trace attachment click", null, delegate
            {
                if (web.CoreWebView2 == null) return;
                traceEnabled = true;
                Trace("Trace enabled; runtime=" + web.CoreWebView2.Environment.BrowserVersionString);
                EnableAttachmentTrace();
            });
            diagnostics.Items.Add("Open browser DevTools", null, delegate
            {
                if (web.CoreWebView2 != null) web.CoreWebView2.OpenDevToolsWindow();
            });
            title.ContextMenuStrip = diagnostics;
            bar.ContextMenuStrip = diagnostics;

            Button btnBack = BarButton("Balik", 178, 66);
            btnBack.Click += delegate { owner2.GoBack(); };
            bar.Controls.Add(btnBack);
            btns.Add(btnBack);
            Button btnFull = BarButton("Full", 250, 58);
            btnFull.Click += delegate { owner2.OpenClaude(); };
            bar.Controls.Add(btnFull);
            btns.Add(btnFull);
            Button btnClose = BarButton("Tutup", 314, 66);
            btnClose.Click += delegate { Hide(); };
            bar.Controls.Add(btnClose);
            btns.Add(btnClose);

            loading = new Label();
            loading.Text = "Memuat Muse...";
            loading.ForeColor = Ivory;
            loading.Font = new Font(Ui.FontName, 10);
            loading.Location = new Point(0, 40);
            loading.Size = new Size(392, 520);
            loading.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                | AnchorStyles.Left | AnchorStyles.Right;
            loading.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(loading);

            web = new WebView2();
            web.Location = new Point(0, 40);
            web.Size = new Size(392, 520);
            web.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                | AnchorStyles.Left | AnchorStyles.Right;
            web.Visible = false;
            Controls.Add(web);

            VisibleChanged += delegate { if (Visible) ApplyRegion(); };
            Activated += delegate { if (web.Visible) web.Focus(); };
            ApplyTheme();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (web.CoreWebView2 != null && keyData == Keys.F12)
            {
                web.CoreWebView2.OpenDevToolsWindow();
                return true;
            }
            if (web.CoreWebView2 != null && keyData == Keys.F9)
            {
                traceEnabled = true;
                Trace("Trace enabled; runtime=" + web.CoreWebView2.Environment.BrowserVersionString);
                EnableAttachmentTrace();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        async void EnableAttachmentTrace()
        {
            try
            {
                await web.CoreWebView2.ExecuteScriptAsync(@"
(() => {
 if(window.__museAttachmentTrace) return;
 window.__museAttachmentTrace = true;
 const emit = (kind, data) => chrome.webview.postMessage({museAttachmentTrace:true,kind,...data});
 const state = () => ({focused:document.hasFocus(),active:navigator.userActivation.isActive});
 for(const type of ['pointerdown','click']) document.addEventListener(type,e=>{
   const el=e.target instanceof Element ? e.target.closest('button,input,[role=button]') : null;
   emit(type,{...state(),trusted:e.isTrusted,tag:e.target.tagName,button:!!el,
     disabled:el ? !!el.disabled : false,ariaDisabled:el && el.getAttribute('aria-disabled')});
 },true);
 function wrap(obj,name,label) {
   const fn=obj[name]; if(typeof fn!=='function') return;
   obj[name]=function(...args){
     emit(label,{...state(),type:this.type || null});
     try {
       const result=Reflect.apply(fn,this,args);
       if(result && typeof result.then==='function') return result.then(v=>v,e=>{
         emit(label+' rejected',{name:e.name});throw e;
       });
       return result;
     } catch(e){emit(label+' threw',{name:e.name});throw e;}
   };
 }
 wrap(window,'showOpenFilePicker','showOpenFilePicker');
 wrap(window,'open','window.open');
 wrap(HTMLInputElement.prototype,'showPicker','input.showPicker');
 wrap(HTMLElement.prototype,'click','element.click');
 window.addEventListener('error',e=>emit('error',{name:e.error && e.error.name}));
 window.addEventListener('unhandledrejection',e=>emit('unhandledrejection',{name:e.reason && e.reason.name}));
 emit('ready',state());
})();");
                MessageBox.Show(this, "Attachment trace enabled. Click +, then inspect attachment-trace.log beside the app. F12 opens DevTools. Reload to remove page tracing.", "Muse diagnostics");
            }
            catch (Exception ex) { Trace("Trace setup failed: " + ex); }
        }

        public void ApplyTheme()
        {
            bool light = owner2.Light;
            Color bg = light ? Color.FromArgb(245, 245, 247) : Color.FromArgb(42, 39, 36);
            Color btn = light ? Color.FromArgb(232, 232, 237) : Color.FromArgb(58, 53, 47);
            Color txt = light ? Color.FromArgb(29, 29, 31) : Color.FromArgb(245, 240, 230);
            Color acc = light ? Color.FromArgb(0, 113, 227) : Color.FromArgb(212, 175, 105);
            BackColor = bg;
            if (barPanel != null) barPanel.BackColor = bg;
            if (titleLbl != null) titleLbl.ForeColor = acc;
            loading.ForeColor = txt;
            foreach (Button b in btns) { b.BackColor = btn; b.ForeColor = txt; }
        }

        Button BarButton(string text, int x, int w)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = BtnBg;
            b.ForeColor = Ivory;
            b.Font = new Font(Ui.FontName, 9);
            b.Location = new Point(x, 6);
            b.Size = new Size(w, 28);
            return b;
        }

        void ApplyRegion()
        {
            IntPtr rgn = Native.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 16, 16);
            Region = Region.FromHrgn(rgn);
        }

        public async void InitWeb()
        {
            if (inited) return;
            inited = true;
            try
            {
                string udf = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MuseBubbleWin", "WebView2Data");
                Directory.CreateDirectory(udf);
                CoreWebView2Environment env =
                    await CoreWebView2Environment.CreateAsync(null, udf, null);
                await web.EnsureCoreWebView2Async(env);
                ConfigureFilePicker(web.CoreWebView2, this);
                web.CoreWebView2.PermissionRequested +=
                    delegate(object s, CoreWebView2PermissionRequestedEventArgs e)
                    {
                        e.State = CoreWebView2PermissionState.Allow;
                    };
                web.CoreWebView2.WebMessageReceived += delegate(object sender, CoreWebView2WebMessageReceivedEventArgs args)
                {
                    if (traceEnabled && args.WebMessageAsJson.Contains("\"museAttachmentTrace\":true"))
                        Trace(args.WebMessageAsJson);
                };
                web.CoreWebView2.NewWindowRequested +=
                    async delegate(object sender, CoreWebView2NewWindowRequestedEventArgs args)
                    { await OpenPopup(env, args, this); };
                web.CoreWebView2.NavigationCompleted += delegate
                {
                    loading.Visible = false;
                    web.Visible = true;
                };
                web.ZoomFactor = 0.9;
                web.Source = new Uri("https://muse.ai");
            }
            catch (Exception ex)
            {
                loading.Text = "Chat gagal dimuat: " + ex.Message
                    + "\r\nPakai Panel cepat / Buka Muse full dulu ya, beb.";
            }
        }
    }
}
