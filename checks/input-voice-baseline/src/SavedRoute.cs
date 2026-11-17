using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ScumMiniMap {
    internal sealed class SavedRoute {
        const string Prefix="LX-";
        internal const int MaxCodeLength=10000;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        internal readonly string Name,Map;
        internal readonly bool Loop;
        internal readonly SharedMapMarker[] Stops;
        internal SavedRoute(string name,string map,bool loop,SharedMapMarker[] stops) {
            Name=(name??"").Trim();
            if(Name.Length==0||Name.Length>64||Name.Any(char.IsControl)||string.IsNullOrEmpty(map)||map.Length>80||
                stops==null||stops.Length<2||stops.Length>SquadState.MaximumStops)throw new InvalidDataException();
            Map=map;Loop=loop;
            var ordered=new SquadState();
            for(int i=0;i<stops.Length;i++) {
                var m=stops[i];
                if(m==null||!ordered.AddStop(m.Name,m.X,m.Y))throw new InvalidDataException();
                if((i>0||loop)&&SamePoint(m,stops[(i+stops.Length-1)%stops.Length]))throw new InvalidDataException();
            }
            Stops=ordered.Stops;
        }
        static bool SamePoint(SharedMapMarker a,SharedMapMarker b) { return (float)a.X==(float)b.X&&(float)a.Y==(float)b.Y; }
        static byte[] Digest(byte[] data) { using(var sha=SHA256.Create())return sha.ComputeHash(data); }
        internal string Code() {
            using(var stream=new MemoryStream()) {
                using(var w=new BinaryWriter(stream,Utf8,true)) {
                    w.Write((byte)1);w.Write(Map);w.Write(Name);w.Write(Loop);w.Write((byte)Stops.Length);
                    foreach(var m in Stops) { w.Write(m.Name);w.Write(m.X);w.Write(m.Y); }
                }
                var hash=Digest(stream.ToArray());stream.Write(hash,0,4);
                return Prefix+Convert.ToBase64String(stream.ToArray()).TrimEnd('=').Replace('+','-').Replace('/','_');
            }
        }
        internal static SavedRoute Decode(string input,string map) {
            string code=(input??"").Trim();
            if(code.Length>MaxCodeLength||!code.StartsWith(Prefix,StringComparison.Ordinal))throw new InvalidDataException();
            string text=code.Substring(Prefix.Length);
            if(text.Any(c=>!(c>='A'&&c<='Z'||c>='a'&&c<='z'||c>='0'&&c<='9'||c=='-'||c=='_')))throw new InvalidDataException();
            var data=Convert.FromBase64String(text.Replace('-','+').Replace('_','/').PadRight((text.Length+3)/4*4,'='));
            if(data.Length<8)throw new InvalidDataException();
            int length=data.Length-4;
            if(!Digest(data.Take(length).ToArray()).Take(4).SequenceEqual(data.Skip(length)))throw new InvalidDataException();
            using(var r=new BinaryReader(new MemoryStream(data,0,length,false),Utf8)) {
                if(r.ReadByte()!=1||r.ReadString()!=map)throw new InvalidDataException();
                string name=r.ReadString();int kind=r.ReadByte(),count=r.ReadByte();
                if(kind>1||count<2||count>SquadState.MaximumStops)throw new InvalidDataException();
                var stops=new SharedMapMarker[count];
                for(int i=0;i<count;i++)stops[i]=new SharedMapMarker { Name=r.ReadString(),X=r.ReadDouble(),Y=r.ReadDouble() };
                var route=new SavedRoute(name,map,kind==1,stops);
                if(r.BaseStream.Position!=length||route.Code()!=code)throw new InvalidDataException();
                return route;
            }
        }
        internal void Store(string directory) {
            Directory.CreateDirectory(directory);
            string code=Code(),id=BitConverter.ToString(Digest(Utf8.GetBytes(code))).Replace("-","");
            string file=Path.Combine(directory,id+".lx");
            if(File.Exists(file)&&File.ReadAllText(file,Utf8)==code)return;
            string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllText(temp,code,Utf8);
                if(File.Exists(file))File.Replace(temp,file,null);
                else try { File.Move(temp,file); }catch(IOException) { if(!File.Exists(file)||File.ReadAllText(file,Utf8)!=code)throw; }
            }finally { if(File.Exists(temp))File.Delete(temp); }
        }
        public override string ToString() { return Name+" · "+Localization.Get(Loop?"LxLoop":"LxNormal")+" · "+Stops.Length; }
    }
}