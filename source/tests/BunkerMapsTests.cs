using System;
using ScumMiniMap;
class BunkerMapsTests {
    static void Check(bool value,string name) { if(!value)throw new Exception(name);Console.WriteLine("PASS "+name); }
    static BunkerFloor Floor(string id,double z) {
        var f=new BunkerFloor { Id=id,BunkerId="synthetic",MinX=0,MinY=0,MaxX=400,MaxY=400,SurfaceZ=z,CellSize=100,Width=4,Height=4,Heights=new float[16] };
        for(int i=0;i<16;i++)f.Heights[i]=(float)z;return f;
    }
    static int Main(string[] args) {
        try { return Run(args); }catch(Exception error) { Console.WriteLine("FAIL: "+error.Message);return 1; }
    }
    static int Run(string[] args) {
        var low=Floor("lower",1000);var high=Floor("upper",1500);
        var tracker=new BunkerTracker(new[]{low,high});var t=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
        tracker.Observe(200,200,1092,t);Check(tracker.Current==null,"single sample not enough");
        tracker.Observe(200,200,1092,t);Check(tracker.Current==null,"duplicate timestamp not a new sample");
        tracker.Observe(200,200,1092,t.AddMilliseconds(500));Check(tracker.Current==low,"enter lower layer");
        tracker.Observe(200,200,1062,t.AddSeconds(1));Check(tracker.Current==low,"crouch stays on same layer");
        tracker.Observe(200,200,1592,t.AddSeconds(2));Check(tracker.Current==low,"one higher report cannot flicker");
        tracker.Observe(200,200,1592,t.AddSeconds(2.5));Check(tracker.Current==high,"stacked floor switches by Z");
        tracker.Observe(200,200,1800,t.AddSeconds(3));Check(tracker.Current==high,"brief jump keeps map");
        tracker.Observe(200,200,1592,t.AddSeconds(3.5));Check(tracker.Current==high,"landing resets exit delay");
        tracker.Observe(900,900,1592,t.AddSeconds(4));tracker.Observe(900,900,1592,t.AddSeconds(5.1));Check(tracker.Current==null,"outside footprint exits");
        tracker.Observe(200,200,1592,t.AddSeconds(6));tracker.Observe(200,200,1592,t.AddSeconds(6.5));
        tracker.Observe(30000,30000,1592,t.AddSeconds(7));Check(tracker.Current==null,"teleport clears immediately");
        Check(double.IsPositiveInfinity(high.Score(200,200,1400)),"below surface not inside");
        var coarse=Floor("coarse exterior",1000);coarse.Heights[2*4+2]=float.NaN;
        Check(double.IsPositiveInfinity(coarse.Score(150,150,1092)),"coarse grid cannot pull exterior point into a neighbouring room");
        var overlap=Floor("stacked stairs",1500);
        overlap.SurfaceTriangles=new float[]{0,0,1000,400,0,1000,0,400,1000};
        Check(!double.IsPositiveInfinity(overlap.Score(100,100,1092)),"lower stair under another surface remains detectable");
        Check(double.IsPositiveInfinity(overlap.Score(350,350,1092)),"stair triangle does not fill its bounding rectangle");
        Check(double.IsPositiveInfinity(overlap.Score(100,100,1400)),"space between stair and upper surface stays outside");
        for(int i=0;i<16;i++)high.Heights[i]=float.NaN;
        Check(double.IsPositiveInfinity(high.Score(200,200,1592)),"unmapped hole never matches");
        tracker.Observe(double.NaN,200,1592,t.AddSeconds(8));Check(tracker.Current==null,"invalid coordinates rejected");
        if(args.Length>0) {
            var atlas=BunkerAtlas.Load(args[0]);Check(atlas.Floors.Length>=170,"real packaged atlas loads");
            if(args.Length>1) {
                int checks=0;
                foreach(string line in System.IO.File.ReadAllLines(args[1])) {
                    string[] cells=line.Split('\t');if(cells.Length!=4)throw new Exception("Invalid model regression fixture");
                    double x=double.Parse(cells[1],System.Globalization.CultureInfo.InvariantCulture),y=double.Parse(cells[2],System.Globalization.CultureInfo.InvariantCulture),z=double.Parse(cells[3],System.Globalization.CultureInfo.InvariantCulture);
                    var model=new BunkerTracker(atlas.Floors);model.Observe(x,y,z+92.15,t);model.Observe(x,y,z+92.15,t.AddSeconds(.5));
                    Check(model.Current!=null&&model.Current.BunkerId==cells[0],"model surface "+(++checks)+" enters correct bunker");
                    var selected=model.Current;model.Observe(x,y,z+62.15,t.AddSeconds(1));model.Observe(x,y,z+62.15,t.AddSeconds(2));
                    Check(model.Current==selected,"model surface "+checks+" crouch keeps layer");
                }
                Check(checks>=20,"all originally missed surface probes replayed");
            }
        }
        return 0;
    }
}
