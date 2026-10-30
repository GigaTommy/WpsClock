// WpsClock: offline, independently implemented presentation timer.
// Reference interaction: https://blog.csdn.net/qq_45634338/article/details/141186362
// Compile with Build.cmd on Windows. C# 5 / .NET Framework 4.5+.
// No PowerShell process, WPS add-in, network service or installation required.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace WpsClock {
    public sealed class FileRule { public string File=""; public int Seconds=480; public bool Background=false; }
    public sealed class WindowPosition { public string Screen=""; public int X=0, Y=16; }
    public sealed class Settings {
        public int Duration=480, Warning=60, Grace=3;
        public bool CountUp=false, PerPage=false, NearSound=true, EndSound=true;
        public string NearFile="", EndFile="";
        public int EndAction=0;
        public bool Blink=true, StopAtZero=false, Manual=true;
        public int PauseKey=(int)Keys.F3, ResetKey=(int)Keys.F4;
        public int Foreground=Color.White.ToArgb(), Background=Color.FromArgb(0,120,215).ToArgb();
        public int WarningForeground=Color.Black.ToArgb(), WarningBackground=Color.Red.ToArgb();
        public string FontName="Arial", InfoFontName="Microsoft YaHei UI";
        public float FontSize=42, InfoFontSize=10;
        public bool FontBold=false, ClockTime=false, Transparent=false, HideBeforeWarning=false;
        public bool ShowHelp=false, HideWhenIdle=false, HideZero=true, ShowPage=true, ZeroNoPage=false;
        public bool ControlBar=false, SyncScreens=true, Locked=false, Auto=true, Compatible=true;
        public int Opacity=100; // Legacy whole-window setting, retained for XML migration.
        public int BackgroundOpacity=-1, PanelWidth=0, PanelHeight=0;
        public string Prefix="-";
        public List<FileRule> Rules=new List<FileRule>();
        public List<WindowPosition> Positions=new List<WindowPosition>();
        public Settings Clone() {
            using(var stream=new MemoryStream()) { var xml=new XmlSerializer(typeof(Settings));xml.Serialize(stream,this);stream.Position=0;return (Settings)xml.Deserialize(stream); }
        }
        public void Normalize() {
            Duration=Math.Max(0,Math.Min(86399,Duration));Warning=Math.Max(0,Math.Min(86399,Warning));Grace=Math.Max(0,Math.Min(120,Grace));
            if(float.IsNaN(FontSize)||float.IsInfinity(FontSize))FontSize=42;
            if(float.IsNaN(InfoFontSize)||float.IsInfinity(InfoFontSize))InfoFontSize=10;
            FontSize=Math.Max(12,Math.Min(120,FontSize));InfoFontSize=Math.Max(8,Math.Min(32,InfoFontSize));
            if(BackgroundOpacity<0)BackgroundOpacity=Transparent?0:Opacity;
            BackgroundOpacity=Math.Max(0,Math.Min(100,BackgroundOpacity));
            if(PanelWidth>0)PanelWidth=Math.Max(120,Math.Min(8192,PanelWidth));else PanelWidth=0;
            if(PanelHeight>0)PanelHeight=Math.Max(60,Math.Min(8192,PanelHeight));else PanelHeight=0;
            Opacity=Math.Max(25,Math.Min(100,Opacity));EndAction=Math.Max(0,Math.Min(2,EndAction));
            if(Rules==null)Rules=new List<FileRule>();if(Positions==null)Positions=new List<WindowPosition>();
            if(String.IsNullOrWhiteSpace(FontName))FontName="Arial";if(String.IsNullOrWhiteSpace(InfoFontName))InfoFontName="Microsoft YaHei UI";
            if(Prefix==null)Prefix="-";
        }
    }
    public sealed class SettingsStore {
        public readonly string PathName;
        public string Warning="";
        public SettingsStore() {
            string local=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"WpsClock.settings.xml");
            try {
                string probe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".wpsclock-write-"+Guid.NewGuid().ToString("N"));
                using(File.Create(probe)) {} File.Delete(probe);PathName=local;
            } catch {
                string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WpsClockPortable");
                Directory.CreateDirectory(folder);PathName=Path.Combine(folder,"WpsClock.settings.xml");
                Warning="程序目录不可写，设置将保存到："+PathName;
            }
        }
        public Settings Load() {
            try { if(File.Exists(PathName))using(var f=File.OpenRead(PathName)){var s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(f);s.Normalize();return s;} }
            catch(Exception e){Warning="设置读取失败，已使用默认值。原文件未删除。\r\n"+e.Message;}
            var defaults=new Settings();defaults.Normalize();return defaults;
        }
        public void Save(Settings s) {
            string temp=PathName+".tmp";
            using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))new XmlSerializer(typeof(Settings)).Serialize(f,s);
            if(File.Exists(PathName))File.Replace(temp,PathName,null);else File.Move(temp,PathName);
        }
    }

    // Pure timing model; independent of UI, WPS, system wall clock and timer ticks.
    public sealed class ClockState {
        public bool Active, Paused, Warned, Ended;
        public int Duration;
        double anchor, accumulated;
        public void Start(double now,int seconds){Active=true;Paused=false;Warned=false;Ended=false;Duration=seconds;anchor=now;accumulated=0;}
        public double Elapsed(double now){return Active?accumulated+(Paused?0:Math.Max(0,now-anchor)):0;}
        public void TogglePause(double now){if(!Active)return;if(Paused){anchor=now;Paused=false;}else{accumulated=Elapsed(now);Paused=true;}}
        public void Stop(){Active=false;Paused=false;Warned=false;Ended=false;accumulated=0;}
        public void ChangeDuration(double now,int seconds){
            if(Duration==seconds)return;
            bool stoppedAtEnd=Ended&&Paused;
            Duration=seconds;Warned=false;Ended=false;
            if(stoppedAtEnd){anchor=now;Paused=false;}
        }
        public int Advance(double now,int warning,bool stopAtZero){
            if(!Active||Paused||Duration==0)return 0;
            double e=Elapsed(now);
            if(e>=Duration&&!Ended){Ended=true;if(stopAtZero){accumulated=Duration;Paused=true;}return 2;}
            if(!Warned&&!Ended&&warning>0&&e>=Math.Max(0,Duration-warning)){Warned=true;return 1;}return 0;
        }
        public string Display(double now,bool countUp,string prefix){
            double e=Elapsed(now);int seconds;
            if(countUp||Duration==0)seconds=(int)Math.Floor(e);
            else if(e<=Duration)seconds=(int)Math.Ceiling(Duration-e);
            else return prefix+Format((int)Math.Floor(e-Duration));
            return Format(seconds);
        }
        public static string Format(int seconds){seconds=Math.Max(0,seconds);return (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
    }
    public sealed class ShowInfo {
        public IntPtr Handle;
        public IntPtr Owner;
        public int ProcessId;
        public bool Foreground;
        public Rectangle WindowBounds,ClientBounds,MonitorBounds;
        public bool ClientValid,Visible,Minimized,Caption;
        public string Process="", Title="", ClassName="", File="", Reason="", FileSource="";
        public int Page=0, Pages=0;
        public bool IsShow;
    }
    public sealed class FileMatch {
        public string File="",Rule="",Source="",Explanation="";
        public int Seconds;
        public bool Matched,Ambiguous,Background;
    }
    public static class FileTiming {
        public static string CleanTitle(string title){
            title=Regex.Replace((title??"").Trim(),@"^(?:WPS\s+Presentation\s+Slide\s+Show|PowerPoint Slide Show|Microsoft PowerPoint|幻灯片放映|WPS\s*演示\s*(?:幻灯片\s*)?放映)[\s:：\-—–]*","",RegexOptions.IgnoreCase);
            title=Regex.Replace(title,@"\s*[-—–|]\s*(?:WPS(?:\s*(?:Office|演示|Presentation))?|Microsoft PowerPoint|PowerPoint|Adobe\s+(?:Acrobat|Reader)(?:\s+(?:Reader|Pro|Standard|DC))*\s*(?:\((?:32|64)[ -]bit\))?)\s*$","",RegexOptions.IgnoreCase);
            title=Regex.Replace(title,@"\s*[\[【(（](?:只读|兼容模式|Read.Only|Compatibility Mode)[\]】)）]\s*","",RegexOptions.IgnoreCase);
            return title.Trim().Trim('[',']','［','］','【','】').Trim();
        }
        public static string GetFile(ShowInfo info){
            if(info==null)return "";if(info.File.Length>0)return info.File;
            string title=CleanTitle(info.Title);
            Match full=Regex.Match(title,@"((?:[A-Za-z]:[\\/]|\\\\)[^""<>|]+?\.(?:pptx|ppt|ppsx|pps|pdf))(?=$|[\]\s])",RegexOptions.IgnoreCase);
            if(full.Success)return full.Value.Trim();
            Match m=Regex.Match(title,@"([^\\/:*?""<>|]+\.(?:pptx|ppt|ppsx|pps|pdf))",RegexOptions.IgnoreCase);
            return m.Success?m.Value.Trim():"";
        }
        public static string Normalize(string file){return (file??"").Trim().Replace('/','\\');}
        public static string BaseName(string file){string normal=Normalize(file);return normal.Substring(normal.LastIndexOf('\\')+1);}
        public static bool HasPath(string file){return Normalize(file).Contains("\\");}
        public static bool SameFile(string a,string b){
            if(a.Length==0||b.Length==0)return false;
            return String.Equals(HasPath(a)&&HasPath(b)?Normalize(a):BaseName(a),HasPath(a)&&HasPath(b)?Normalize(b):BaseName(b),StringComparison.OrdinalIgnoreCase);
        }
        public static FileMatch Match(List<FileRule> rules,int fallback,ShowInfo info){
            var result=new FileMatch{File=GetFile(info),Seconds=fallback,Source=info==null?"":info.FileSource};
            if(result.File.Length==0&&info!=null){
                string caption=CleanTitle(info.Title);FileRule selected=null;int count=0;
                foreach(var rule in rules){string name=BaseName(rule.File);int dot=name.LastIndexOf('.');string stem=dot>0?name.Substring(0,dot):name;
                    if(caption.Length>0&&(String.Equals(caption,name,StringComparison.OrdinalIgnoreCase)||String.Equals(caption,stem,StringComparison.OrdinalIgnoreCase))){selected=rule;count++;}}
                if(count==1){result.File=BaseName(selected.File);result.Rule=selected.File;result.Seconds=selected.Seconds;result.Background=selected.Background;result.Matched=true;result.Source="窗口标题（唯一文件名）";result.Explanation="文件名匹配";return result;}
                if(count>1){result.Ambiguous=true;result.Explanation="窗口标题对应多个文件";return result;}
            }
            if(result.File.Length==0){result.Explanation="尚未识别文件";return result;}
            if(result.Source.Length==0)result.Source=info.File.Length>0?"文件信息":"放映窗口标题";
            foreach(var rule in rules)if(String.Equals(Normalize(rule.File),Normalize(result.File),StringComparison.OrdinalIgnoreCase)){
                result.Rule=rule.File;result.Seconds=rule.Seconds;result.Background=rule.Background;result.Matched=true;result.Explanation="完整路径匹配";return result;}
            FileRule unique=null;int matches=0;
            foreach(var rule in rules)if(String.Equals(BaseName(rule.File),BaseName(result.File),StringComparison.OrdinalIgnoreCase)){unique=rule;matches++;}
            if(matches==1&&unique.Background&&HasPath(result.File)&&HasPath(unique.File)&&!String.Equals(Normalize(result.File),Normalize(unique.File),StringComparison.OrdinalIgnoreCase)){
                result.Explanation="文件路径不同（未应用会议背景规则）";return result;
            }
            if(matches==1){result.Rule=unique.File;result.Seconds=unique.Seconds;result.Background=unique.Background;result.Matched=true;result.Explanation="唯一文件名匹配";}
            else {result.Ambiguous=matches>1;result.Explanation=matches>1?"同名文件有歧义":"该文件未设置专属时长";}
            return result;
        }
        public static int Resolve(List<FileRule> rules,int fallback,ShowInfo info){
            return Match(rules,fallback,info).Seconds;
        }
    }
    // Keep file identity across temporary metadata failures. A late file result
    // changes the duration, not the elapsed speech time. A different file starts
    // a new session only after stable observations, even when HWND is reused.
    public sealed class TimingSession {
        public string File="",Source="";
        public FileMatch Match=new FileMatch();
        public bool ManualChoice;
        string candidate="";
        double candidateSince;
        public void Start(ClockState clock,double now,ShowInfo show,List<FileRule> rules,int fallback){
            Match=FileTiming.Match(rules,fallback,show);File=Match.File;Source=Match.Source;ManualChoice=false;candidate="";clock.Start(now,Match.Seconds);
        }
        public int Update(ClockState clock,double now,ShowInfo show,List<FileRule> rules,int fallback){
            FileMatch next=FileTiming.Match(rules,fallback,show);
            if(next.File.Length==0){candidate="";return 0;}
            if(File.Length>0&&!FileTiming.SameFile(File,next.File)){
                if(!String.Equals(candidate,FileTiming.Normalize(next.File),StringComparison.OrdinalIgnoreCase)){candidate=FileTiming.Normalize(next.File);candidateSince=now;return 0;}
                if(now-candidateSince<.35)return 0;
                Start(clock,now,show,rules,fallback);return 2;
            }
            candidate="";
            if(File.Length>0&&FileTiming.HasPath(File)&&!FileTiming.HasPath(next.File))return 0;
            bool learned=!String.Equals(FileTiming.Normalize(File),FileTiming.Normalize(next.File),StringComparison.OrdinalIgnoreCase);
            File=next.File;Source=next.Source;
            if(learned&&!ManualChoice){Match=next;clock.ChangeDuration(now,next.Seconds);return 1;}
            return 0;
        }
        public void Choose(ClockState clock,double now,FileRule rule){ManualChoice=true;Match=new FileMatch{File=File,Rule=rule.File,Seconds=rule.Seconds,Matched=true,Source="本场手动指定",Explanation="本场手动指定"};clock.ChangeDuration(now,rule.Seconds);}
    }
    // Background slides interrupt a speech without consuming its remaining time.
    public sealed class BackgroundHold {
        public bool Hidden {get;private set;}
        public bool Suspended {get;private set;}
        public string BackgroundFile {get;private set;}
        string speechFile="",candidateFile="";bool resumeRunning;double candidateAt,lastSeen;
        // 0: normal; 1: keep hidden; 2: resume same speech; 3: start a different document.
        public int Observe(ClockState clock,double now,string currentSpeech,FileMatch match){
            if(Suspended)return 0;
            if(match!=null&&match.Matched&&match.Background){
                if(!Hidden){speechFile=currentSpeech;resumeRunning=clock.Active&&!clock.Paused;if(resumeRunning)clock.TogglePause(now);}
                Hidden=true;BackgroundFile=match.File;candidateFile="";return 1;
            }
            if(!Hidden)return 0;
            // Brief gaps and missing/ambiguous metadata must not resume a hidden clock.
            if(match==null||match.File.Length==0||match.Ambiguous){candidateFile="";return 1;}
            string next=FileTiming.Normalize(match.File);
            if(!String.Equals(candidateFile,next,StringComparison.OrdinalIgnoreCase)||now-lastSeen>1.2){candidateFile=next;candidateAt=lastSeen=now;return 1;}
            lastSeen=now;if(now-candidateAt<.35)return 1;
            bool same=clock.Active&&FileTiming.SameFile(speechFile,match.File);
            if(same&&resumeRunning&&clock.Paused)clock.TogglePause(now);
            Clear();return same?2:3;
        }
        public void SuspendMonitoring(){if(Hidden){Hidden=false;Suspended=true;candidateFile="";}}
        public void ResumeMonitoring(){if(Suspended){Suspended=false;Hidden=true;candidateFile="";}}
        public void Clear(){Hidden=false;Suspended=false;BackgroundFile="";speechFile="";candidateFile="";resumeRunning=false;}
    }
    public static class ShowSelection {
        public static ShowInfo Select(List<ShowInfo> windows,ShowInfo previous){
            ShowInfo first=null,retained=null;
            foreach(var w in windows){if(!w.IsShow)continue;if(w.Foreground)return w;if(first==null)first=w;if(previous!=null&&w.Handle==previous.Handle)retained=w;}
            foreach(var front in windows){
                if(!front.Foreground||front.IsShow||!IsPresenter(front))continue;
                ShowInfo byOwner=null,byFile=null;int owners=0,files=0;
                string frontFile=FileTiming.GetFile(front);
                foreach(var w in windows){
                    if(!w.IsShow||front.ProcessId<=0||w.ProcessId!=front.ProcessId)continue;
                    string showFile=FileTiming.GetFile(w);
                    if(frontFile.Length>0&&showFile.Length>0&&!FileTiming.SameFile(frontFile,showFile))continue;
                    if((front.Handle!=IntPtr.Zero&&w.Owner==front.Handle)||(w.Handle!=IntPtr.Zero&&front.Owner==w.Handle)||(front.Owner!=IntPtr.Zero&&front.Owner==w.Owner)){byOwner=w;owners++;}
                    if(FileTiming.SameFile(frontFile,showFile)){byFile=w;files++;}
                }
                if(owners==1)return byOwner;if(files==1)return byFile;
            }
            return retained??first;
        }
        static bool IsPresenter(ShowInfo w){return w.Title.IndexOf("演讲者",StringComparison.OrdinalIgnoreCase)>=0||w.Title.IndexOf("presenter",StringComparison.OrdinalIgnoreCase)>=0;}
    }
    // Native window metadata is associated only through exact HWND, owner, or
    // unique same-process document evidence. Never pick a rule from process name.
    public sealed class WindowFiles {
        sealed class Recent {public ShowInfo Info;public double At;}
        readonly Dictionary<int,Recent> editors=new Dictionary<int,Recent>();
        readonly Dictionary<IntPtr,ShowInfo> bindings=new Dictionary<IntPtr,ShowInfo>();
        HashSet<IntPtr> previous=new HashSet<IntPtr>();
        static string Family(string name){return name=="wps"||name=="wpp"?"wps":name.ToLowerInvariant();}
        static void CopyFile(ShowInfo target,ShowInfo from,string source){target.File=from.File;target.FileSource=source;}
        public void Enrich(List<ShowInfo> list,List<ShowInfo> office,List<FileRule> rules,double now){
            foreach(var w in list){var match=FileTiming.Match(rules,0,w);if(match.File.Length>0){w.File=match.File;w.FileSource=match.Source;}
                if(w.Foreground&&!w.IsShow&&w.ProcessId>0&&w.File.Length>0)editors[w.ProcessId]=new Recent{Info=w,At=now};}
            foreach(var w in list)foreach(var extra in office)if(extra.Handle!=IntPtr.Zero&&extra.Handle==w.Handle){
                if(extra.File.Length>0)CopyFile(w,extra,"Office 放映接口");w.Page=extra.Page;w.Pages=extra.Pages;w.IsShow=true;w.Reason="Office 放映接口";break;}
            var current=new HashSet<IntPtr>();
            foreach(var w in list){if(!w.IsShow)continue;current.Add(w.Handle);
                if(w.File.Length==0){
                    // Some WPS versions provide Presentation but no SlideShowWindow.HWND.
                    ShowInfo one=null;int nativeCount=0,officeCount=0;
                    foreach(var n in list)if(n.IsShow&&Family(n.Process)==Family(w.Process))nativeCount++;
                    foreach(var o in office)if(o.Handle==IntPtr.Zero&&o.File.Length>0&&Family(o.Process)==Family(w.Process)){one=o;officeCount++;}
                    if(nativeCount==1&&officeCount==1){CopyFile(w,one,"Office 接口（唯一放映窗口关联）");w.Page=one.Page;w.Pages=one.Pages;}
                }
                if(w.File.Length==0&&w.ProcessId>0&&w.Owner!=IntPtr.Zero)foreach(var e in list)
                    if(e.Handle==w.Owner&&e.ProcessId==w.ProcessId&&e.File.Length>0){CopyFile(w,e,"放映所属编辑窗口");break;}
                ShowInfo bound;
                if(w.File.Length==0&&previous.Contains(w.Handle)&&bindings.TryGetValue(w.Handle,out bound)&&bound.ProcessId==w.ProcessId)CopyFile(w,bound,bound.FileSource+"（保持）");
                Recent recent;
                if(w.File.Length==0&&!previous.Contains(w.Handle)&&w.ProcessId>0&&editors.TryGetValue(w.ProcessId,out recent)&&now-recent.At<=15)
                    CopyFile(w,recent.Info,"放映前的前台编辑窗口");
                if(w.File.Length==0&&w.ProcessId>0){
                    var docs=new Dictionary<string,ShowInfo>(StringComparer.OrdinalIgnoreCase);int shows=0;
                    foreach(var e in list)if(e.ProcessId==w.ProcessId){if(e.IsShow)shows++;else if(e.File.Length>0)docs[FileTiming.Normalize(e.File)]=e;}
                    if(shows==1&&docs.Count==1)foreach(var e in docs.Values)CopyFile(w,e,"同进程唯一文档窗口");
                }
                if(w.File.Length>0)bindings[w.Handle]=new ShowInfo{File=w.File,FileSource=w.FileSource.Replace("（保持）",""),ProcessId=w.ProcessId};
            }
            foreach(var h in new List<IntPtr>(bindings.Keys))if(!current.Contains(h))bindings.Remove(h);
            foreach(var pid in new List<int>(editors.Keys))if(now-editors[pid].At>15)editors.Remove(pid);
            previous=current;
        }
    }
    // Evaluate actual screen geometry, separately from WS_CAPTION. Adobe can
    // retain window style flags while its client area covers the full monitor.
    public static class FullscreenPolicy {
        public static bool CoversMonitor(Rectangle area,Rectangle monitor,int tolerance){
            return monitor.Width>0&&monitor.Height>0&&area.Width>0&&area.Height>0&&
                Math.Abs((long)area.Left-monitor.Left)<=tolerance&&Math.Abs((long)area.Top-monitor.Top)<=tolerance&&
                Math.Abs((long)area.Right-monitor.Right)<=tolerance&&Math.Abs((long)area.Bottom-monitor.Bottom)<=tolerance;
        }
        public static bool IsPdf(ShowInfo w){
            string p=w.Process.ToLowerInvariant();
            if(p=="acrobat"||p=="acrord32"||p=="wpspdf"||p=="foxitpdfreader"||p=="sumatrapdf")return true;
            return (p=="wps"||p=="wpp")&&FileTiming.GetFile(w).EndsWith(".pdf",StringComparison.OrdinalIgnoreCase);
        }
        public static bool Evaluate(ShowInfo w,bool compatible,out string reason){
            if(!w.Visible||w.Minimized){reason="窗口隐藏或最小化";return false;}
            string c=w.ClassName.ToLowerInvariant(),t=w.Title;
            bool presenter=t.IndexOf("演讲者",StringComparison.OrdinalIgnoreCase)>=0||t.IndexOf("presenter",StringComparison.OrdinalIgnoreCase)>=0;
            bool explicitPpt=c=="screenclass"||c.Contains("slideshow")||c.Contains("wppshow")||t.StartsWith("幻灯片放映",StringComparison.OrdinalIgnoreCase)||t.StartsWith("PowerPoint Slide Show",StringComparison.OrdinalIgnoreCase)||t.StartsWith("WPS Presentation Slide Show",StringComparison.OrdinalIgnoreCase);
            if(!presenter&&explicitPpt){reason="PPT 放映窗口特征";return true;}
            if(IsPdf(w)){
                bool clientFull=w.ClientValid&&CoversMonitor(w.ClientBounds,w.MonitorBounds,4);
                // With no client-area result, accept only a borderless full window.
                bool borderlessFull=!w.Caption&&CoversMonitor(w.WindowBounds,w.MonitorBounds,8)&&(!w.ClientValid||CoversMonitor(w.ClientBounds,w.MonitorBounds,8));
                if(clientFull||borderlessFull){reason=clientFull?"PDF 全屏（内容区域覆盖屏幕）":"PDF 全屏（无边框窗口）";return true;}
                reason="PDF 窗口未覆盖全屏内容区域";return false;
            }
            if(compatible&&!presenter&&!w.Caption&&!String.Equals(w.Process,"wps",StringComparison.OrdinalIgnoreCase)&&CoversMonitor(w.WindowBounds,w.MonitorBounds,12)){
                reason="无边框全屏（兼容识别）";return true;
            }
            reason="编辑窗口 / 未识别为放映";return false;
        }
    }
    public static class Native {
        public delegate bool EnumProc(IntPtr h,IntPtr p);
        [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb,IntPtr p);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder b,int n);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
        [StructLayout(LayoutKind.Sequential)] struct NativePoint {public int X,Y;}
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h,out Rect r);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h,ref NativePoint p);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] public static extern int GetWindowLong(IntPtr h,int i);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")] public static extern int SetWindowLong(IntPtr h,int i,int v);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] public static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h,uint command);
        public static int Pid(IntPtr h){uint pid;GetWindowThreadProcessId(h,out pid);return (int)pid;}
        [DllImport("winmm.dll",CharSet=CharSet.Unicode)] public static extern int mciSendString(string command,StringBuilder result,int size,IntPtr callback);
        public static List<ShowInfo> Scan(bool compatible) {
            var procs=new Dictionary<int,string>();
            foreach(string name in new string[]{"wpp","wps","POWERPNT","wpspdf","Acrobat","AcroRd32","FoxitPDFReader","SumatraPDF"})
                foreach(var p in Process.GetProcessesByName(name)){try{procs[p.Id]=name;}finally{p.Dispose();}}
            var list=new List<ShowInfo>();
            EnumWindows(delegate(IntPtr h,IntPtr unused){
                uint pid;GetWindowThreadProcessId(h,out pid);string proc;
                if(!procs.TryGetValue((int)pid,out proc))return true;
                Rect r;if(!GetWindowRect(h,out r)||r.Right-r.Left<160||r.Bottom-r.Top<100)return true;
                var title=new StringBuilder(1024);var cls=new StringBuilder(256);GetWindowText(h,title,title.Capacity);GetClassName(h,cls,cls.Capacity);
                var info=new ShowInfo {Handle=h,Owner=GetWindow(h,4),ProcessId=(int)pid,Foreground=GetForegroundWindow()==h,Title=title.ToString(),ClassName=cls.ToString(),Process=proc,
                    WindowBounds=Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom),MonitorBounds=Screen.FromHandle(h).Bounds,
                    Caption=(GetWindowLong(h,-16)&0x00C00000)!=0,Visible=IsWindowVisible(h),Minimized=IsIconic(h)};
                Rect client;
                if(GetClientRect(h,out client)){
                    var topLeft=new NativePoint{X=client.Left,Y=client.Top};var bottomRight=new NativePoint{X=client.Right,Y=client.Bottom};
                    if(ClientToScreen(h,ref topLeft)&&ClientToScreen(h,ref bottomRight)){info.ClientValid=true;info.ClientBounds=Rectangle.FromLTRB(topLeft.X,topLeft.Y,bottomRight.X,bottomRight.Y);}
                }
                info.IsShow=FullscreenPolicy.Evaluate(info,compatible,out info.Reason);
                if(!info.Visible&&FileTiming.GetFile(info).Length==0)return true;
                list.Add(info);return true;
            },IntPtr.Zero);return list;
        }
    }

    // Optional COM enrichment on a separate STA thread. Never launches Office.
    // A hung/unavailable automation interface cannot block the tray or clock.
    public sealed class OfficeProbe : IDisposable {
        volatile bool closed, requested;
        readonly object gate=new object();
        List<ShowInfo> latest=new List<ShowInfo>();
        readonly Stopwatch age=Stopwatch.StartNew();
        double published=-100;
        readonly Thread thread;
        public volatile string Diagnostic="Office 接口尚未查询";
        public OfficeProbe(){thread=new Thread(Worker);thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();}
        public List<ShowInfo> Snapshot(){requested=true;lock(gate){return age.Elapsed.TotalSeconds-published<2?new List<ShowInfo>(latest):new List<ShowInfo>();}}
        static void Release(object o){if(o!=null&&Marshal.IsComObject(o))try{Marshal.ReleaseComObject(o);}catch{}}
        public static ShowInfo ReadWindow(object window,string process){
            var info=new ShowInfo{Process=process,IsShow=true,FileSource="Office 放映接口",Reason="Office 放映接口"};
            object presentation=null,view=null,slide=null,slides=null;dynamic w=window;
            // HWND and page metadata are optional. Failure must never discard FullName.
            try {presentation=w.Presentation;dynamic p=presentation;try{info.File=Convert.ToString(p.FullName);}catch{info.File=Convert.ToString(p.Name);} }
            catch(Exception e){info.Reason="Office 文件信息不可用："+e.Message;}
            try {info.Handle=new IntPtr(Convert.ToInt64(w.HWND));}catch{}
            try {view=w.View;dynamic v=view;slide=v.Slide;dynamic s=slide;info.Page=(int)s.SlideIndex;}catch{}
            try {if(presentation!=null){dynamic p=presentation;slides=p.Slides;dynamic ss=slides;info.Pages=(int)ss.Count;}}catch{}
            Release(slides);Release(slide);Release(view);Release(presentation);return info;
        }
        void Worker(){
            while(!closed){if(!requested){Thread.Sleep(100);continue;}requested=false;var found=new List<ShowInfo>();var messages=new StringBuilder();
                foreach(string prog in new string[]{"kwpp.Application","PowerPoint.Application"}){
                    object app=null,windows=null;
                    try {
                        app=Marshal.GetActiveObject(prog);dynamic a=app;windows=a.SlideShowWindows;dynamic ws=windows;
                        int count=Math.Min(8,(int)ws.Count);
                        for(int i=1;i<=count;i++){
                            object window=null;
                            try{
                                window=ws.Item(i);var info=ReadWindow(window,prog.StartsWith("kwpp")?"wpp":"POWERPNT");found.Add(info);messages.AppendLine(info.Reason);
                            }catch(Exception e){messages.AppendLine(prog+"："+e.Message);}finally{Release(window);}
                        }
                    }catch(Exception e){messages.AppendLine(prog+"："+e.GetType().Name);}finally{Release(windows);Release(app);}
                }
                Diagnostic=messages.Length==0?"Office 接口未报告放映窗口":messages.ToString();lock(gate){latest=found;published=age.Elapsed.TotalSeconds;}Thread.Sleep(300);
            }
        }
        public void Dispose(){closed=true;}
    }

    public sealed class Audio : IDisposable {
        MemoryStream nearStream,endStream;
        SoundPlayer near,end;
        public Audio(){nearStream=Wave(false);endStream=Wave(true);near=new SoundPlayer(nearStream);end=new SoundPlayer(endStream);near.Load();end.Load();}
        public static MemoryStream Wave(bool final){
            const int rate=22050;byte[] pcm;
            using(var data=new MemoryStream()){
                var writer=new BinaryWriter(data);int[] notes=final?new int[]{880,880,1175}:new int[]{880,1100};
                for(int n=0;n<notes.Length;n++){int count=(int)(rate*(n==notes.Length-1?.4:.2));for(int i=0;i<count;i++){
                    double env=Math.Max(0,Math.Min(1,Math.Min(i/(rate*.015),(count-i)/(rate*.04))));
                    writer.Write((short)(Math.Sin(2*Math.PI*notes[n]*i/rate)*10000*env));}
                    for(int i=0;i<rate/9;i++)writer.Write((short)0);
                }writer.Flush();pcm=data.ToArray();
            }
            var stream=new MemoryStream();var wave=new BinaryWriter(stream);
            wave.Write(Encoding.ASCII.GetBytes("RIFF"));wave.Write(36+pcm.Length);wave.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            wave.Write(16);wave.Write((short)1);wave.Write((short)1);wave.Write(rate);wave.Write(rate*2);wave.Write((short)2);wave.Write((short)16);
            wave.Write(Encoding.ASCII.GetBytes("data"));wave.Write(pcm.Length);wave.Write(pcm);wave.Flush();stream.Position=0;return stream;
        }
        public void Stop(){Native.mciSendString("close WpsClockAlert",null,0,IntPtr.Zero);near.Stop();end.Stop();}
        public void Play(bool final,string file){
            Stop();
            if(!String.IsNullOrWhiteSpace(file)){
                string full=Path.IsPathRooted(file)?file:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,file);
                if(!File.Exists(full))throw new FileNotFoundException("提示音文件不存在",full);
                if(full.Contains("\""))throw new IOException("提示音路径不能包含引号");
                int result=Native.mciSendString("open \""+full+"\" alias WpsClockAlert",null,0,IntPtr.Zero);
                if(result==0)result=Native.mciSendString("play WpsClockAlert from 0",null,0,IntPtr.Zero);
                if(result!=0)throw new IOException("无法播放此音频（错误 "+result+"）。请改用 PCM WAV 或 MP3。");
            }else (final?end:near).Play();
        }
        public void Dispose(){Stop();near.Dispose();end.Dispose();nearStream.Dispose();endStream.Dispose();}
    }

    // Pure geometry shared by all monitors. Resizing never changes the opposite edge.
    public static class PanelGeometry {
        public static int BarHeight(float dpi,int panelHeight){return Math.Min(Math.Max(0,panelHeight/2),(int)Math.Ceiling(28*dpi/96));}
        public static Size MergeSize(Size latest,Size shown,bool modeEdited,bool automatic,bool widthEdited,bool heightEdited){
            if(modeEdited&&automatic)return Size.Empty;
            return new Size(modeEdited||widthEdited?shown.Width:latest.Width,modeEdited||heightEdited?shown.Height:latest.Height);
        }
        public static int Alpha(int percent){return (Math.Max(0,Math.Min(100,percent))*255+50)/100;}
        public static int Hit(Point p,Size size){int edge=0;if(p.X<8)edge|=1;else if(p.X>=size.Width-8)edge|=2;if(p.Y<8)edge|=4;else if(p.Y>=size.Height-8)edge|=8;return edge;}
        public static Rectangle Resize(Rectangle start,Point delta,int edge,Rectangle screen){
            int l=start.Left,r=start.Right,t=start.Top,b=start.Bottom;
            int minW=Math.Min(120,screen.Width),minH=Math.Min(60,screen.Height);
            if((edge&1)!=0)l=Math.Max(screen.Left,Math.Min(r-minW,l+delta.X));
            if((edge&2)!=0)r=Math.Min(screen.Right,Math.Max(l+minW,r+delta.X));
            if((edge&4)!=0)t=Math.Max(screen.Top,Math.Min(b-minH,t+delta.Y));
            if((edge&8)!=0)b=Math.Min(screen.Bottom,Math.Max(t+minH,b+delta.Y));
            return Rectangle.FromLTRB(l,t,r,b);
        }
    }
    // UpdateLayeredWindow keeps text opaque while the background has its own alpha.
    // Do not use Form.Opacity or TransparencyKey on this window.
    static class LayeredPanel {
        [StructLayout(LayoutKind.Sequential)] struct XY {public int X,Y;public XY(int x,int y){X=x;Y=y;}}
        [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend {public byte Op,Flags,Alpha,Format;}
        [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dst,ref XY point,ref XY size,IntPtr src,ref XY origin,uint key,ref Blend blend,uint flags);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        public static void Present(Form window,Bitmap bitmap){
            IntPtr dc=CreateCompatibleDC(IntPtr.Zero),dib=IntPtr.Zero,old=IntPtr.Zero;
            if(dc==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
            try{dib=bitmap.GetHbitmap(Color.FromArgb(0));old=SelectObject(dc,dib);
                var p=new XY(window.Left,window.Top);var size=new XY(bitmap.Width,bitmap.Height);var origin=new XY(0,0);var blend=new Blend{Alpha=255,Format=1};
                if(!UpdateLayeredWindow(window.Handle,IntPtr.Zero,ref p,ref size,dc,ref origin,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }finally{if(old!=IntPtr.Zero)SelectObject(dc,old);if(dib!=IntPtr.Zero)DeleteObject(dib);DeleteDC(dc);}
        }
    }
    public sealed class ClockWindow : Form {
        readonly Controller controller;
        public readonly string ScreenKey;
        public string Digits="08:00", Information="";
        public bool WarningState;
        public double Animation;
        Point dragMouse;Rectangle dragBounds;
        bool dragging;int resizeEdge;string painted="";bool renderFailed;float dpi=96;
        Font timerFont,infoFont;
        readonly Font barFont=new Font("Microsoft YaHei UI",9,FontStyle.Regular,GraphicsUnit.Point);
        int BarHeight {get{return HasBar?PanelGeometry.BarHeight(dpi,Height):0;}}
        public bool IsDragging {get{return dragging;}}
        bool HasBar {get{return controller.Config.ControlBar&&!controller.Config.Locked;}}
        public ClockWindow(Controller owner,string screen){
            controller=owner;ScreenKey=screen;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
            StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
            ContextMenuStrip=owner.MakeMenu();ApplyAppearance();
        }
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x80000;return p;}}
        protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
        public void ApplyAppearance(){
            Settings s=controller.Config;
            if(timerFont!=null)timerFont.Dispose();if(infoFont!=null)infoFont.Dispose();
            timerFont=new Font(s.FontName,s.FontSize,s.FontBold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Point);
            infoFont=new Font(s.InfoFontName,s.InfoFontSize,FontStyle.Regular,GraphicsUnit.Point);
            Cursor=s.Locked?Cursors.Default:Cursors.SizeAll;
            int style=Native.GetWindowLong(Handle,-20);Native.SetWindowLong(Handle,-20,s.Locked?style|0x20:style&~0x20);
            using(Graphics metrics=CreateGraphics())dpi=metrics.DpiX;
            painted="";ResizeForText();Render();
        }
        public void ResizeForText(){
            if(dragging)return;
            Settings s=controller.Config;Size measured=TextRenderer.MeasureText(Digits,timerFont,Size.Empty,TextFormatFlags.NoPadding);
            Size info=TextRenderer.MeasureText(Information.Length==0?" ":Information,infoFont);
            int w=s.PanelWidth>0?s.PanelWidth:Math.Max(120,Math.Max(measured.Width+18,Information.Length>0?info.Width+12:0));
            int h=s.PanelHeight>0?s.PanelHeight:Math.Max(60,measured.Height+6+(Information.Length>0?info.Height:0)+(HasBar?(int)Math.Ceiling(28*dpi/96):0));
            Rectangle bounds=controller.ScreenBounds(ScreenKey);w=Math.Min(w,bounds.Width);h=Math.Min(h,bounds.Height);
            if(Size!=new Size(w,h)){Size=new Size(w,h);controller.Position(this);}
        }
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){Render();}
        public void Render(){
            if(Width<1||Height<1||timerFont==null)return;
            Settings s=controller.Config;int alpha=PanelGeometry.Alpha(controller.BackgroundOpacity);
            string key=Digits+"|"+Information+"|"+WarningState+"|"+Size+"|"+Animation.ToString("F3")+"|"+alpha+"|"+HasBar;
            if(key==painted)return;
            try{using(var bitmap=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)){
                bitmap.SetResolution(dpi,dpi);
                using(Graphics g=Graphics.FromImage(bitmap)){
                    // Alpha 1 preserves mouse hit testing when the background is visually transparent.
                    Color bg=Color.FromArgb(WarningState?s.WarningBackground:s.Background);
                    g.Clear(Color.FromArgb(s.Locked?alpha:Math.Max(1,alpha),bg));g.SmoothingMode=SmoothingMode.AntiAlias;
                    g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    Color fg=Color.FromArgb(255,Color.FromArgb(WarningState?s.WarningForeground:s.Foreground));
                    if(Animation>0&&s.Blink)fg=Mix(fg,bg,Animation*.55);
                    int footer=BarHeight,infoHeight=Information.Length>0?Math.Min((int)Math.Ceiling(infoFont.GetHeight(g))+4,Math.Max(0,(Height-footer)/3)):0;
                    var rect=new RectangleF(5,1,Math.Max(1,Width-10),Math.Max(1,Height-footer-infoHeight-2));
                    using(var brush=new SolidBrush(fg))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap}){
                        SizeF measure=g.MeasureString(Digits,timerFont);float ratio=Math.Min(1,Math.Min(rect.Width/Math.Max(1,measure.Width),rect.Height/Math.Max(1,measure.Height)));
                        using(var fitted=new Font(timerFont.FontFamily,Math.Max(1,timerFont.Size*ratio),timerFont.Style))g.DrawString(Digits,fitted,brush,rect,format);
                        if(infoHeight>0)using(var fittedInfo=new Font(infoFont.FontFamily,Math.Max(1,infoFont.Size*Math.Min(1,infoHeight/infoFont.GetHeight(g))),infoFont.Style))g.DrawString(Information,fittedInfo,brush,new RectangleF(4,Height-footer-infoHeight,Width-8,infoHeight),format);
                        if(HasBar){string[] labels={"Ⅱ / ▶","重置","设置"};for(int i=0;i<3;i++){var cell=new RectangleF(i*Width/3f,Height-footer,Width/3f,footer);using(var fill=new SolidBrush(Color.FromArgb(220,bg)))g.FillRectangle(fill,cell);SizeF labelSize=g.MeasureString(labels[i],barFont);
                            float fit=Math.Min(1,Math.Min(Math.Max(1,cell.Width-6)/labelSize.Width,Math.Max(1,cell.Height-4)/labelSize.Height));
                            using(var fittedBar=new Font(barFont.FontFamily,Math.Max(1,barFont.Size*fit),barFont.Style))g.DrawString(labels[i],fittedBar,brush,cell,format);}}
                    }
                    if(Animation>0)using(var pen=new Pen(fg,1+(float)Animation*2))g.DrawRectangle(pen,1,1,Width-3,Math.Max(1,Height-footer-3));
                }
                LayeredPanel.Present(this,bitmap);
            }painted=key;renderFailed=false;}catch(Exception e){if(!renderFailed){renderFailed=true;controller.Notify("计时面板绘制失败，请从托盘打开设置或重启："+e.Message);}}
        }
        static Color Mix(Color a,Color b,double amount){return Color.FromArgb((int)(a.R*(1-amount)+b.R*amount),(int)(a.G*(1-amount)+b.G*amount),(int)(a.B*(1-amount)+b.B*amount));}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||controller.Config.Locked)return;
            resizeEdge=PanelGeometry.Hit(e.Location,Size);
            if(resizeEdge==0&&HasBar&&e.Y>=Height-BarHeight){int cell=Math.Min(2,e.X*3/Width);if(cell==0)controller.Pause();else if(cell==1)controller.Reset();else controller.OpenSettings();return;}
            dragging=true;dragMouse=MousePosition;dragBounds=Bounds;Capture=true;
        }
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);
            if(dragging){Point m=MousePosition;var delta=new Point(m.X-dragMouse.X,m.Y-dragMouse.Y);
                if(resizeEdge==0)Location=controller.Clamp(this,new Point(dragBounds.X+delta.X,dragBounds.Y+delta.Y));
                else{Bounds=PanelGeometry.Resize(dragBounds,delta,resizeEdge,controller.ScreenBounds(ScreenKey));Render();}return;}
            int edge=PanelGeometry.Hit(e.Location,Size);Cursor=controller.Config.Locked?Cursors.Default:edge==5||edge==10?Cursors.SizeNWSE:edge==6||edge==9?Cursors.SizeNESW:(edge&3)!=0?Cursors.SizeWE:edge!=0?Cursors.SizeNS:Cursors.SizeAll;
        }
        void EndDrag(){if(!dragging)return;dragging=false;Capture=false;if(resizeEdge!=0)controller.RememberSize(this);controller.RememberPosition(this);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left)EndDrag();}
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)EndDrag();}
        protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(e.Button==MouseButtons.Left&&PanelGeometry.Hit(e.Location,Size)==0)controller.OpenSettings();}
        protected override void Dispose(bool disposing){if(disposing){barFont.Dispose();if(timerFont!=null)timerFont.Dispose();if(infoFont!=null)infoFont.Dispose();if(ContextMenuStrip!=null)ContextMenuStrip.Dispose();}base.Dispose(disposing);}
    }

    public sealed class MessageWindow : NativeWindow,IDisposable {
        readonly Controller owner;
        public static readonly uint WakeMessage=Native.RegisterWindowMessage("WpsClock.ShowSettings.v2");
        public MessageWindow(Controller c){owner=c;CreateHandle(new CreateParams{Caption="WpsClock.MessageWindow"});}
        protected override void WndProc(ref Message m){if((uint)m.Msg==WakeMessage)owner.OpenSettings();else if(m.Msg==0x0312){if(m.WParam.ToInt32()==1)owner.Pause();if(m.WParam.ToInt32()==2)owner.Reset();}base.WndProc(ref m);}
        public void Dispose(){Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);DestroyHandle();}
    }
    public sealed class Blackout : Form {
        public Blackout(Screen screen){Bounds=screen.Bounds;FormBorderStyle=FormBorderStyle.None;TopMost=true;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Color.Black;ForeColor=Color.White;KeyPreview=true;
            var label=new Label{Text="时间到\r\n\r\n按 Esc 或双击返回放映",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Microsoft YaHei UI",24)};
            Controls.Add(label);label.DoubleClick+=delegate{Close();};KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape)Close();};}
    }

    public sealed class Controller : ApplicationContext {
        public Settings Config;
        public readonly SettingsStore Store;
        public readonly ClockState State=new ClockState();
        readonly TimingSession session=new TimingSession();
        readonly BackgroundHold background=new BackgroundHold();
        bool HasTimedRules {get{foreach(var rule in Config.Rules)if(!rule.Background)return true;return false;}}
        readonly WindowFiles windowFiles=new WindowFiles();
        readonly Stopwatch monotonic=Stopwatch.StartNew();
        readonly NotifyIcon tray=new NotifyIcon();
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly List<ClockWindow> windows=new List<ClockWindow>();
        readonly List<Blackout> blackouts=new List<Blackout>();
        readonly OfficeProbe probe=new OfficeProbe();
        readonly Audio audio;
        readonly MessageWindow messages;
        SettingsDialog settings;
        int openMenus;
        ContextMenuStrip activeMenu;
        int previewOpacity=-1;
        public int BackgroundOpacity {get{return previewOpacity>=0?previewOpacity:Config.BackgroundOpacity;}}
        bool Editing {get{return openMenus>0||(settings!=null&&!settings.IsDisposed&&settings.Visible);}}
        public void PreviewOpacity(int value){previewOpacity=value;foreach(var w in windows)w.Render();}
        public Size CurrentPanelSize {get{return windows.Count>0?windows[0].Size:new Size(240,100);}}
        public Rectangle ScreenBounds(string key){return ScreenFor(key).Bounds;}
        public void RememberSize(ClockWindow w){Config.PanelWidth=w.Width;Config.PanelHeight=w.Height;foreach(var other in windows)if(other!=w)other.ResizeForText();SyncSizeEditor();}
        public void AutoSizePanels(){Config.PanelWidth=0;Config.PanelHeight=0;foreach(var w in windows)w.ResizeForText();SyncSizeEditor();Save();}
        void SyncSizeEditor(){if(settings!=null&&!settings.IsDisposed)settings.SyncPanelSize();}
        public ShowInfo Current;
        bool manualSession,preview,hidden,disposed,pageWarning,documentsOpen,matchWarning;
        double scanAt=-10,missingAt=-1,candidateAt=-1,alertAt=-100,previewAt,sessionAt;
        IntPtr candidate=IntPtr.Zero;
        int page;
        string diagnostics="等待检测 WPS 放映。";
        public string HotkeyWarning="";
        public double Now {get{return monotonic.Elapsed.TotalSeconds;}}
        public Controller(){
            Store=new SettingsStore();Config=Store.Load();audio=new Audio();messages=new MessageWindow(this);
            tray.Icon=SystemIcons.Information;tray.Text="WpsClock：等待放映";tray.ContextMenuStrip=MakeMenu();tray.Visible=true;
            tray.DoubleClick+=delegate{OpenSettings();};RegisterKeys();SyncWindows();
            timer.Interval=50;timer.Tick+=delegate{Tick();};timer.Start();
            SystemEvents.DisplaySettingsChanged+=DisplayChanged;
            if(Store.Warning.Length>0)Notify(Store.Warning);
            if(Config.ShowHelp)ShowHelp();
        }
        void DisplayChanged(object sender,EventArgs e){if(windows.Count>0&&!windows[0].IsDisposed)windows[0].BeginInvoke((Action)delegate{SyncWindows();});}
        public ContextMenuStrip MakeMenu(){
            var menu=new ContextMenuStrip();
            menu.Opened+=delegate{openMenus++;activeMenu=menu;};menu.Closed+=delegate{openMenus=Math.Max(0,openMenus-1);if(activeMenu==menu)activeMenu=null;};
            menu.Items.Add("本地设置",null,delegate{OpenSettings();});menu.Items.Add(new ToolStripSeparator());
            var pause=new ToolStripMenuItem("开始 / 暂停计时",null,delegate{Pause();});menu.Items.Add(pause);
            var reset=new ToolStripMenuItem("重新计时",null,delegate{Reset();});menu.Items.Add(reset);
            menu.Items.Add("结束本次计时 / 返回待命",null,delegate{Finish();});
            var auto=new ToolStripMenuItem("自动监测放映",null,delegate{ToggleAuto();});menu.Items.Add(auto);
            var file=new ToolStripMenuItem("调整当前文件设置",null,delegate{OpenSettings();settings.SelectCurrentFile();});menu.Items.Add(file);
            var choose=new ToolStripMenuItem("本场选择文件时长");menu.Items.Add(choose);
            menu.Items.Add(new ToolStripSeparator());
            var locked=new ToolStripMenuItem("锁定位置 / 点击穿透",null,delegate{Config.Locked=!Config.Locked;foreach(var w in windows)w.ApplyAppearance();Save();});menu.Items.Add(locked);
            menu.Items.Add("恢复右上角位置",null,delegate{Config.Positions.Clear();foreach(var w in windows)Position(w);Save();});
            menu.Items.Add("恢复自动尺寸",null,delegate{AutoSizePanels();});
            var hide=new ToolStripMenuItem("隐藏计时窗口",null,delegate{hidden=!hidden;});menu.Items.Add(hide);
            menu.Items.Add("高手入门",null,delegate{ShowHelp();});menu.Items.Add("关于",null,delegate{OpenSettings();settings.SelectAbout();});
            menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,delegate{ExitThread();});
            menu.Opening+=delegate{auto.Checked=Config.Auto;locked.Checked=Config.Locked;hide.Text=hidden?"显示计时窗口":"隐藏计时窗口";pause.Enabled=Config.Manual&&!background.Hidden;reset.Enabled=Config.Manual&&!background.Hidden;file.Enabled=Current!=null&&FileName(Current).Length>0;
                while(choose.DropDownItems.Count>0){var item=choose.DropDownItems[0];choose.DropDownItems.RemoveAt(0);item.Dispose();}
                foreach(var rule in Config.Rules){if(rule.Background)continue;FileRule selected=rule;choose.DropDownItems.Add(rule.File+"  "+ClockState.Format(rule.Seconds),null,delegate{ChooseRule(selected);});}
                choose.Enabled=State.Active&&!preview&&!background.Hidden&&HasTimedRules;
            };return menu;
        }
        public void ToggleAuto(){
            Config.Auto=!Config.Auto;
            if(!Config.Auto&&background.Hidden){background.SuspendMonitoring();manualSession=State.Active;Current=null;}
            else if(Config.Auto&&!preview){manualSession=false;background.ResumeMonitoring();}
            Save();
        }
        void ShowHelp(){MessageBox.Show("1. 双击 EXE 即进入待命，无需打开命令窗口。\r\n2. WPS 全屏放映后自动计时，翻页不重置。\r\n3. 左键拖动小窗微调位置；右键进入本地设置。\r\n4. 关闭设置窗口仍在后台运行；从托盘菜单退出。\r\n5. 锁定位置后可从托盘取消锁定。","高手入门",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        public void Notify(string text){tray.BalloonTipTitle="WpsClock";tray.BalloonTipText=text.Length>250?text.Substring(0,250):text;tray.ShowBalloonTip(5000);}
        public bool Save(){try{Store.Save(Config);return true;}catch(Exception e){Notify("设置保存失败："+e.Message);return false;}}
        public void OpenSettings(){if(disposed)return;if(settings==null||settings.IsDisposed)settings=new SettingsDialog(this);PublishStatus();settings.Show();settings.WindowState=FormWindowState.Normal;settings.BringToFront();settings.Activate();}
        public void Apply(Settings s){Config=s;Config.Normalize();previewOpacity=-1;bool saved=Save();RegisterKeys();SyncWindows();foreach(var w in windows)w.ApplyAppearance();Notify(saved?"设置已保存。计时时长在下一场或重新计时时生效；会议背景标记将在下一次检测时应用。":"设置已应用，但未保存到磁盘；请检查设置目录是否可写。");}
        void RegisterKeys(){Native.UnregisterHotKey(messages.Handle,1);Native.UnregisterHotKey(messages.Handle,2);HotkeyWarning="";
            if(!Config.Manual)return;
            if(!Native.RegisterHotKey(messages.Handle,1,0x4000,(uint)Config.PauseKey))HotkeyWarning+="暂停快捷键已被占用。 ";
            if(!Native.RegisterHotKey(messages.Handle,2,0x4000,(uint)Config.ResetKey))HotkeyWarning+="重置快捷键已被占用。";
            if(HotkeyWarning.Length>0)Notify(HotkeyWarning);
        }
        Screen PresentationScreen(){return Current!=null?Screen.FromHandle(Current.Handle):Screen.PrimaryScreen;}
        Screen ScreenFor(string key){foreach(Screen s in Screen.AllScreens)if(s.DeviceName==key)return s;return Screen.PrimaryScreen;}
        void SyncWindows(){
            var desired=new List<string>();if(Config.SyncScreens)foreach(Screen s in Screen.AllScreens)desired.Add(s.DeviceName);else desired.Add(PresentationScreen().DeviceName);
            for(int i=windows.Count-1;i>=0;i--)if(!desired.Contains(windows[i].ScreenKey)){windows[i].Dispose();windows.RemoveAt(i);}
            foreach(string key in desired){bool exists=false;foreach(var w in windows)if(w.ScreenKey==key)exists=true;if(!exists){var w=new ClockWindow(this,key);windows.Add(w);Position(w);}}
        }
        public Point Clamp(ClockWindow w,Point p){Rectangle b=ScreenFor(w.ScreenKey).Bounds;return new Point(Math.Max(b.Left,Math.Min(b.Right-w.Width,p.X)),Math.Max(b.Top,Math.Min(b.Bottom-w.Height,p.Y)));}
        public void Position(ClockWindow w){if(w.IsDragging)return;Rectangle b=ScreenFor(w.ScreenKey).Bounds;Point p=new Point(b.Right-w.Width-16,b.Top+16);
            foreach(var pos in Config.Positions)if(pos.Screen==w.ScreenKey){p=new Point(b.Left+pos.X,b.Top+pos.Y);break;}
            p=Clamp(w,p);
            // Stay in the topmost band, but below our active menu/settings and its dialogs.
            IntPtr after=new IntPtr(-1);uint flags=0x0010|0x0200;
            if(activeMenu!=null&&!activeMenu.IsDisposed&&activeMenu.Visible)after=activeMenu.Handle;
            else if(settings!=null&&!settings.IsDisposed&&settings.Visible)after=settings.Handle;
            else if(Editing)flags|=0x0004;
            Native.SetWindowPos(w.Handle,after,p.X,p.Y,w.Width,w.Height,flags);
        }
        public void RememberPosition(ClockWindow w){Rectangle b=ScreenFor(w.ScreenKey).Bounds;WindowPosition stored=null;foreach(var p in Config.Positions)if(p.Screen==w.ScreenKey)stored=p;
            if(stored==null){stored=new WindowPosition{Screen=w.ScreenKey};Config.Positions.Add(stored);}stored.X=w.Left-b.Left;stored.Y=w.Top-b.Top;Save();}
        public static string FileName(ShowInfo info){return FileTiming.GetFile(info);}
        public int DurationFor(ShowInfo info){return FileTiming.Resolve(Config.Rules,Config.Duration,info);}
        public void Pause(){if(!Config.Manual||background.Hidden)return;if(background.Suspended)background.Clear();if(!State.Active){manualSession=true;preview=false;session.Start(State,Now,Current,Config.Rules,Config.Duration);}else if(!(Config.StopAtZero&&State.Ended))State.TogglePause(Now);}
        public void Reset(){if(!Config.Manual||background.Hidden)return;if(background.Suspended)background.Clear();CloseBlackouts();audio.Stop();alertAt=-100;
            if(State.Active){if(preview)State.Start(Now,8);else if(session.ManualChoice)State.Start(Now,session.Match.Seconds);else session.Start(State,Now,Current,Config.Rules,Config.Duration);}
            else{manualSession=true;session.Start(State,Now,null,Config.Rules,Config.Duration);}}
        public void Preview(){if(background.Hidden){MessageBox.Show("会议背景模式下不预览提醒，请先切换到发言文件。","提示");return;}if(State.Active&&!preview){MessageBox.Show("当前正在计时，请先结束本次计时再预览。","提示");return;}CloseBlackouts();manualSession=true;preview=true;previewAt=Now;State.Start(Now,8);alertAt=-100;hidden=false;}
        public void TestSound(bool final,string file){try{audio.Play(final,file);}catch(Exception e){MessageBox.Show(e.Message,"试听失败",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
        public void ChooseRule(FileRule rule){if(!State.Active||preview||background.Hidden||rule.Background)return;if(background.Suspended)background.Clear();session.Choose(State,Now,rule);alertAt=-100;audio.Stop();CloseBlackouts();PublishStatus();Notify("已应用本场时长 "+ClockState.Format(rule.Seconds)+"，保留已用时间。");}
        void Begin(ShowInfo show){background.Clear();Current=show;manualSession=false;preview=false;page=show.Page;pageWarning=false;matchWarning=false;sessionAt=Now;session.Start(State,Now,show,Config.Rules,Config.Duration);missingAt=-1;alertAt=-100;CloseBlackouts();SyncWindows();}
        void Finish(){if(background.Suspended)background.Clear();State.Stop();Current=null;manualSession=false;preview=false;candidate=IntPtr.Zero;candidateAt=-1;missingAt=-1;CloseBlackouts();audio.Stop();SyncWindows();}
        void CloseBlackouts(){foreach(var b in blackouts)if(!b.IsDisposed)b.Close();blackouts.Clear();}
        void Scan(double now){
            List<ShowInfo> list=Native.Scan(Config.Compatible);List<ShowInfo> enriched=probe.Snapshot();
            windowFiles.Enrich(list,enriched,Config.Rules,now);
            documentsOpen=false;foreach(var w in list)if(w.IsShow||FileName(w).Length>0)documentsOpen=true;
            ShowInfo found=null;var report=new StringBuilder();
            foreach(var w in list){report.AppendLine(w.Reason+" | "+w.Process+" | "+w.ClassName+" | PID "+w.ProcessId);report.AppendLine(w.Title);
                report.AppendLine("窗口="+w.WindowBounds+"\r\n内容="+(w.ClientValid?w.ClientBounds.ToString():"读取失败")+"\r\n屏幕="+w.MonitorBounds+" | 标题栏标记="+w.Caption);
                if(w.File.Length>0)report.AppendLine("文件："+w.File+"\r\n来源："+w.FileSource);if(w.Page>0)report.AppendLine("页码 "+w.Page+" / "+w.Pages);}
            diagnostics=(report.Length==0?"未发现可见的 WPS / PPT / PDF 窗口。":report.ToString())+"\r\n"+probe.Diagnostic+"\r\n"+HotkeyWarning;
            if(preview)return;
            found=ShowSelection.Select(list,Current);
            if(Config.Auto||background.Hidden){
                bool wasHidden=background.Hidden;
                FileMatch observed=found==null?null:FileTiming.Match(Config.Rules,Config.Duration,found);
                int transition=background.Observe(State,now,session.File,observed);
                if(transition==1){
                    if(observed!=null&&observed.Matched&&observed.Background)Current=found;missingAt=-1;candidate=IntPtr.Zero;candidateAt=-1;
                    if(!wasHidden){audio.Stop();CloseBlackouts();alertAt=-100;}
                    return;
                }
                if(transition==2){Current=found;page=found.Page;missingAt=-1;SyncWindows();return;}
                if(transition==3){Begin(found);return;}
            }
            if(State.Active&&!manualSession){
                if(found==null){if(missingAt<0)missingAt=now;if(now-missingAt>=Math.Max(.8,Config.Grace))Finish();return;}
                missingAt=-1;
                int update=session.Update(State,now,found,Config.Rules,Config.Duration);
                Current=found;
                if(update==2){Begin(found);return;}
                if(update==1){alertAt=-100;CloseBlackouts();audio.Stop();}
                if(Current.File.Length==0&&session.File.Length>0){Current.File=session.File;Current.FileSource=session.Source;}
                if(!Config.SyncScreens&&windows.Count>0&&windows[0].ScreenKey!=PresentationScreen().DeviceName)SyncWindows();
                if(Config.PerPage&&found.Page>0&&page>0&&found.Page!=page){State.Start(now,session.Match.Seconds);alertAt=-100;CloseBlackouts();}
                if(Config.PerPage&&found.Page==0&&!pageWarning){Notify("此 WPS 版本未提供页码接口，当前按整场计时。可在本地设置中查看检测状态。");pageWarning=true;}
                page=found.Page;return;
            }
            if(!Config.Auto||manualSession)return;
            if(found==null){candidate=IntPtr.Zero;candidateAt=-1;return;}
            if(found.Handle!=candidate){candidate=found.Handle;candidateAt=now;return;}
            if(now-candidateAt>=.35)Begin(found);
        }
        void Alert(int kind){alertAt=Now;bool final=kind==2;
            if(final?Config.EndSound:Config.NearSound)try{audio.Play(final,final?Config.EndFile:Config.NearFile);}catch(Exception e){Notify(e.Message);}
            if(!final||preview||Current==null)return;
            if(Config.EndAction==1){Native.PostMessage(Current.Handle,0x0100,new IntPtr(27),IntPtr.Zero);Native.PostMessage(Current.Handle,0x0101,new IntPtr(27),IntPtr.Zero);}
            if(Config.EndAction==2){var black=new Blackout(PresentationScreen());blackouts.Add(black);black.Show();}
        }
        void Tick(){
            double now=Now;bool scan=now-scanAt>=.4;
            if(scan){scanAt=now;try{Scan(now);}catch(Exception e){diagnostics="检测暂时失败："+e.Message;}PublishStatus();}
            bool unresolved=State.Active&&!background.Hidden&&!preview&&!manualSession&&HasTimedRules&&!session.Match.Matched;
            if(unresolved&&Now-sessionAt>=2&&!matchWarning){matchWarning=true;Notify(session.Match.Explanation+"，当前使用默认时长 "+ClockState.Format(State.Duration)+"。请核对本地设置中的当前匹配，必要时右键选择本场文件。");}
            int signal=background.Hidden||(unresolved&&Now-sessionAt<2)?0:State.Advance(Now,preview?4:Config.Warning,Config.StopAtZero);if(signal>0)Alert(signal);
            if(preview&&Now-previewAt>11){Finish();return;}
            bool warn=State.Warned||State.Ended;
            string digits=State.Active?State.Display(Now,Config.CountUp,Config.Prefix):ClockState.Format(Config.Duration);
            string info="";if(State.Ended&&Config.StopAtZero)info="时间到";else if(State.Paused)info="已暂停";else if(Config.ClockTime)info=DateTime.Now.ToString("HH:mm:ss");
            if(unresolved)info=Now-sessionAt<2?"正在识别文件…":"未匹配 · 默认时长";
            if(Config.ShowPage&&Current!=null&&Current.Page>0&&!(Config.ZeroNoPage&&State.Duration==0))info+=(info.Length>0?"  ":"")+Current.Page+" / "+Current.Pages;
            bool visible=!background.Hidden&&!hidden&&!(Config.HideWhenIdle&&!documentsOpen&&!State.Active)&&!(Config.HideBeforeWarning&&!warn)&&!(Config.HideZero&&(State.Active?State.Duration:Config.Duration)==0);
            double age=Now-alertAt;double animation=age>=0&&age<6?(Math.Sin(age*Math.PI*2/1.4)+1)/2:0;
            foreach(var w in windows){w.Digits=digits;w.Information=info;w.WarningState=warn;w.Animation=animation;
                w.ResizeForText();if(visible){if(!w.Visible){w.Show();Position(w);}if(scan)Position(w);w.Render();}else if(w.Visible)w.Hide();}
            string trayText="WpsClock："+(background.Hidden?"会议背景 · 暂停并隐藏":State.Active?digits+(State.Paused?" 已暂停":""):Config.Auto?"等待放映":"手动模式");if(tray.Text!=trayText)tray.Text=trayText.Length>63?trayText.Substring(0,63):trayText;
        }
        void PublishStatus(){
            if(settings==null||settings.IsDisposed)return;
            string summary=background.Hidden?"会议背景："+background.BackgroundFile+"\r\n已暂停并隐藏；返回原发言文件继续计时。":preview?"当前为声音与动画预览。":!State.Active?"等待放映。已保存 "+Config.Rules.Count+" 条文件时长规则。":
                "当前文件："+(session.File.Length>0?session.File:"未识别")+"\r\n"+
                "时长："+ClockState.Format(State.Duration)+"  ·  "+session.Match.Explanation;
            string details=summary;
            if(State.Active&&!preview)details+="\r\n识别来源："+session.Source+"\r\n匹配规则："+(session.Match.Matched?session.Match.Rule:"无，使用默认时长")+"\r\n规则来源："+session.Match.Source;
            settings.SetDiagnostics(details+"\r\n\r\n"+diagnostics,summary);
        }
        protected override void ExitThreadCore(){if(disposed)return;disposed=true;timer.Stop();SystemEvents.DisplaySettingsChanged-=DisplayChanged;Save();
            if(settings!=null&&!settings.IsDisposed)settings.Close();CloseBlackouts();foreach(var w in windows)w.Dispose();windows.Clear();
            tray.Visible=false;tray.ContextMenuStrip.Dispose();tray.Dispose();messages.Dispose();probe.Dispose();audio.Dispose();timer.Dispose();base.ExitThreadCore();}
    }

    // SettingsDialog is defined below. All pages use standard Windows controls
    // and the classic tab layout of the supplied reference screenshots.
    public sealed class SettingsDialog : Form {
        readonly Controller owner;
        readonly Settings draft;
        readonly TabControl tabs=new TabControl();
        readonly DateTimePicker duration=TimeInput(),nearTime=TimeInput();
        readonly DataGridView rules=new DataGridView();
        readonly RadioButton countDown=new RadioButton(),countUp=new RadioButton();
        readonly CheckBox perPage=new CheckBox(),nearSound=new CheckBox(),endSound=new CheckBox();
        readonly TextBox nearFile=new TextBox(),endFile=new TextBox();
        readonly RadioButton keep=new RadioButton(),exit=new RadioButton(),black=new RadioButton();
        readonly NumericUpDown grace=new NumericUpDown();
        readonly CheckBox blink=new CheckBox(),stop=new CheckBox(),manual=new CheckBox();
        readonly ComboBox pauseKey=new ComboBox(),resetKey=new ComboBox(),prefix=new ComboBox();
        readonly TrackBar opacity=new TrackBar();
        readonly Label opacityLabel=new Label();
        readonly NumericUpDown panelWidth=new NumericUpDown(),panelHeight=new NumericUpDown();
        readonly CheckBox autoSize=new CheckBox();
        bool widthEdited,heightEdited,sizeModeEdited,syncingSize;
        readonly Dictionary<string,CheckBox> appearances=new Dictionary<string,CheckBox>();
        readonly TextBox diagnostics=new TextBox();
        readonly TextBox currentMatch=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
        readonly CheckBox compatible=new CheckBox();
        readonly Label previewLabel=new Label();
        public SettingsDialog(Controller controller){
            // Runtime construction must not trigger repeated automatic scaling as controls are added.
            AutoScaleMode=AutoScaleMode.None;SuspendLayout();
            owner=controller;draft=owner.Config.Clone();Text="设置 — WpsClock";Font=new Font("Microsoft YaHei UI",9);
            ClientSize=new Size(580,650);FormBorderStyle=FormBorderStyle.Sizable;MinimizeBox=false;TopMost=true;StartPosition=FormStartPosition.CenterScreen;
            tabs.Dock=DockStyle.Fill;
            var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(10),WrapContents=false};
            var save=new Button{Text="保存（Enter）",AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(14,6,14,6)};save.Click+=delegate{SaveSettings();};
            var cancel=new Button{Text="取消",AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(14,6,14,6),DialogResult=DialogResult.Cancel};cancel.Click+=delegate{Close();};
            footer.Controls.Add(cancel);footer.Controls.Add(save);AcceptButton=save;CancelButton=cancel;
            Controls.Add(tabs);Controls.Add(footer);
            var time=new TabPage("时长设置");var control=new TabPage("控制设置");var appearance=new TabPage("外观设置");var about=new TabPage("版本 / 检测");
            tabs.TabPages.AddRange(new TabPage[]{time,control,appearance,about});BuildTime(time);BuildControl(control);BuildAppearance(appearance);BuildAbout(about);
            float scale;using(Graphics metrics=CreateGraphics())scale=metrics.DpiX/96f;
            foreach(TabPage page in tabs.TabPages)PreparePage(page,scale);
            ClientSize=new Size((int)Math.Ceiling(580*scale),(int)Math.Ceiling(650*scale));
            MinimumSize=new Size((int)Math.Ceiling(360*scale),(int)Math.Ceiling(300*scale));
            ResumeLayout(true);
            Shown+=delegate{Rectangle area=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);};
            FormClosed+=delegate{owner.PreviewOpacity(-1);};
        }
        static void PreparePage(TabPage page,float scale){
            // Leave controls in their original parent. Each design coordinate is converted once.
            page.SuspendLayout();page.AutoScroll=true;
            int bottom=0,right=0;
            foreach(Control c in page.Controls){
                LayoutControl(c,scale);
                bottom=Math.Max(bottom,c.Bottom);right=Math.Max(right,c.Right);
            }
            page.AutoScrollMinSize=new Size(right+(int)(12*scale),bottom+(int)(12*scale));
            page.ResumeLayout(true);
        }
        static void LayoutControl(Control c,float scale){
            Rectangle design=c.Bounds;
            // Only explicit radio-button groups have application-owned children to scale.
            // Native editors/grid scrollbars manage their own children.
            if(c is Panel){c.SuspendLayout();foreach(Control child in c.Controls)LayoutControl(child,scale);}
            c.Bounds=new Rectangle((int)Math.Round(design.X*1.12f*scale),(int)Math.Round(design.Y*1.05f*scale),
                (int)Math.Ceiling(design.Width*1.12f*scale),(int)Math.Ceiling(design.Height*scale));
            var button=c as Button;var check=c as CheckBox;var radio=c as RadioButton;
            if(button!=null){button.AutoSize=true;button.AutoSizeMode=AutoSizeMode.GrowOnly;button.Padding=new Padding((int)(3*scale));}
            if(check!=null)check.AutoSize=true;if(radio!=null)radio.AutoSize=true;
            var label=c as Label;if(label!=null&&label.BorderStyle==BorderStyle.None)label.Height=Math.Max(label.Height,label.GetPreferredSize(new Size(label.Width,0)).Height);
            if(c is Panel)c.ResumeLayout(true);
        }
        static DateTimePicker TimeInput(){return new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm:ss",ShowUpDown=true,Width=112,Value=DateTime.Today};}
        static void SetTime(DateTimePicker picker,int seconds){picker.Value=DateTime.Today.AddSeconds(Math.Max(0,Math.Min(86399,seconds)));}
        static int Seconds(DateTimePicker picker){return (int)picker.Value.TimeOfDay.TotalSeconds;}
        static Label LabelAt(Control p,string text,int x,int y,int width){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(width,24),TextAlign=ContentAlignment.MiddleLeft};p.Controls.Add(l);return l;}
        static void At(Control p,Control c,int x,int y,int width,int height){c.Location=new Point(x,y);c.Size=new Size(width,height);p.Controls.Add(c);}
        static Button ButtonAt(Control p,string text,int x,int y,int w,int h,Action click){var b=new Button{Text=text};At(p,b,x,y,w,h);b.Click+=delegate{click();};return b;}
        static void CheckAt(Control p,CheckBox c,string text,int x,int y,int width,bool value){c.Text=text;c.Checked=value;At(p,c,x,y,width,25);}
        static void Separator(Control p,int y){At(p,new Label{BorderStyle=BorderStyle.Fixed3D},16,y,463,2);}
        void BuildTime(TabPage p){
            LabelAt(p,"默认计时时长：",24,25,116);At(p,duration,144,26,116,25);SetTime(duration,draft.Duration);
            currentMatch.Text="等待放映。";At(p,currentMatch,24,61,455,65);
            LabelAt(p,"独立文件计时清单（优先级高于默认设置）",24,129,440);
            rules.AllowUserToAddRows=false;rules.AllowUserToDeleteRows=true;rules.RowHeadersVisible=false;rules.SelectionMode=DataGridViewSelectionMode.FullRowSelect;rules.MultiSelect=false;
            rules.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;rules.BackgroundColor=Color.White;
            rules.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="PPT / PDF 文件",Name="File",FillWeight=50});
            rules.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="时长 HH:mm:ss",Name="Time",FillWeight=27});
            rules.Columns.Add(new DataGridViewComboBoxColumn{HeaderText="类型",Name="Background",FillWeight=23,
                ValueType=typeof(bool),DisplayMember="Value",ValueMember="Key",
                DataSource=new KeyValuePair<bool,string>[] {new KeyValuePair<bool,string>(false,"发言计时"),new KeyValuePair<bool,string>(true,"会议背景")}});
            rules.CurrentCellDirtyStateChanged+=delegate{if(rules.IsCurrentCellDirty&&rules.CurrentCell is DataGridViewComboBoxCell)rules.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            rules.CellValueChanged+=delegate(object sender,DataGridViewCellEventArgs e){if(e.RowIndex>=0&&e.ColumnIndex==2)StyleRule(rules.Rows[e.RowIndex]);};
            At(p,rules,24,156,455,208);foreach(var r in draft.Rules){int row=rules.Rows.Add(r.File,TimeSpan.FromSeconds(r.Seconds).ToString(@"hh\:mm\:ss"),r.Background);StyleRule(rules.Rows[row]);}
            ButtonAt(p,"添加文件…",24,375,100,28,delegate{using(var dialog=new OpenFileDialog{Filter="演示 / PDF|*.ppt;*.pptx;*.pps;*.ppsx;*.pdf|所有文件|*.*",Multiselect=true})if(dialog.ShowDialog(this)==DialogResult.OK)foreach(string file in dialog.FileNames)rules.Rows.Add(file,TimeSpan.FromSeconds(Seconds(duration)).ToString(@"hh\:mm\:ss"),false);});
            ButtonAt(p,"删除选中",135,375,94,28,delegate{if(rules.SelectedRows.Count>0)rules.Rows.Remove(rules.SelectedRows[0]);});
            ButtonAt(p,"当前文件",241,375,94,28,delegate{SelectCurrentFile();});
            LabelAt(p,"类型选“会议背景”：暂停并隐藏，不触发提醒。",24,418,455);
            LabelAt(p,"返回原文件继续；切换另一文件重新计时。Enter 保存。",24,448,455);
        }
        static void StyleRule(DataGridViewRow row){
            bool isBackground=Convert.ToBoolean(row.Cells[2].Value??false);
            row.Cells[1].ReadOnly=isBackground;
            row.Cells[1].Style.BackColor=isBackground?SystemColors.Control:SystemColors.Window;
            row.Cells[1].ToolTipText=isBackground?"会议背景不使用计时时长。":"HH:mm:ss；00:00:00 表示不限时。";
        }
        public void SelectCurrentFile(){tabs.SelectedIndex=0;string file=Controller.FileName(owner.Current);if(file.Length==0){MessageBox.Show(this,"当前没有可识别的放映文件。请使用“添加文件”。");return;}
            foreach(DataGridViewRow row in rules.Rows)if(String.Equals(Convert.ToString(row.Cells[0].Value),file,StringComparison.OrdinalIgnoreCase)){row.Selected=true;return;}
            int index=rules.Rows.Add(file,TimeSpan.FromSeconds(owner.DurationFor(owner.Current)).ToString(@"hh\:mm\:ss"),false);rules.Rows[index].Selected=true;
        }
        void MusicRow(Control p,CheckBox enabled,TextBox path,bool final,int y){
            CheckAt(p,enabled,"播放音乐：",16,y,91,final?draft.EndSound:draft.NearSound);
            path.Text=final?draft.EndFile:draft.NearFile;At(p,path,107,y+1,247,23);
            ButtonAt(p,"浏览",362,y-1,57,27,delegate{using(var d=new OpenFileDialog{Filter="提示音|*.wav;*.mp3|所有文件|*.*"})if(d.ShowDialog(this)==DialogResult.OK){string baseDir=AppDomain.CurrentDomain.BaseDirectory;path.Text=d.FileName.StartsWith(baseDir,StringComparison.OrdinalIgnoreCase)?d.FileName.Substring(baseDir.Length):d.FileName;}});
            ButtonAt(p,"试听",427,y-1,57,27,delegate{owner.TestSound(final,path.Text);});
        }
        void BuildControl(TabPage p){
            LabelAt(p,"默认方式：",16,18,88);
            var modes=new Panel();At(p,modes,103,17,202,27);countDown.Text="倒计时";countUp.Text="正计时";At(modes,countDown,0,0,94,26);At(modes,countUp,96,0,94,26);countUp.Checked=draft.CountUp;countDown.Checked=!draft.CountUp;
            CheckAt(p,perPage,"单页计时",320,18,150,draft.PerPage);Separator(p,59);
            LabelAt(p,"在计时结束前",16,77,108);At(p,nearTime,124,78,113,25);SetTime(nearTime,draft.Warning);
            MusicRow(p,nearSound,nearFile,false,115);Separator(p,155);
            LabelAt(p,"计时时间到：",16,168,130);
            var actions=new Panel();At(p,actions,16,196,450,28);keep.Text="保持全屏";exit.Text="退出全屏";black.Text="黑屏";
            At(actions,keep,0,0,115,27);At(actions,exit,125,0,115,27);At(actions,black,251,0,96,27);
            keep.Checked=draft.EndAction==0;exit.Checked=draft.EndAction==1;black.Checked=draft.EndAction==2;
            MusicRow(p,endSound,endFile,true,235);Separator(p,277);
            LabelAt(p,"在",17,291,22);grace.Minimum=0;grace.Maximum=120;grace.Value=draft.Grace;At(p,grace,42,292,57,24);
            LabelAt(p,"秒内，意外退出不中断计时",106,291,333);
            CheckAt(p,blink,"提示时闪烁时间",16,328,200,draft.Blink);CheckAt(p,stop,"计时时间到，停止继续计时",242,328,250,draft.StopAtZero);
            CheckAt(p,manual,"允许手动暂停、重置计时",16,359,278,draft.Manual);
            LabelAt(p,"暂停快捷键：",16,397,104);KeyList(pauseKey,draft.PauseKey);At(p,pauseKey,119,397,100,25);
            LabelAt(p,"重置快捷键：",262,397,104);KeyList(resetKey,draft.ResetKey);At(p,resetKey,367,397,100,25);
            LabelAt(p,"音乐留空使用内置声音。单页计时需要 WPS 页码接口。",16,445,473);
        }
        static void KeyList(ComboBox c,int selected){c.DropDownStyle=ComboBoxStyle.DropDownList;for(int i=1;i<=12;i++)c.Items.Add("F"+i);c.SelectedIndex=Math.Max(0,Math.Min(11,selected-(int)Keys.F1));}
        void ColorButton(Control p,int x,int y,int color,Action<int> setter){var button=new Button{BackColor=Color.FromArgb(color),UseVisualStyleBackColor=false};At(p,button,x,y,71,27);button.Click+=delegate{using(var dialog=new ColorDialog{Color=button.BackColor,FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK){button.BackColor=dialog.Color;setter(dialog.Color.ToArgb());UpdatePreview();}};}
        void BuildAppearance(TabPage p){
            LabelAt(p,"正常",133,20,70);LabelAt(p,"提示",232,20,70);LabelAt(p,"前景：",24,60,80);LabelAt(p,"背景：",24,103,80);
            ColorButton(p,119,61,draft.Foreground,delegate(int v){draft.Foreground=v;});ColorButton(p,218,61,draft.WarningForeground,delegate(int v){draft.WarningForeground=v;});
            ColorButton(p,119,104,draft.Background,delegate(int v){draft.Background=v;});ColorButton(p,218,104,draft.WarningBackground,delegate(int v){draft.WarningBackground=v;});
            ButtonAt(p,"调整计时\r\n字体",316,35,78,55,delegate{using(var dialog=new FontDialog{Font=new Font(draft.FontName,draft.FontSize,draft.FontBold?FontStyle.Bold:FontStyle.Regular),MinSize=12,MaxSize=120})if(dialog.ShowDialog(this)==DialogResult.OK){draft.FontName=dialog.Font.Name;draft.FontSize=dialog.Font.Size;draft.FontBold=dialog.Font.Bold;UpdatePreview();}});
            ButtonAt(p,"放大字体",402,35,78,55,delegate{draft.FontSize=Math.Min(120,draft.FontSize+4);UpdatePreview();});
            ButtonAt(p,"调整信息\r\n字体",316,96,78,55,delegate{using(var dialog=new FontDialog{Font=new Font(draft.InfoFontName,draft.InfoFontSize),MinSize=8,MaxSize=32})if(dialog.ShowDialog(this)==DialogResult.OK){draft.InfoFontName=dialog.Font.Name;draft.InfoFontSize=dialog.Font.Size;}});
            ButtonAt(p,"缩小字体",402,96,78,55,delegate{draft.FontSize=Math.Max(12,draft.FontSize-4);UpdatePreview();});
            LabelAt(p,"提示前缀：",24,174,92);prefix.DropDownStyle=ComboBoxStyle.DropDown;prefix.Items.AddRange(new object[]{"-","超时 ","+"});prefix.Text=draft.Prefix;At(p,prefix,117,175,112,25);
            LabelAt(p,"背景不透明度：",242,174,108);opacity.Minimum=0;opacity.Maximum=100;opacity.TickStyle=TickStyle.None;opacity.Value=draft.BackgroundOpacity;At(p,opacity,343,174,139,32);
            opacityLabel.Text=draft.BackgroundOpacity+"%";At(p,opacityLabel,423,206,60,20);opacity.ValueChanged+=delegate{opacityLabel.Text=opacity.Value+"%";owner.PreviewOpacity(opacity.Value);if(appearances.ContainsKey("Transparent"))appearances["Transparent"].Checked=opacity.Value==0;};
            AddAppearance(p,"ClockTime","显示机器时间",24,235,draft.ClockTime);AddAppearance(p,"Transparent","背景透明",258,235,draft.BackgroundOpacity==0);
            appearances["Transparent"].CheckedChanged+=delegate{if(appearances["Transparent"].Checked)opacity.Value=0;else if(opacity.Value==0)opacity.Value=100;};
            AddAppearance(p,"HideBeforeWarning","提示前不显示窗口",24,264,draft.HideBeforeWarning);AddAppearance(p,"ShowHelp","显示“高手入门”",258,264,draft.ShowHelp);
            AddAppearance(p,"HideWhenIdle","没有PPT/PDF文件时隐藏窗口",24,293,draft.HideWhenIdle);AddAppearance(p,"ShowPage","显示页码提示（PPT）",258,293,draft.ShowPage);
            AddAppearance(p,"HideZero","计时时长为零时隐藏窗口",24,322,draft.HideZero);AddAppearance(p,"ZeroNoPage","计时时长为零时不显示页码",258,322,draft.ZeroNoPage);
            AddAppearance(p,"ControlBar","显示控制栏",24,351,draft.ControlBar);AddAppearance(p,"Locked","锁定位置 / 点击穿透",258,351,draft.Locked);
            AddAppearance(p,"SyncScreens","在扩展屏上同步显示计时器",24,381,draft.SyncScreens);
            previewLabel.Text="08:00";previewLabel.TextAlign=ContentAlignment.MiddleCenter;At(p,previewLabel,24,421,204,56);UpdatePreview();
            ButtonAt(p,"预览声音 / 动画",258,433,154,28,delegate{owner.Preview();});
            CheckAt(p,autoSize,"自动适应文字尺寸",24,496,245,draft.PanelWidth==0&&draft.PanelHeight==0);
            LabelAt(p,"宽度：",24,532,60);LabelAt(p,"高度：",244,532,60);
            panelWidth.Minimum=120;panelWidth.Maximum=8192;panelWidth.Value=Math.Max(120,Math.Min(8192,draft.PanelWidth>0?draft.PanelWidth:owner.CurrentPanelSize.Width));At(p,panelWidth,89,533,115,25);
            panelHeight.Minimum=60;panelHeight.Maximum=8192;panelHeight.Value=Math.Max(60,Math.Min(8192,draft.PanelHeight>0?draft.PanelHeight:owner.CurrentPanelSize.Height));At(p,panelHeight,309,533,115,25);
            panelWidth.Enabled=panelHeight.Enabled=!autoSize.Checked;
            autoSize.CheckedChanged+=delegate{if(!syncingSize)sizeModeEdited=true;panelWidth.Enabled=panelHeight.Enabled=!autoSize.Checked;};
            panelWidth.ValueChanged+=delegate{if(!syncingSize)widthEdited=true;};panelHeight.ValueChanged+=delegate{if(!syncingSize)heightEdited=true;};
            LabelAt(p,"解锁后可拖动面板边缘或四角缩放；右键可恢复自动尺寸。",24,572,455);
        }
        public void SyncPanelSize(){
            syncingSize=true;
            try{
                if(!sizeModeEdited)autoSize.Checked=owner.Config.PanelWidth==0&&owner.Config.PanelHeight==0;
                if(!widthEdited)panelWidth.Value=Math.Max(120,Math.Min(8192,owner.Config.PanelWidth>0?owner.Config.PanelWidth:owner.CurrentPanelSize.Width));
                if(!heightEdited)panelHeight.Value=Math.Max(60,Math.Min(8192,owner.Config.PanelHeight>0?owner.Config.PanelHeight:owner.CurrentPanelSize.Height));
            }finally{syncingSize=false;}
        }
        void AddAppearance(Control p,string key,string label,int x,int y,bool value){var c=new CheckBox();CheckAt(p,c,label,x,y,245,value);appearances[key]=c;}
        void UpdatePreview(){previewLabel.BackColor=Color.FromArgb(draft.Background);previewLabel.ForeColor=Color.FromArgb(draft.Foreground);Font old=previewLabel.Font;previewLabel.Font=new Font(draft.FontName,Math.Min(30,draft.FontSize),draft.FontBold?FontStyle.Bold:FontStyle.Regular);if(old!=Font)old.Dispose();}
        void BuildAbout(TabPage p){
            LabelAt(p,"WpsClock 2.4.2 · 中文放映标题识别修正",20,17,453).Font=new Font(Font,FontStyle.Bold);
            var explanation=new Label{Text="参考 FlyClock 的本地计时界面与交互独立实现。\r\n双击 EXE 后自动待命；关闭设置后仍在托盘运行。\r\n不联网、没有注册或在线升级。",Location=new Point(20,51),Size=new Size(454,65)};p.Controls.Add(explanation);
            CheckAt(p,compatible,"兼容识别无边框全屏窗口",20,125,454,draft.Compatible);
            LabelAt(p,"检测状态（仅显示在本机）：",20,161,454);
            diagnostics.Multiline=true;diagnostics.ReadOnly=true;diagnostics.ScrollBars=ScrollBars.Vertical;At(p,diagnostics,20,192,454,203);
            ButtonAt(p,"复制检测信息",20,407,117,27,delegate{try{Clipboard.SetText(diagnostics.Text);}catch{}});
            ButtonAt(p,"打开设置目录",148,407,117,27,delegate{Process.Start("explorer.exe","\""+Path.GetDirectoryName(owner.Store.PathName)+"\"");});
            LabelAt(p,"兼容识别需实机验证；普通编辑全屏可能需要取消此选项。",20,448,454);
        }
        public void SetDiagnostics(string value,string summary){if(diagnostics.Text!=value)diagnostics.Text=value;if(currentMatch.Text!=summary)currentMatch.Text=summary;}
        public void SelectAbout(){tabs.SelectedIndex=3;}
        void SaveSettings(){
            rules.EndEdit();draft.Duration=Seconds(duration);draft.Warning=Seconds(nearTime);
            if(draft.Duration>0&&draft.Warning>=draft.Duration&&nearSound.Checked){MessageBox.Show(this,"提前提醒时间应小于默认时长；或关闭提前播放音乐。");tabs.SelectedIndex=1;return;}
            draft.Rules.Clear();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(DataGridViewRow row in rules.Rows){string file=Convert.ToString(row.Cells[0].Value).Trim(),value=Convert.ToString(row.Cells[1].Value);TimeSpan time;
                bool isBackground=Convert.ToBoolean(row.Cells[2].Value??false);
                bool validTime=TimeSpan.TryParseExact(value,@"hh\:mm\:ss",System.Globalization.CultureInfo.InvariantCulture,out time)&&time.TotalSeconds<=86399;
                if(file.Length==0||(!isBackground&&!validTime)||!paths.Add(FileTiming.Normalize(file))){
                    MessageBox.Show(this,"文件清单中存在空文件名、重复文件或无效时长。请使用 HH:mm:ss 格式。");tabs.SelectedIndex=0;return;}
                draft.Rules.Add(new FileRule{File=file,Seconds=validTime?(int)time.TotalSeconds:draft.Duration,Background=isBackground});
            }
            if(pauseKey.SelectedIndex==resetKey.SelectedIndex&&manual.Checked){MessageBox.Show(this,"暂停和重置快捷键不能相同。");return;}
            draft.CountUp=countUp.Checked;draft.PerPage=perPage.Checked;draft.NearSound=nearSound.Checked;draft.EndSound=endSound.Checked;draft.NearFile=nearFile.Text.Trim();draft.EndFile=endFile.Text.Trim();
            draft.EndAction=exit.Checked?1:black.Checked?2:0;draft.Grace=(int)grace.Value;draft.Blink=blink.Checked;draft.StopAtZero=stop.Checked;draft.Manual=manual.Checked;
            draft.PauseKey=(int)Keys.F1+pauseKey.SelectedIndex;draft.ResetKey=(int)Keys.F1+resetKey.SelectedIndex;draft.Prefix=prefix.Text;draft.BackgroundOpacity=opacity.Value;
            foreach(var pair in appearances)typeof(Settings).GetField(pair.Key).SetValue(draft,pair.Value.Checked);
            draft.Compatible=compatible.Checked;
            // Dragging a clock while this dialog is open must not discard its new position.
            draft.Positions=owner.Config.Clone().Positions;draft.Auto=owner.Config.Auto;
            Size merged=PanelGeometry.MergeSize(new Size(owner.Config.PanelWidth,owner.Config.PanelHeight),new Size((int)panelWidth.Value,(int)panelHeight.Value),sizeModeEdited,autoSize.Checked,widthEdited,heightEdited);
            draft.PanelWidth=merged.Width;draft.PanelHeight=merged.Height;
            owner.Apply(draft);Close();
        }
    }

    public static class Program {
        [STAThread] public static void Main(){
            bool created;
            using(var mutex=new Mutex(true,"Local\\WpsClockPortableV2",out created)){
                if(!created){Native.PostMessage(new IntPtr(0xffff),MessageWindow.WakeMessage,IntPtr.Zero,IntPtr.Zero);return;}
                try{Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Controller());}
                catch(Exception e){MessageBox.Show("启动失败：\r\n"+e.Message,"WpsClock",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            }
        }
    }
}
