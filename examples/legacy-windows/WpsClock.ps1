# WPS automatic presentation timer. Save this file as UTF-8 WITH BOM.
# Windows PowerShell 5.1 / Windows 10 or 11. No Python, downloads or WPS add-in.
# Run: powershell.exe -NoProfile -STA -WindowStyle Hidden -File ".\WpsClock.ps1"
# WinRAR SFX: use that same command as "Run after extraction".
# This uses Windows built-in .NET Framework. WinRAR only extracts and launches.

$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $source = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WpsClock {
    public sealed class Candidate {
        public IntPtr Handle;
        public string Title, ClassName, ProcessName;
        public Rectangle Bounds;
        public bool IsShow;
        public string Reason;
    }

    public static class Native {
        public delegate bool EnumProc(IntPtr hwnd, IntPtr state);
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr state);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll", EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

        // Heuristic detection: process identity + slideshow class/title, or a
        // borderless, full-monitor WPP window. This is not an official WPS API.
        public static List<Candidate> Scan(bool compatible) {
            var processes = new Dictionary<int,string>();
            foreach (string name in new string[] { "wpp", "wps" }) {
                foreach (Process p in Process.GetProcessesByName(name)) {
                    try { processes[p.Id] = name; } finally { p.Dispose(); }
                }
            }
            var result = new List<Candidate>();
            EnumWindows(delegate(IntPtr hwnd, IntPtr unused) {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                string process;
                if (!processes.TryGetValue((int)pid, out process) || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
                Rect r;
                if (!GetWindowRect(hwnd, out r) || r.Right-r.Left<160 || r.Bottom-r.Top<100) return true;
                var title = new StringBuilder(1024);
                var cls = new StringBuilder(256);
                GetWindowText(hwnd, title, title.Capacity);
                GetClassName(hwnd, cls, cls.Capacity);
                Rectangle bounds = Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);
                Rectangle screen = Screen.FromHandle(hwnd).Bounds;
                string c = cls.ToString().ToLowerInvariant(), t = title.ToString();
                bool full = Math.Abs(bounds.Left-screen.Left)<=12 && Math.Abs(bounds.Top-screen.Top)<=12
                    && Math.Abs(bounds.Right-screen.Right)<=12 && Math.Abs(bounds.Bottom-screen.Bottom)<=12;
                bool caption = (GetWindowLong(hwnd,-16) & 0x00C00000)!=0;
                bool presenter = t.IndexOf("演讲者",StringComparison.OrdinalIgnoreCase)>=0
                    || t.IndexOf("presenter",StringComparison.OrdinalIgnoreCase)>=0;
                bool showClass = c == "screenclass" || c.IndexOf("slideshow",StringComparison.Ordinal)>=0
                    || c.IndexOf("wppshow",StringComparison.Ordinal)>=0;
                bool showTitle = t.StartsWith("幻灯片放映",StringComparison.OrdinalIgnoreCase)
                    || t.StartsWith("WPS 演示 放映",StringComparison.OrdinalIgnoreCase)
                    || t.StartsWith("WPS Presentation Slide Show",StringComparison.OrdinalIgnoreCase)
                    || t.StartsWith("PowerPoint Slide Show",StringComparison.OrdinalIgnoreCase);
                bool explicitShow = (showClass || showTitle) && !presenter;
                bool fallback = compatible && process=="wpp" && full && !caption && !presenter;
                result.Add(new Candidate { Handle=hwnd, Title=t, ClassName=cls.ToString(), ProcessName=process,
                    Bounds=bounds, IsShow=explicitShow || fallback,
                    Reason=explicitShow ? "放映窗口特征" : fallback ? "WPP 无边框全屏窗口（兼容识别）" : "普通窗口 / 未识别" });
                return true;
            }, IntPtr.Zero);
            return result;
        }
    }

    public sealed class Overlay : Form {
        public string TimeText="10:00", StatusText="倒计时";
        public Color Accent=Color.FromArgb(110,231,183);
        public double Fraction=1, Pulse=0;
        public Overlay() {
            FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
            StartPosition=FormStartPosition.Manual; BackColor=Color.FromArgb(15,23,38);
            DoubleBuffered=true; Width=230; Height=102; Opacity=0.94;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get { CreateParams p=base.CreateParams; p.ExStyle|=0x08000000|0x00000080|0x00000020; return p; }
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            var g=e.Graphics;
            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var pen=new Pen(Accent,1+(float)Pulse*3)) g.DrawRectangle(pen,2,2,Width-5,Height-5);
            using(var font=new Font("Consolas",(float)(Height*0.37*(1+0.025*Pulse)),FontStyle.Bold,GraphicsUnit.Pixel))
            using(var brush=new SolidBrush(Accent))
            using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center }) {
                g.DrawString(TimeText,font,brush,new RectangleF(6,8,Width-12,Height*0.52f),format);
                using(var small=new Font("Microsoft YaHei UI",Math.Max(11,Height*0.12f),FontStyle.Regular,GraphicsUnit.Pixel))
                    g.DrawString(StatusText,small,brush,new RectangleF(5,Height*0.58f,Width-10,Height*0.24f),format);
                g.FillRectangle(brush,10,Height-10,(float)((Width-20)*Math.Max(0,Math.Min(1,Fraction))),3);
            }
        }
        public void Place(Rectangle screen, int width, int margin) {
            int height=(int)(width*0.44);
            Native.SetWindowPos(Handle,new IntPtr(-1),screen.Right-width-margin,screen.Top+margin,width,height,0x0010);
        }
    }

    public sealed class Tone : IDisposable {
        readonly MemoryStream stream;
        readonly SoundPlayer player;
        public Tone(bool final) {
            int rate=22050;
            int[] frequencies=final ? new int[]{880,880,1175} : new int[]{880,1100};
            using(var samples=new MemoryStream()) {
                using(var writer=new BinaryWriter(samples,Encoding.UTF8,true)) {
                    for(int n=0;n<frequencies.Length;n++) {
                        int count=(int)(rate*(n==frequencies.Length-1 ? 0.40 : 0.20));
                        for(int i=0;i<count;i++) {
                            double envelope=Math.Min(1,Math.Min(i/(rate*0.015),(count-i)/(rate*0.04)));
                            writer.Write((short)(Math.Sin(2*Math.PI*frequencies[n]*i/rate)*10000*envelope));
                        }
                        for(int i=0;i<rate/9;i++) writer.Write((short)0);
                    }
                    writer.Flush();
                }
                byte[] pcm=samples.ToArray();
                stream=new MemoryStream();
                using(var wave=new BinaryWriter(stream,Encoding.UTF8,true)) {
                    wave.Write(Encoding.ASCII.GetBytes("RIFF"));wave.Write(36+pcm.Length);
                    wave.Write(Encoding.ASCII.GetBytes("WAVEfmt "));wave.Write(16);
                    wave.Write((short)1);wave.Write((short)1);wave.Write(rate);wave.Write(rate*2);
                    wave.Write((short)2);wave.Write((short)16);wave.Write(Encoding.ASCII.GetBytes("data"));
                    wave.Write(pcm.Length);wave.Write(pcm);wave.Flush();
                }
            }
            stream.Position=0;player=new SoundPlayer(stream);player.Load();
        }
        public void Play() { try { player.Play(); } catch { SystemSounds.Exclamation.Play(); } }
        public void Dispose() { player.Stop();player.Dispose();stream.Dispose(); }
    }

    public sealed class MainForm : Form {
        readonly NumericUpDown minutes=new NumericUpDown(), warning=new NumericUpDown(), boxWidth=new NumericUpDown(), margin=new NumericUpDown();
        readonly CheckBox sound=new CheckBox(), compatible=new CheckBox();
        readonly ComboBox screenChoice=new ComboBox();
        readonly Label status=new Label();
        readonly TextBox diagnostics=new TextBox();
        readonly Button arm=new Button(), preview=new Button();
        readonly Overlay overlay=new Overlay();
        readonly Timer timer=new Timer();
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly NotifyIcon tray=new NotifyIcon();
        Tone nearTone, endTone;
        bool armed=false, active=false, warned=false, ended=false, testing=false;
        double startedAt, alertAt=-100, lastScan=-1, missingAt=-1, candidateAt=-1;
        int totalSeconds, threshold;
        IntPtr candidateHandle=IntPtr.Zero;
        Candidate current;
        List<Candidate> lastWindows=new List<Candidate>();

        public MainForm() {
            Text="WPS 自动放映倒计时";ClientSize=new Size(570,560);MinimumSize=new Size(586,599);
            StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",9);
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=2,RowCount=11 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            Controls.Add(layout);
            var heading=new Label { Text="提前待命 · 放映自动计时 · 右上角悬浮提醒",AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,0,0,12) };
            layout.Controls.Add(heading,0,0);layout.SetColumnSpan(heading,2);
            ConfigureNumber(minutes,1,1440,10);ConfigureNumber(warning,0,86399,60);
            ConfigureNumber(boxWidth,160,400,230);ConfigureNumber(margin,0,150,16);
            AddRow(layout,1,"每场时长（分钟）",minutes);AddRow(layout,2,"提前提醒（秒，0 关闭）",warning);
            AddRow(layout,3,"悬浮窗宽度（像素）",boxWidth);AddRow(layout,4,"右侧 / 顶部边距（像素）",margin);
            screenChoice.DropDownStyle=ComboBoxStyle.DropDownList;screenChoice.Width=325;
            screenChoice.Items.Add("自动：显示在检测到的放映屏幕");
            foreach(Screen s in Screen.AllScreens) screenChoice.Items.Add(s.DeviceName+(s.Primary?"（主屏）":""));
            screenChoice.SelectedIndex=0;AddRow(layout,5,"悬浮窗所在屏幕",screenChoice);
            var checks=new FlowLayoutPanel { AutoSize=true,Dock=DockStyle.Fill };
            sound.Text="提示音";sound.Checked=true;sound.AutoSize=true;
            compatible.Text="兼容识别无边框全屏 WPS";compatible.Checked=true;compatible.AutoSize=true;
            checks.Controls.Add(sound);checks.Controls.Add(compatible);layout.Controls.Add(checks,0,6);layout.SetColumnSpan(checks,2);
            var buttons=new FlowLayoutPanel { AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,10,0,6) };
            arm.Text="开始待命";arm.AutoSize=true;arm.Click+=delegate { ToggleArm(); };
            preview.Text="预览声音和动画";preview.AutoSize=true;preview.Click+=delegate { StartPreview(); };
            var hide=new Button { Text="隐藏到托盘",AutoSize=true };hide.Click+=delegate { Hide(); };
            buttons.Controls.Add(arm);buttons.Controls.Add(preview);buttons.Controls.Add(hide);
            layout.Controls.Add(buttons,0,7);layout.SetColumnSpan(buttons,2);
            status.Text="尚未待命：设置参数后，点击“开始待命”。";status.AutoSize=true;status.MaximumSize=new Size(530,0);
            status.Margin=new Padding(0,8,0,8);layout.Controls.Add(status,0,8);layout.SetColumnSpan(status,2);
            diagnostics.Multiline=true;diagnostics.ReadOnly=true;diagnostics.ScrollBars=ScrollBars.Vertical;diagnostics.Dock=DockStyle.Fill;
            layout.Controls.Add(diagnostics,0,9);layout.SetColumnSpan(diagnostics,2);layout.RowStyles.Clear();
            for(int i=0;i<9;i++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var note=new Label { AutoSize=true,MaximumSize=new Size(530,0),Text="退出放映约 2 秒后自动复位。翻页不会重置。右上角小窗会占用少量画面，可调整宽度和边距。关闭本窗口将退出工具；待命时请用“隐藏到托盘”。",Margin=new Padding(0,10,0,0) };
            layout.Controls.Add(note,0,10);layout.SetColumnSpan(note,2);
            tray.Icon=SystemIcons.Information;tray.Text="WPS 倒计时：未待命";tray.Visible=true;
            var menu=new ContextMenuStrip();menu.Items.Add("打开设置",null,delegate { ShowSettings(); });
            menu.Items.Add("开始 / 停止待命",null,delegate { ToggleArm(); });
            menu.Items.Add("退出",null,delegate { Close(); });tray.ContextMenuStrip=menu;
            tray.DoubleClick+=delegate { ShowSettings(); };
            try { nearTone=new Tone(false);endTone=new Tone(true); } catch { sound.Checked=false; }
            timer.Interval=50;timer.Tick+=delegate { UpdateTimer(); };timer.Start();
            FormClosed+=delegate { timer.Stop();timer.Dispose();tray.Visible=false;tray.Dispose();overlay.Dispose();if(nearTone!=null)nearTone.Dispose();if(endTone!=null)endTone.Dispose(); };
        }

        static void ConfigureNumber(NumericUpDown n,int min,int max,int value) { n.Minimum=min;n.Maximum=max;n.Value=value;n.Width=130; }
        static void AddRow(TableLayoutPanel p,int row,string text,Control input) {
            p.Controls.Add(new Label { Text=text,AutoSize=true,Margin=new Padding(0,8,0,8) },0,row);
            input.Margin=new Padding(0,4,0,4);p.Controls.Add(input,1,row);
        }
        void ShowSettings() { Show();WindowState=FormWindowState.Normal;Activate(); }
        bool ValidateTime() {
            if(warning.Value>=minutes.Value*60) { MessageBox.Show(this,"提前提醒秒数必须小于倒计时总秒数。","请检查参数");return false; }return true;
        }
        void ToggleArm() {
            if(!armed && !ValidateTime()) { ShowSettings();return; }
            StopSession();armed=!armed;minutes.Enabled=!armed;warning.Enabled=!armed;compatible.Enabled=!armed;
            arm.Text=armed?"停止待命":"开始待命";preview.Enabled=!armed;
            candidateHandle=IntPtr.Zero;candidateAt=-1;
            status.Text=armed?"已待命：请在 WPS 中开始放映 PPTX。":"已停止待命。";
            tray.Text=armed?"WPS 倒计时：等待放映":"WPS 倒计时：未待命";
        }
        void StartPreview() {
            if(armed)return;
            StopSession();testing=true;totalSeconds=8;threshold=4;active=true;startedAt=clock.Elapsed.TotalSeconds;
            status.Text="正在预览：8 秒倒计时，剩余 4 秒提醒；不会改变正式时长。";
            overlay.Show();PlaceOverlay();
        }
        void Begin(Candidate target) {
            StopSession();current=target;active=true;totalSeconds=(int)minutes.Value*60;threshold=(int)warning.Value;
            startedAt=clock.Elapsed.TotalSeconds;status.Text="放映已识别，自动倒计时中。";tray.Text="WPS 倒计时：放映计时中";
            overlay.Show();PlaceOverlay();
        }
        void StopSession() {
            active=false;testing=false;warned=false;ended=false;current=null;missingAt=-1;alertAt=-100;
            overlay.Hide();
        }
        void PlaceOverlay() {
            Screen target=current!=null?Screen.FromHandle(current.Handle):Screen.PrimaryScreen;
            int choice=screenChoice.SelectedIndex-1;
            Screen[] screens=Screen.AllScreens;if(choice>=0 && choice<screens.Length)target=screens[choice];
            overlay.Place(target.Bounds,(int)boxWidth.Value,(int)margin.Value);
        }
        void Alert(bool final) {
            alertAt=clock.Elapsed.TotalSeconds;
            if(sound.Checked) { Tone tone=final?endTone:nearTone;if(tone!=null)tone.Play();else SystemSounds.Exclamation.Play(); }
        }
        void Scan(double now) {
            lastWindows=Native.Scan(compatible.Checked);
            var text=new StringBuilder();Candidate found=null;
            foreach(Candidate w in lastWindows) {
                text.AppendLine(w.Reason+" | "+w.ProcessName+" | "+w.ClassName);
                text.AppendLine(w.Title);text.AppendLine();
                if(w.IsShow && (found==null || (current!=null && w.Handle==current.Handle)))found=w;
            }
            string report=text.Length==0?"未发现可见的 WPS 演示窗口。检测仅在本机进行。":text.ToString();
            if(diagnostics.Text!=report)diagnostics.Text=report;
            if(!armed || testing)return;
            if(active) {
                // Temporary disappearance, Alt-Tab and slide changes must not
                // start a new countdown. Confirm absence for two seconds.
                if(found!=null) { current=found;missingAt=-1;PlaceOverlay(); }
                else {
                    if(missingAt<0)missingAt=now;
                    if(now-missingAt>=2) { StopSession();candidateHandle=IntPtr.Zero;candidateAt=-1;status.Text="放映已结束，等待下一场。";tray.Text="WPS 倒计时：等待放映"; }
                }
                return;
            }
            if(found==null) { candidateHandle=IntPtr.Zero;candidateAt=-1;return; }
            if(found.Handle!=candidateHandle) { candidateHandle=found.Handle;candidateAt=now;return; }
            if(now-candidateAt>=0.35)Begin(found);
        }
        void UpdateTimer() {
            double now=clock.Elapsed.TotalSeconds;
            if(now-lastScan>=0.4) { lastScan=now;Scan(now); }
            if(!active)return;
            now=clock.Elapsed.TotalSeconds;
            double remaining=Math.Max(0,totalSeconds-(now-startedAt));
            if(remaining<=0 && !ended) { ended=true;Alert(true); }
            else if(remaining>0 && threshold>0 && remaining<=threshold && !warned) { warned=true;Alert(false); }
            int seconds=(int)Math.Ceiling(remaining);
            overlay.TimeText=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            overlay.StatusText=ended?"时间到":warned?"即将结束":"倒计时";
            overlay.Accent=ended?Color.FromArgb(251,113,133):warned?Color.FromArgb(251,191,36):Color.FromArgb(110,231,183);
            overlay.Fraction=remaining/totalSeconds;
            double age=now-alertAt;overlay.Pulse=age>=0 && age<6?(Math.Sin(age*Math.PI*2/1.4)+1)/2:0;
            overlay.Invalidate();
            if(testing) { PlaceOverlay();if(now-startedAt>=11) { StopSession();status.Text="预览结束。请点击“开始待命”，再启动 WPS 放映。"; } }
        }
    }

    public static class Entry {
        [STAThread] public static void Run() {
            bool created;
            using(var mutex=new System.Threading.Mutex(true,"Local\\WpsClockPortableTimer",out created)) {
                if(!created) { MessageBox.Show("倒计时工具已在运行，请从右下角托盘打开设置。");return; }
                Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
'@
    $references = @([System.Windows.Forms.Form].Assembly.Location, [System.Drawing.Point].Assembly.Location, [System.Media.SoundPlayer].Assembly.Location)
    Add-Type -TypeDefinition $source -ReferencedAssemblies $references
    [WpsClock.Entry]::Run()
} catch {
    $message = "WPS 倒计时未能启动：`r`n" + $_.Exception.Message
    try { [System.Windows.Forms.MessageBox]::Show($message, 'WPS 倒计时') | Out-Null }
    catch { Write-Error $message }
}
