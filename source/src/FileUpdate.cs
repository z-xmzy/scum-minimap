using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ScumMiniMap {
    // Incremental updates: each release publishes files.txt (path, SHA256, size and byte range of every
    // installed file) and files.pack (the files as concatenated raw-deflate streams). The client hashes
    // its installation, fetches only the changed files with HTTP range requests, stages and verifies
    // them, then a private copy of this executable swaps them in after MiniMap has exited.
    public sealed class FileManifest {
        public sealed class Entry {
            public string Path { get; internal set; }
            public string Sha256 { get; internal set; }
            public long Size { get; internal set; }
            public long Offset { get; internal set; }
            public long Length { get; internal set; }
        }
        public Version Version { get; private set; }
        public string VersionText { get { return Version.ToString(3); } }
        public long PackSize { get; private set; }
        public readonly List<Entry> Files=new List<Entry>();
        public const int MaxManifestBytes=4*1024*1024;

        public static FileManifest Parse(string text) {
            if(string.IsNullOrWhiteSpace(text) || text.Length>MaxManifestBytes)throw new FormatException("Invalid file manifest.");
            var manifest=new FileManifest();string schema=null;
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string raw in text.TrimStart('\uFEFF').Split('\n')) {
                string line=raw.TrimEnd('\r');
                if(line.Length==0)continue;
                int split=line.IndexOf('=');
                if(split<1)throw new FormatException("Invalid file manifest line.");
                string key=line.Substring(0,split),value=line.Substring(split+1);
                if(key=="schema") { if(schema!=null)throw new FormatException("Duplicate schema.");schema=value; }
                else if(key=="version") {
                    Version parsed;
                    if(manifest.Version!=null || !Regex.IsMatch(value,@"\A(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\z") || !Version.TryParse(value+".0",out parsed))
                        throw new FormatException("Invalid file manifest version.");
                    manifest.Version=parsed;
                } else if(key=="pack-size") {
                    long size;
                    if(manifest.PackSize!=0 || !long.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out size) || size<=0)throw new FormatException("Invalid pack size.");
                    manifest.PackSize=size;
                } else if(key=="file") {
                    string[] parts=value.Split('|');
                    long size,offset,length;
                    if(parts.Length!=5 || !Regex.IsMatch(parts[0],@"\A[0-9a-f]{64}\z") ||
                       !long.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out size) ||
                       !long.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out offset) ||
                       !long.TryParse(parts[3],NumberStyles.None,CultureInfo.InvariantCulture,out length) || length<=0)
                        throw new FormatException("Invalid file manifest entry.");
                    string path=CheckRelativePath(parts[4]);
                    if(!seen.Add(path))throw new FormatException("Duplicate file manifest entry.");
                    manifest.Files.Add(new Entry { Sha256=parts[0],Size=size,Offset=offset,Length=length,Path=path });
                } else throw new FormatException("Unknown file manifest field.");
            }
            if(schema!="1" || manifest.Version==null || manifest.PackSize==0 || manifest.Files.Count==0)throw new FormatException("Incomplete file manifest.");
            foreach(var file in manifest.Files)
                if(file.Offset<0 || file.Offset+file.Length>manifest.PackSize)throw new FormatException("File range lies outside the pack.");
            return manifest;
        }
        // Only plain relative paths inside the installation; never data, parent folders or rooted paths.
        internal static string CheckRelativePath(string path) {
            if(string.IsNullOrEmpty(path) || path.Length>260 || path.IndexOf('\\')>=0 || path.StartsWith("/",StringComparison.Ordinal) || path.IndexOf(':')>=0)
                throw new FormatException("Invalid file path in manifest.");
            foreach(string part in path.Split('/')) {
                if(part.Length==0 || part=="." || part==".." || part.Trim()!=part || part.EndsWith(".",StringComparison.Ordinal) || part.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())>=0)
                    throw new FormatException("Invalid file path in manifest.");
            }
            string lower=path.ToLowerInvariant();
            if(lower.StartsWith("app/data/",StringComparison.Ordinal) || lower.StartsWith("app/temp/",StringComparison.Ordinal) || lower.StartsWith(".update",StringComparison.Ordinal) ||
               lower.StartsWith("unins",StringComparison.Ordinal))
                throw new FormatException("The manifest may not replace user data or the uninstaller.");
            return path.Replace('/',System.IO.Path.DirectorySeparatorChar);
        }
    }

    public sealed class FileUpdatePlan {
        public FileManifest Manifest { get; internal set; }
        public string Root { get; internal set; }
        public readonly List<FileManifest.Entry> Needed=new List<FileManifest.Entry>();
        public long DownloadBytes { get { long total=0;foreach(var e in Needed)total+=e.Length;return total; } }
        public string Summary { get { return Needed.Count.ToString(CultureInfo.InvariantCulture)+" files, "+(DownloadBytes/1048576.0).ToString("0.0",CultureInfo.InvariantCulture)+" MB"; } }
    }

    public static class FileUpdate {
        internal const string StagingFolder=".update-staging";
        internal const string RangeGap="1048576";

        // The installation root holds app\SkynettMiniMap.exe.
        public static string InstallRoot(string executable) {
            string appFolder=Path.GetDirectoryName(Path.GetFullPath(executable));
            if(!string.Equals(Path.GetFileName(appFolder),"app",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("MiniMap is not running from an installed layout.");
            return Path.GetDirectoryName(appFolder);
        }

        public static FileUpdatePlan Plan(FileManifest manifest,string root,CancellationToken cancel) {
            var plan=new FileUpdatePlan { Manifest=manifest,Root=root };
            bool roadMarker=Directory.Exists(Path.Combine(root,"dist"));
            foreach(var entry in manifest.Files) {
                cancel.ThrowIfCancellationRequested();
                if(!roadMarker && entry.Path.StartsWith("dist"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))continue;
                string local=Path.Combine(root,entry.Path);
                if(File.Exists(local) && new FileInfo(local).Length==entry.Size && HashFile(local)==entry.Sha256)continue;
                plan.Needed.Add(entry);
            }
            return plan;
        }

        internal static string HashFile(string path) {
            using(var sha=SHA256.Create()) using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,1<<20))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }

        // Groups needed entries into few range requests: neighbours closer than 1 MB share one request.
        internal static List<List<FileManifest.Entry>> Ranges(IEnumerable<FileManifest.Entry> needed,long maxGap,long maxRange) {
            var sorted=new List<FileManifest.Entry>(needed);sorted.Sort((a,b)=>a.Offset.CompareTo(b.Offset));
            var groups=new List<List<FileManifest.Entry>>();List<FileManifest.Entry> current=null;long start=0,end=0;
            foreach(var entry in sorted) {
                if(current!=null && entry.Offset-end<=maxGap && entry.Offset+entry.Length-start<=maxRange) {
                    current.Add(entry);end=Math.Max(end,entry.Offset+entry.Length);continue;
                }
                current=new List<FileManifest.Entry> { entry };groups.Add(current);start=entry.Offset;end=entry.Offset+entry.Length;
            }
            return groups;
        }

        // fetchRange(offset,length,output,received,cancel) writes exactly that byte range of files.pack to output,
        // calling received(bytesSoFar) as data arrives.
        public static void Download(FileUpdatePlan plan,string staging,Action<long,long,Stream,Action<long>,CancellationToken> fetchRange,Action<long,long> progress,CancellationToken cancel) {
            Directory.CreateDirectory(staging);
            long total=0,done=0;
            var groups=Ranges(plan.Needed,1<<20,64L<<20);
            foreach(var group in groups) { long s0=group[0].Offset,e0=0;foreach(var e in group)e0=Math.Max(e0,e.Offset+e.Length);total+=e0-s0; }
            string rangeFile=Path.Combine(staging,"range.bin");
            try {
                foreach(var group in groups) {
                    cancel.ThrowIfCancellationRequested();
                    long start=group[0].Offset,end=0;
                    foreach(var e in group)end=Math.Max(end,e.Offset+e.Length);
                    long before=done;
                    using(var block=new FileStream(rangeFile,FileMode.Create,FileAccess.ReadWrite,FileShare.None,1<<16)) {
                        fetchRange(start,end-start,block,received=> { if(progress!=null)progress(before+received,total); },cancel);
                        if(block.Length!=end-start)throw new InvalidDataException("The update server returned an incomplete range.");
                        foreach(var entry in group) {
                            cancel.ThrowIfCancellationRequested();
                            string target=Path.Combine(staging,"files",entry.Path);
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            block.Position=entry.Offset-start;
                            using(var slice=new BoundedStream(block,entry.Length))
                            using(var inflate=new DeflateStream(slice,CompressionMode.Decompress,true))
                            using(var output=new FileStream(target,FileMode.Create,FileAccess.Write,FileShare.None)) {
                                byte[] buffer=new byte[1<<16];int read;long written=0;
                                while((read=inflate.Read(buffer,0,buffer.Length))>0) {
                                    written+=read;
                                    if(written>entry.Size)throw new InvalidDataException("Update file is larger than its manifest entry.");
                                    output.Write(buffer,0,read);
                                }
                            }
                            if(new FileInfo(target).Length!=entry.Size || HashFile(target)!=entry.Sha256)
                                throw new InvalidDataException("Update checksum mismatch: "+entry.Path);
                        }
                    }
                    done+=end-start;
                    if(progress!=null)progress(done,total);
                }
            } finally { try { File.Delete(rangeFile); } catch {} }
        }

        // Read-only window over part of another stream; the inflater never reads past one entry.
        sealed class BoundedStream:Stream {
            readonly Stream inner;long remaining;
            internal BoundedStream(Stream inner,long length) { this.inner=inner;remaining=length; }
            public override int Read(byte[] buffer,int offset,int count) {
                if(remaining<=0)return 0;
                int read=inner.Read(buffer,offset,(int)Math.Min(count,remaining));remaining-=read;return read;
            }
            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
            public override void Flush() {}
            public override long Seek(long offset,SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer,int offset,int count) { throw new NotSupportedException(); }
        }

        // ---- Hand-over to the private updater copy ----

        internal sealed class Request {
            internal int ProcessId;internal long StartTicks;
            internal string Root,Staging,Version,ResultPath;
            internal List<string> Files=new List<string>();
            internal string Encode() {
                var text=new StringBuilder();
                text.Append("SCUM-MiniMap-file-update-v1\n").Append(ProcessId.ToString(CultureInfo.InvariantCulture)).Append('\n')
                    .Append(StartTicks.ToString(CultureInfo.InvariantCulture)).Append('\n').Append(Root).Append('\n').Append(Staging).Append('\n')
                    .Append(Version).Append('\n').Append(ResultPath).Append('\n');
                foreach(string f in Files)text.Append(f).Append('\n');
                return text.ToString();
            }
            internal static Request Decode(string text) {
                string[] lines=text.Split('\n');
                if(lines.Length<8 || lines[0]!="SCUM-MiniMap-file-update-v1")throw new InvalidDataException("Invalid file update request.");
                var r=new Request { ProcessId=int.Parse(lines[1],CultureInfo.InvariantCulture),StartTicks=long.Parse(lines[2],CultureInfo.InvariantCulture),
                    Root=lines[3],Staging=lines[4],Version=lines[5],ResultPath=lines[6] };
                for(int i=7;i<lines.Length;i++)if(lines[i].Length>0)r.Files.Add(FileManifest.CheckRelativePath(lines[i].Replace('\\','/')));
                return r;
            }
        }

        // Copies this executable to a private temp folder and starts it in apply mode. The caller then exits.
        public static void StartApply(FileUpdatePlan plan,string staging,string resultPath) {
            var request=new Request { Root=plan.Root,Staging=Path.GetFullPath(staging),Version=plan.Manifest.VersionText,ResultPath=resultPath };
            using(var current=Process.GetCurrentProcess()) { request.ProcessId=current.Id;request.StartTicks=current.StartTime.ToUniversalTime().Ticks; }
            foreach(var e in plan.Needed)request.Files.Add(e.Path.Replace(Path.DirectorySeparatorChar,'/'));
            string helperFolder=Path.Combine(Path.GetTempPath(),"ScumMiniMap-file-update-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(helperFolder);
            string helper=Path.Combine(helperFolder,"Updater.exe");
            File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location,helper);
            string requestPath=Path.Combine(staging,"request.txt");
            File.WriteAllText(requestPath,request.Encode(),new UTF8Encoding(false));
            var process=Process.Start(new ProcessStartInfo {
                FileName=helper,Arguments="-ApplyFileUpdate \""+requestPath+"\"",UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=helperFolder
            });
            if(process==null)throw new IOException("Unable to start the update helper.");
            process.Dispose();
        }

        // Runs in the private copy: waits for MiniMap to exit, swaps files with rollback, then restarts MiniMap.
        public static void RunApply(string requestPath) {
            Request request=null;string message;
            var backups=new List<KeyValuePair<string,string>>();var created=new List<string>();
            try {
                request=Request.Decode(File.ReadAllText(Path.GetFullPath(requestPath),Encoding.UTF8));
                string root=Path.GetFullPath(request.Root),staging=Path.GetFullPath(request.Staging);
                if(!string.Equals(Path.GetDirectoryName(staging),Path.Combine(root,StagingFolder),StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unexpected staging folder.");
                WaitForExit(request.ProcessId,request.StartTicks,20000);
                StopProcessesUnder(root);
                string backupRoot=Path.Combine(staging,"backup");
                foreach(string relative in request.Files) {
                    string source=Path.Combine(staging,"files",relative),target=Path.Combine(root,relative);
                    if(!File.Exists(source))throw new FileNotFoundException("Staged update file is missing.",relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    if(File.Exists(target)) {
                        string backup=Path.Combine(backupRoot,relative);Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        MoveWithRetry(target,backup);backups.Add(new KeyValuePair<string,string>(backup,target));
                    } else created.Add(target);
                    MoveWithRetry(source,target);
                }
                UpdateUninstallVersion(root,request.Version);
                message="Update installed successfully. SCUM MiniMap v"+request.Version;
            } catch(Exception ex) {
                // Roll back in reverse order so the previous version starts again.
                for(int i=backups.Count-1;i>=0;i--)try {
                    if(File.Exists(backups[i].Value))File.Delete(backups[i].Value);
                    File.Move(backups[i].Key,backups[i].Value);
                } catch {}
                foreach(string path in created)try { File.Delete(path); } catch {}
                message="Update failed; the previous version was restored. "+ex.Message;
            }
            if(request!=null) {
                try { if(!string.IsNullOrEmpty(request.ResultPath)) { Directory.CreateDirectory(Path.GetDirectoryName(request.ResultPath));File.WriteAllText(request.ResultPath,message); } } catch {}
                try {
                    string exe=Path.Combine(Path.GetFullPath(request.Root),"app","SkynettMiniMap.exe");
                    var p=Process.Start(new ProcessStartInfo { FileName=exe,UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe) });
                    if(p!=null)p.Dispose();
                } catch {}
            }
        }

        static void MoveWithRetry(string from,string to) {
            for(int attempt=0;;attempt++) {
                try { File.Move(from,to);return; }
                catch(IOException) { if(attempt>=20)throw;Thread.Sleep(250); }
                catch(UnauthorizedAccessException) { if(attempt>=20)throw;Thread.Sleep(250); }
            }
        }

        static void WaitForExit(int pid,long startTicks,int graceMilliseconds) {
            Process process;
            try { process=Process.GetProcessById(pid); } catch(ArgumentException) { return; }
            using(process) {
                try { if(process.StartTime.ToUniversalTime().Ticks!=startTicks)return; } catch(InvalidOperationException) { return; }
                if(process.WaitForExit(graceMilliseconds))return;
                process.Kill();
                if(!process.WaitForExit(10000))throw new IOException("MiniMap did not exit; update cancelled.");
            }
        }

        // Navigation workers (python, WorldRaster) run from the installation and lock their files.
        static void StopProcessesUnder(string root) {
            string prefix=root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            int self;using(var current=Process.GetCurrentProcess())self=current.Id;
            foreach(var process in Process.GetProcesses())using(process) {
                try {
                    if(process.Id==self)continue;
                    string path=process.MainModule.FileName;
                    if(path!=null && path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) { process.Kill();process.WaitForExit(5000); }
                } catch {}
            }
        }

        static void UpdateUninstallVersion(string root,string version) {
            try {
                using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{6F0B7C52-3E8A-4D4B-9C2E-5A1D7E3B9F41}_is1",true)) {
                    if(key==null)return;
                    string location=key.GetValue("InstallLocation") as string;
                    if(location!=null && string.Equals(Path.GetFullPath(location).TrimEnd('\\'),root.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))
                        key.SetValue("DisplayVersion",version);
                }
            } catch {}
        }

        // Called at startup: removes staging left by a finished or abandoned update.
        public static void CleanupStaging(string executable) {
            try {
                string folder=Path.Combine(InstallRoot(executable),StagingFolder);
                if(Directory.Exists(folder))Directory.Delete(folder,true);
            } catch {}
        }
    }
}
