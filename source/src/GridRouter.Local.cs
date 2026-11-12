
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ScumMiniMap {
    internal sealed partial class GridRouter {
        readonly object fineGate=new object();
        FineSession fineSession;
        static readonly CultureInfo FineCulture=CultureInfo.InvariantCulture;
        static string FinePoint(PointF p) {
            return ((double)p.X).ToString("R",FineCulture)+","+((double)p.Y).ToString("R",FineCulture);
        }
        static PointF FineParse(string x,string y) {
            var p=new PointF(float.Parse(x,FineCulture),float.Parse(y,FineCulture));
            if(!Finite(p.X)||!Finite(p.Y)||p.X<0||p.Y<0||p.X>1||p.Y>1)throw new InvalidDataException("Fine coordinate");
            return p;
        }
        internal RoadRoute FindRoute(PointF a,PointF b,RoadRouteMode mode) {
            return FindCurrentRoute(a,b,mode,null);
        }
        internal RoadRoute FindCurrentRoute(PointF a,PointF b,RoadRouteMode mode,Func<bool> current) {
            RoadRoute first=FindBaseRoute(a,b,mode);
            if(first.Success||!isLoaded||worldGrade==null||mode==RoadRouteMode.Walking||
                !Enum.IsDefined(typeof(RoadRouteMode),mode)||!Finite(a.X)||!Finite(a.Y)||!Finite(b.X)||!Finite(b.Y)||
                Math.Min(a.X,a.Y)<0||Math.Min(b.X,b.Y)<0||Math.Max(a.X,a.Y)>=1||Math.Max(b.X,b.Y)>=1)return first;
            while(!Monitor.TryEnter(fineGate,50))if(current!=null&&!current())return first;
            try {
                if(current!=null&&!current())return first;
                if(fineSession==null)fineSession=new FineSession();
                var timeout=Stopwatch.StartNew();
                var cues=new HashSet<PointF>();
                fineSession.Send(mode+" "+FinePoint(a).Replace(',',' ')+" "+FinePoint(b).Replace(',',' '));
                for(;;) {
                    string line=fineSession.Receive(current,timeout);
                    if(line=="NONE")return first;
                    if(line.StartsWith("BASE ",StringComparison.Ordinal)) {
                        var fields=line.Split(' ');
                        if(fields.Length!=5)throw new InvalidDataException("Fine connector query");
                        var route=FindBaseRoute(FineParse(fields[1],fields[2]),FineParse(fields[3],fields[4]),mode);
                        if(!route.Success){fineSession.Send("-");continue;}
                        if(route.JunctionIndices!=null)foreach(int i in route.JunctionIndices)
                            if(i>0&&i<route.Polyline.Length-1)cues.Add(route.Polyline[i]);
                        fineSession.Send(string.Join(";",route.Polyline.Select(FinePoint)));
                        continue;
                    }
                    if(!line.StartsWith("OK ",StringComparison.Ordinal))throw new InvalidDataException("Fine response");
                    var path=new List<PointF>();
                    foreach(string item in line.Substring(3).Split(';')) {
                        var xy=item.Split(',');if(xy.Length!=2)throw new InvalidDataException("Fine geometry");
                        var p=FineParse(xy[0],xy[1]);
                        if(path.Count==0||p!=path[path.Count-1])path.Add(p);
                    }
                    if(path.Count==0||path[0]!=a||path[path.Count-1]!=b)throw new InvalidDataException("Fine endpoints");
                    lock(searchLock)return FineResult(path,mode,cues);
                }
            } catch(Exception ex) {
                if(fineSession!=null){fineSession.Dispose();fineSession=null;}
                if(!(ex is OperationCanceledException))Program.LogException("Fine routing",ex);
                return first;
            } finally {Monitor.Exit(fineGate);}
        }
        RoadRoute FineResult(List<PointF> path,RoadRouteMode mode,HashSet<PointF> cues) {
            var r=new RoadRoute{Success=true,Mode=mode,PlayerPoint=path[0],TargetPoint=path[path.Count-1],Polyline=path.ToArray()};
            var indices=new List<int>();var fine=levels[LevelFine];bool road=false;double lastRoad=0;
            var overlay=trailOverlay==null?null:trailOverlay.Fine;
            for(int i=1;i<path.Count;i++) {
                PointF a=path[i-1],b=path[i];double len=RoadRouter.DistanceMeters(a,b);
                int steps=Math.Max(1,(int)Math.Ceiling(len/2));double slice=len/steps;
                for(int j=0;j<steps;j++){
                    double t=(j+.5)/steps;
                    var sample=new PointF((float)(a.X+(b.X-a.X)*t),(float)(a.Y+(b.Y-a.Y)*t));
                    int id=ClampCell((int)(sample.Y*fine.Height),fine.Height)*fine.Width+ClampCell((int)(sample.X*fine.Width),fine.Width);
                    byte cost=TrailCost(overlay,id,fine.Data[id]);
                    if(cost==CostBlocked||cost==CostWater||cost==CostUndefined)cost=CostRough;
                    if(cost==CostPaved||cost==CostDirt){
                        if(!road){road=true;r.EntryDistanceMeters=r.TotalDistanceMeters+j*slice;r.RoadEntryPoint=sample;}
                        r.RoadExitPoint=sample;lastRoad=r.TotalDistanceMeters+(j+1)*slice;
                    }
                    if(cost==CostPaved)r.PavedDistanceMeters+=slice;else if(cost==CostDirt)r.DirtDistanceMeters+=slice;else r.UnknownDistanceMeters+=slice;
                    r.EstimatedSeconds+=slice/TravelSpeedFor(mode,cost,TrailMultiplier(overlay,id));
                }
                r.TotalDistanceMeters+=len;
                if(i<path.Count-1&&cues.Contains(b))indices.Add(i);
            }
            r.RoadDistanceMeters=r.PavedDistanceMeters+r.DirtDistanceMeters;
            if(road)r.ExitDistanceMeters=Math.Max(0,r.TotalDistanceMeters-lastRoad);
            else {r.RoadEntryPoint=r.PlayerPoint;r.RoadExitPoint=r.TargetPoint;r.EntryDistanceMeters=r.TotalDistanceMeters;}
            r.JunctionIndices=indices.ToArray();return r;
        }
        // A Windows job owns both the portable interpreter and its raster worker, including on abrupt map exit.
        sealed class FineSession:IDisposable {
            readonly string work;
            Process process; SafeFileHandle job;
            [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateJobObject(IntPtr attributes,string name);
            [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(SafeFileHandle job,int kind,IntPtr limits,uint size);
            [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
            [StructLayout(LayoutKind.Sequential)]struct BasicLimits {
                public long processTime,jobTime;public uint flags;public UIntPtr minWorking,maxWorking;
                public uint maxProcesses;public UIntPtr affinity;public uint priority,scheduling;
            }
            [StructLayout(LayoutKind.Sequential)]struct Limits {
                public BasicLimits basic;public ulong a,b,c,d,e,f;public UIntPtr g,h,i,j;
            }
            internal FineSession(){
                string root=Path.Combine(Path.GetDirectoryName(typeof(GridRouter).Assembly.Location),"fine");
                string runtime=Path.Combine(root,@"runtime\python.exe");
                if(!File.Exists(runtime))throw new FileNotFoundException("Fine routing runtime",runtime);
                work=Path.Combine(Program.UserDataDirectory,"fine-work",Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(work);
                try {
                    job=CreateJobObject(IntPtr.Zero,null);
                    var lim=new Limits();lim.basic.flags=0x2000;
                    int size=Marshal.SizeOf(lim);IntPtr buf=Marshal.AllocHGlobal(size);
                    try {Marshal.StructureToPtr(lim,buf,false);
                        if(job.IsInvalid||!SetInformationJobObject(job,9,buf,(uint)size))throw new System.ComponentModel.Win32Exception();
                    } finally {Marshal.FreeHGlobal(buf);}
                    var info=new ProcessStartInfo(runtime,"-B -I -u serve.py \""+work+"\""){
                        WorkingDirectory=root,CreateNoWindow=true,UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true
                    };
                    info.EnvironmentVariables["TEMP"]=work;info.EnvironmentVariables["TMP"]=work;
                    info.EnvironmentVariables["OPENBLAS_NUM_THREADS"]="1";info.EnvironmentVariables["OMP_NUM_THREADS"]="1";
                    process=Process.Start(info);
                    if(!AssignProcessToJobObject(job,process.Handle))throw new System.ComponentModel.Win32Exception();
                    process.PriorityClass=ProcessPriorityClass.BelowNormal;
                    AppDomain.CurrentDomain.ProcessExit+=OnExit;
                } catch {Dispose();throw;}
            }
            void OnExit(object sender,EventArgs e){Dispose();}
            internal void Send(string s){process.StandardInput.WriteLine(s);process.StandardInput.Flush();}
            internal string Receive(Func<bool> current,Stopwatch elapsed) {
                var line=process.StandardOutput.ReadLineAsync();
                while(!line.Wait(50)){
                    if(current!=null&&!current())throw new OperationCanceledException();
                    if(elapsed.Elapsed.TotalSeconds>90)throw new TimeoutException("Fine routing timed out");
                }
                if(current!=null&&!current())throw new OperationCanceledException();
                if(line.Result==null)throw new EndOfStreamException("Fine routing stopped");
                return line.Result;
            }
            public void Dispose(){
                lock(this){
                    AppDomain.CurrentDomain.ProcessExit-=OnExit;
                    if(job!=null){job.Dispose();job=null;}
                    if(process!=null){
                        try {if(!process.HasExited){process.Kill();process.WaitForExit(750);}}catch(InvalidOperationException){}
                        process.Dispose();process=null;
                    }
                    try{foreach(string file in Directory.GetFiles(work,"work.*"))File.Delete(file);Directory.Delete(work);}catch(IOException){}catch(UnauthorizedAccessException){}
                }
            }
        }
    }
}
