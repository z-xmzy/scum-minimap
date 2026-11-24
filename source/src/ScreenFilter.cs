using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace ScumMiniMap {
    // Screen filter for dark bunkers and nights. It never touches SCUM: the
    // brightness / contrast / gamma curve goes into the display's gamma ramp
    // (the same table Windows colour calibration uses), and saturation uses the
    // Windows Magnification colour matrix. Both act on the whole monitor, so SCUM
    // must run borderless or windowed. The original ramp is written to disk
    // before any change and restored on exit, by a watchdog process if this one
    // dies, and on the next start as a last resort.
    // Written from the Win32 documentation; no third-party code is included.
    internal enum ScreenFilterPreset { Night, Bunker, Fog, Custom }

    internal sealed class ScreenFilterValues {
        internal int Brightness,Contrast=100,Gamma=100,Saturation=100;
        internal ScreenFilterValues Clone() { return (ScreenFilterValues)MemberwiseClone(); }
        internal void Clamp() {
            Brightness=Math.Max(-50,Math.Min(50,Brightness));Contrast=Math.Max(50,Math.Min(200,Contrast));
            Gamma=Math.Max(50,Math.Min(300,Gamma));Saturation=Math.Max(0,Math.Min(200,Saturation));
        }
        internal bool Neutral { get { return Brightness==0&&Contrast==100&&Gamma==100&&Saturation==100; } }
        internal string Serialize() { return Brightness+","+Contrast+","+Gamma+","+Saturation; }
        internal static bool TryParse(string text,out ScreenFilterValues values) {
            values=null;var parts=(text??"").Split(',');if(parts.Length!=4)return false;var n=new int[4];
            for(int i=0;i<4;i++)if(!int.TryParse(parts[i].Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out n[i]))return false;
            values=new ScreenFilterValues { Brightness=n[0],Contrast=n[1],Gamma=n[2],Saturation=n[3] };values.Clamp();return true;
        }
        internal bool SameAs(ScreenFilterValues other) { return other!=null&&Serialize()==other.Serialize(); }
    }

    internal sealed class ScreenFilterSettings {
        internal const string FileName="screen-filter.ini";
        internal static readonly ScreenFilterPreset[] Presets={ScreenFilterPreset.Night,ScreenFilterPreset.Bunker,ScreenFilterPreset.Fog,ScreenFilterPreset.Custom};
        internal bool Enabled,OnlyWhileGame=true;
        internal ScreenFilterPreset Preset=ScreenFilterPreset.Night;
        internal readonly ScreenFilterValues[] Values=Presets.Select(Default).ToArray();

        internal static ScreenFilterValues Default(ScreenFilterPreset preset) {
            switch(preset) {
                // Night: lift the dark half strongly, keep a little extra contrast so it does not wash out.
                case ScreenFilterPreset.Night:return new ScreenFilterValues { Brightness=4,Contrast=108,Gamma=170,Saturation=110 };
                // Bunker: corridors are near-black; the strongest shadow lift.
                case ScreenFilterPreset.Bunker:return new ScreenFilterValues { Brightness=8,Contrast=100,Gamma=210,Saturation=100 };
                // Fog / overcast: more contrast and colour, little gamma.
                case ScreenFilterPreset.Fog:return new ScreenFilterValues { Brightness=-4,Contrast=125,Gamma=110,Saturation=125 };
                default:return new ScreenFilterValues { Brightness=0,Contrast=110,Gamma=140,Saturation=100 };
            }
        }
        internal ScreenFilterValues Current { get { return Values[Array.IndexOf(Presets,Preset)]; } }
        internal ScreenFilterValues For(ScreenFilterPreset preset) { return Values[Array.IndexOf(Presets,preset)]; }
        internal ScreenFilterPreset NextPreset() { return Presets[(Array.IndexOf(Presets,Preset)+1)%Presets.Length]; }

        internal ScreenFilterSettings Clone() { var copy=new ScreenFilterSettings();copy.CopyFrom(this);return copy; }
        internal void CopyFrom(ScreenFilterSettings other) {
            Enabled=other.Enabled;OnlyWhileGame=other.OnlyWhileGame;Preset=other.Preset;
            for(int i=0;i<Values.Length;i++)Values[i]=other.Values[i].Clone();
        }
        internal void Clamp() {
            if(!Enum.IsDefined(typeof(ScreenFilterPreset),Preset))Preset=ScreenFilterPreset.Night;
            foreach(var v in Values)v.Clamp();
        }
        internal string Serialize() {
            var lines=new List<string>{"Enabled="+Enabled,"OnlyWhileGame="+OnlyWhileGame,"Preset="+Preset};
            for(int i=0;i<Presets.Length;i++)lines.Add(Presets[i]+"="+Values[i].Serialize());
            return string.Join("\r\n",lines)+"\r\n";
        }
        internal static ScreenFilterSettings Parse(string text) {
            var settings=new ScreenFilterSettings();if(text==null)return settings;
            foreach(string raw in text.Split('\n')) {
                string line=raw.Trim();int split=line.IndexOf('=');if(split<=0)continue;
                string key=line.Substring(0,split).Trim(),value=line.Substring(split+1).Trim();bool flag;ScreenFilterPreset preset;ScreenFilterValues values;
                if(key=="Enabled") { if(bool.TryParse(value,out flag))settings.Enabled=flag; }
                else if(key=="OnlyWhileGame") { if(bool.TryParse(value,out flag))settings.OnlyWhileGame=flag; }
                else if(key=="Preset") { if(Enum.TryParse(value,true,out preset)&&Enum.IsDefined(typeof(ScreenFilterPreset),preset))settings.Preset=preset; }
                else if(Enum.TryParse(key,false,out preset)&&Enum.IsDefined(typeof(ScreenFilterPreset),preset)&&ScreenFilterValues.TryParse(value,out values))
                    settings.Values[Array.IndexOf(Presets,preset)]=values;
            }
            settings.Clamp();return settings;
        }
        internal static ScreenFilterSettings Load(string folder) {
            try { string path=Path.Combine(folder,FileName);if(File.Exists(path))return Parse(File.ReadAllText(path,Encoding.UTF8)); }
            catch(Exception error) { Program.LogException("ScreenFilterLoad",error); }
            return new ScreenFilterSettings();
        }
        internal bool Save(string folder) {
            try {
                Directory.CreateDirectory(folder);string path=Path.Combine(folder,FileName),temp=path+".tmp";
                File.WriteAllText(temp,Serialize(),new UTF8Encoding(false));
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
                return true;
            } catch(Exception error) { Program.LogException("ScreenFilterSave",error);return false; }
        }
    }

    internal static class ScreenFilterMath {
        internal const int RampSize=256;
        // Output level 0..1 for an input level 0..1: gamma first (lifts shadows),
        // then contrast around mid-grey, then a brightness offset.
        internal static double Level(ScreenFilterValues v,double x) {
            double y=Math.Pow(Math.Max(0,Math.Min(1,x)),100.0/v.Gamma);
            y=(y-0.5)*(v.Contrast/100.0)+0.5;
            y+=v.Brightness/100.0;
            return Math.Max(0,Math.Min(1,y));
        }
        internal static ushort[] Identity() {
            var ramp=new ushort[RampSize*3];
            for(int c=0;c<3;c++)for(int i=0;i<RampSize;i++)ramp[c*RampSize+i]=(ushort)(i*257);
            return ramp;
        }
        // Our curve feeds the display's existing ramp, so colour calibration and
        // Night Light keep working underneath. strength blends from original (0) to full (1).
        internal static ushort[] Compose(ScreenFilterValues v,ushort[] original,double strength) {
            var ramp=new ushort[RampSize*3];
            for(int c=0;c<3;c++) {
                int baseIndex=c*RampSize;
                for(int i=0;i<RampSize;i++) {
                    double position=Level(v,i/255.0)*255.0;int low=(int)Math.Floor(position),high=Math.Min(255,low+1);double t=position-low;
                    double mapped=original[baseIndex+low]*(1-t)+original[baseIndex+high]*t;
                    double blended=original[baseIndex+i]+(mapped-original[baseIndex+i])*strength;
                    ramp[baseIndex+i]=(ushort)Math.Max(0,Math.Min(65535,Math.Round(blended)));
                }
            }
            return ramp;
        }
        internal static int MaxDifference(ushort[] a,ushort[] b) {
            int worst=0;for(int i=0;i<a.Length;i++)worst=Math.Max(worst,Math.Abs(a[i]-b[i]));return worst;
        }
        // Row-vector colour matrix (out = in × M) mixing each channel toward Rec.709 luminance.
        internal static float[] SaturationMatrix(int saturation) {
            float s=saturation/100f;float[] w={0.2126f,0.7152f,0.0722f};var m=new float[25];
            for(int i=0;i<3;i++)for(int j=0;j<3;j++)m[i*5+j]=(1-s)*w[i]+(i==j?s:0);
            m[18]=1;m[24]=1;return m;
        }
    }

    // Original ramps saved before the first change, one line per display.
    internal static class ScreenFilterRestore {
        internal const string FileName="screen-filter-restore.dat";
        internal static void Write(string path,IDictionary<string,ushort[]> originals) {
            var lines=originals.Select(pair=> {
                var bytes=new byte[pair.Value.Length*2];Buffer.BlockCopy(pair.Value,0,bytes,0,bytes.Length);
                return pair.Key+"\t"+Convert.ToBase64String(bytes);
            });
            string temp=path+".tmp";File.WriteAllLines(temp,lines,new UTF8Encoding(false));
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        internal static Dictionary<string,ushort[]> Read(string path) {
            var result=new Dictionary<string,ushort[]>(StringComparer.OrdinalIgnoreCase);
            foreach(string line in File.ReadAllLines(path,Encoding.UTF8)) {
                int tab=line.IndexOf('\t');if(tab<=0)continue;
                try {
                    var bytes=Convert.FromBase64String(line.Substring(tab+1).Trim());if(bytes.Length!=ScreenFilterMath.RampSize*6)continue;
                    var ramp=new ushort[ScreenFilterMath.RampSize*3];Buffer.BlockCopy(bytes,0,ramp,0,bytes.Length);result[line.Substring(0,tab)]=ramp;
                } catch(FormatException) { }
            }
            return result;
        }
        // Puts every saved display back and removes the file. Safe to call from any process.
        internal static bool RestoreAndDelete(string path) {
            if(!File.Exists(path))return false;
            bool all=true;
            try { foreach(var pair in Read(path))all&=GammaDevice.Set(pair.Key,pair.Value); }
            catch(Exception error) { Program.LogException("ScreenFilterRestoreRead",error);all=false; }
            if(!all) {
                // A saved ramp the system rejects is useless; fall back to a straight ramp.
                try { foreach(var pair in Read(path))GammaDevice.Set(pair.Key,ScreenFilterMath.Identity()); }catch(Exception) { }
            }
            try { File.Delete(path); }catch(Exception error) { Program.LogException("ScreenFilterRestoreDelete",error); }
            return true;
        }
    }

    internal static class GammaDevice {
        [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateDC(string driver,string device,string output,IntPtr data);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool SetDeviceGammaRamp(IntPtr dc,ushort[] ramp);
        [DllImport("gdi32.dll")] static extern bool GetDeviceGammaRamp(IntPtr dc,[Out] ushort[] ramp);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfoEx info);
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct MonitorInfoEx { public int Size;public Native.WindowRect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device; }

        // Display device (\\.\DISPLAYn) showing a window; the primary display for IntPtr.Zero.
        internal static string DeviceFor(IntPtr window) {
            var info=new MonitorInfoEx { Size=Marshal.SizeOf(typeof(MonitorInfoEx)) };
            IntPtr monitor=MonitorFromWindow(window,window==IntPtr.Zero?1u:2u);
            return monitor!=IntPtr.Zero&&GetMonitorInfo(monitor,ref info)&&!string.IsNullOrEmpty(info.Device)?info.Device:null;
        }
        internal static ushort[] Get(string device) {
            IntPtr dc=CreateDC(null,device,null,IntPtr.Zero);if(dc==IntPtr.Zero)return null;
            try { var ramp=new ushort[ScreenFilterMath.RampSize*3];return GetDeviceGammaRamp(dc,ramp)?ramp:null; }
            finally { DeleteDC(dc); }
        }
        internal static bool Set(string device,ushort[] ramp) {
            IntPtr dc=CreateDC(null,device,null,IntPtr.Zero);if(dc==IntPtr.Zero)return false;
            try { return SetDeviceGammaRamp(dc,ramp); }
            finally { DeleteDC(dc); }
        }

        // Windows rejects ramps that stray far from a straight line unless this is raised to 256.
        const string IcmKey=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM";
        internal static bool FullRangeAllowed() {
            try { using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(IcmKey)) { object value=key==null?null:key.GetValue("GdiIcmGammaRange");return value is int&&(int)value>=256; } }
            catch(Exception) { return false; }
        }
        internal static bool AllowFullRange() {
            try { using(var key=Microsoft.Win32.Registry.LocalMachine.CreateSubKey(IcmKey)) { key.SetValue("GdiIcmGammaRange",256,Microsoft.Win32.RegistryValueKind.DWord);return true; } }
            catch(Exception error) { Program.LogException("ScreenFilterGammaRange",error);return false; }
        }
    }

    internal static class Magnification {
        [StructLayout(LayoutKind.Sequential)] struct ColourEffect { [MarshalAs(UnmanagedType.ByValArray,SizeConst=25)] public float[] Transform; }
        [DllImport("Magnification.dll")] static extern bool MagInitialize();
        [DllImport("Magnification.dll")] static extern bool MagUninitialize();
        [DllImport("Magnification.dll")] static extern bool MagSetFullscreenColorEffect(ref ColourEffect effect);
        static bool initialised,failed;
        internal static bool SetMatrix(float[] matrix) {
            if(failed)return false;
            try {
                if(!initialised) { if(!MagInitialize()) { failed=true;return false; }initialised=true; }
                var effect=new ColourEffect { Transform=matrix };return MagSetFullscreenColorEffect(ref effect);
            } catch(Exception error) { failed=true;Program.LogException("ScreenFilterMagnification",error);return false; }
        }
        internal static void Reset() {
            if(!initialised)return;
            try { SetMatrix(ScreenFilterMath.SaturationMatrix(100));MagUninitialize(); }catch(Exception) { }
            initialised=false;
        }
    }

    internal sealed class ScreenFilterController:IDisposable {
        internal enum Status { Off, Waiting, Active, Limited, Failed }
        readonly string folder,restorePath;
        readonly DispatcherTimer timer;
        readonly Dictionary<string,ushort[]> originals=new Dictionary<string,ushort[]>(StringComparer.OrdinalIgnoreCase);
        string appliedDevice,lastGameDevice;ushort[] appliedRamp;string appliedKey;
        int appliedSaturation=100;long nextVerify;
        Process watchdog;bool disposed;
        static ScreenFilterController current;
        internal ScreenFilterSettings Settings { get; private set; }
        internal Status State { get; private set; }
        internal double Strength { get; private set; }
        internal event Action Changed;

        internal ScreenFilterController(string folder,Dispatcher dispatcher) {
            this.folder=folder;restorePath=Path.Combine(folder,ScreenFilterRestore.FileName);Settings=ScreenFilterSettings.Load(folder);
            // A previous run that died without its watchdog left the screen filtered.
            try { if(ScreenFilterRestore.RestoreAndDelete(restorePath))Program.LogException("ScreenFilterRecovered",new InvalidOperationException("Restored the display ramp left by an earlier run.")); }
            catch(Exception error) { Program.LogException("ScreenFilterRecover",error); }
            current=this;
            AppDomain.CurrentDomain.ProcessExit+=(s,e)=>EmergencyRestore();
            AppDomain.CurrentDomain.UnhandledException+=(s,e)=>EmergencyRestore();
            timer=new DispatcherTimer(DispatcherPriority.Normal,dispatcher) { Interval=TimeSpan.FromMilliseconds(250) };
            timer.Tick+=(s,e)=> { try { Sync(); }catch(Exception error) { timer.Stop();RestoreAll();Program.LogException("ScreenFilterSync",error); } };
        }
        internal void Start() { if(!disposed)timer.Start(); }

        // Active while SCUM (or this app's own map / settings on top of it) is in front.
        internal static bool ShouldApply(bool enabled,bool onlyWhileGame,bool gameFocused,bool ourWindowFocused) {
            return enabled&&(!onlyWhileGame||gameFocused||ourWindowFocused);
        }

        internal void Apply(ScreenFilterSettings settings) {
            settings.Clamp();Settings=settings;appliedKey=null;
            if(!disposed&&timer.IsEnabled)Sync();
        }
        internal bool Save() { return Settings.Save(folder); }
        internal void SetEnabled(bool enabled) { var next=Settings.Clone();next.Enabled=enabled;Apply(next);Save();RaiseChanged(); }
        internal void CyclePreset() { var next=Settings.Clone();next.Preset=next.NextPreset();next.Enabled=true;Apply(next);Save();RaiseChanged(); }
        void RaiseChanged() { var handler=Changed;if(handler!=null)handler(); }

        void Sync() {
            if(disposed)return;
            IntPtr foreground=Native.GetForegroundWindow();bool game=Native.GameFocused(),ours=!game&&Native.IsOurWindow(foreground);
            if(game) { string device=GammaDevice.DeviceFor(foreground);if(device!=null)lastGameDevice=device; }
            var previous=State;
            if(!ShouldApply(Settings.Enabled,Settings.OnlyWhileGame,game,ours)) {
                RestoreAll();State=Settings.Enabled?Status.Waiting:Status.Off;
                if(State!=previous)RaiseChanged();return;
            }
            string target=lastGameDevice??GammaDevice.DeviceFor(IntPtr.Zero);
            if(target==null) { State=Status.Failed;if(State!=previous)RaiseChanged();return; }
            if(appliedDevice!=null&&!string.Equals(appliedDevice,target,StringComparison.OrdinalIgnoreCase))RestoreAll();
            var values=Settings.Current;
            ApplyRamp(target,values);
            ApplySaturation(values.Saturation);
            if(State!=previous)RaiseChanged();
        }

        void ApplyRamp(string device,ScreenFilterValues values) {
            ushort[] original;
            if(!originals.TryGetValue(device,out original)) {
                original=GammaDevice.Get(device);if(original==null) { State=Status.Failed;return; }
                originals[device]=original;PersistOriginals();EnsureWatchdog();appliedKey=null;
            }
            long now=Environment.TickCount;
            if(appliedRamp!=null&&appliedDevice==device&&now>=nextVerify) {
                nextVerify=now+2000;
                // Something else (the game, Night Light, a driver panel) replaced the
                // ramp. Treat theirs as the new base and lay ours on top again.
                var live=GammaDevice.Get(device);
                if(live!=null&&ScreenFilterMath.MaxDifference(live,appliedRamp)>1024) { originals[device]=live;original=live;PersistOriginals();appliedKey=null; }
            }
            string key=device+"|"+values.Brightness+","+values.Contrast+","+values.Gamma;
            if(key==appliedKey)return;
            var full=ScreenFilterMath.Compose(values,original,1);
            if(GammaDevice.Set(device,full)) { Strength=1;appliedRamp=full; }
            else {
                // Windows limits how far a ramp may bend; find the strongest one it accepts.
                double low=0,high=1;ushort[] best=null;
                for(int i=0;i<8;i++) { double mid=(low+high)/2;var ramp=ScreenFilterMath.Compose(values,original,mid);if(GammaDevice.Set(device,ramp)) { low=mid;best=ramp; }else high=mid; }
                Strength=best==null?0:low;appliedRamp=best;
                if(best==null) { GammaDevice.Set(device,original);State=Status.Failed;appliedDevice=device;appliedKey=key;return; }
            }
            appliedDevice=device;appliedKey=key;nextVerify=now+2000;
            State=Strength>=0.999?Status.Active:Status.Limited;
        }
        void ApplySaturation(int saturation) {
            if(saturation==appliedSaturation)return;
            if(Magnification.SetMatrix(ScreenFilterMath.SaturationMatrix(saturation)))appliedSaturation=saturation;
            else appliedSaturation=saturation; // unsupported here; the gamma part still works
        }
        void PersistOriginals() {
            try { Directory.CreateDirectory(folder);ScreenFilterRestore.Write(restorePath,originals); }
            catch(Exception error) { Program.LogException("ScreenFilterPersist",error); }
        }
        // A second copy of this exe that waits for us and restores the ramp if we
        // vanish without cleaning up (crash, Task Manager, power-user kill).
        void EnsureWatchdog() {
            try {
                if(watchdog!=null&&!watchdog.HasExited)return;
                string exe=Process.GetCurrentProcess().MainModule.FileName;
                var start=new ProcessStartInfo(exe,"-ScreenFilterWatchdog "+Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)+" \""+restorePath+"\"") { UseShellExecute=false,CreateNoWindow=true };
                watchdog=Process.Start(start);
            } catch(Exception error) { Program.LogException("ScreenFilterWatchdog",error); }
        }
        internal static void RunWatchdog(string pid,string path) {
            int id;
            if(int.TryParse(pid,NumberStyles.Integer,CultureInfo.InvariantCulture,out id)) {
                try { using(var process=Process.GetProcessById(id))process.WaitForExit(); }catch(ArgumentException) { }catch(InvalidOperationException) { }
            }
            // A clean exit deletes the file first, so this only acts after a crash.
            try { ScreenFilterRestore.RestoreAndDelete(path); }catch(Exception error) { Program.LogException("ScreenFilterWatchdogRestore",error); }
        }

        void RestoreAll() {
            foreach(var pair in originals)GammaDevice.Set(pair.Key,pair.Value);
            originals.Clear();appliedDevice=null;appliedRamp=null;appliedKey=null;Strength=0;
            try { if(File.Exists(restorePath))File.Delete(restorePath); }catch(Exception error) { Program.LogException("ScreenFilterRestoreDelete",error); }
            if(appliedSaturation!=100) { Magnification.SetMatrix(ScreenFilterMath.SaturationMatrix(100));appliedSaturation=100; }
        }
        static void EmergencyRestore() {
            var controller=current;if(controller==null)return;
            try { foreach(var pair in controller.originals.ToArray())GammaDevice.Set(pair.Key,pair.Value); }catch(Exception) { }
            try { Magnification.Reset(); }catch(Exception) { }
        }
        public void Dispose() {
            if(disposed)return;disposed=true;timer.Stop();
            try { RestoreAll();Magnification.Reset(); }catch(Exception error) { Program.LogException("ScreenFilterDispose",error); }
            try { if(watchdog!=null&&!watchdog.HasExited)watchdog.Kill(); }catch(Exception) { }
            current=null;
        }
    }
}
