using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ScumMiniMap {
    internal sealed partial class GridRouter {
        sealed class LocalArea {
            internal int X,Y,Scale,Width,WorldWidth;
            internal byte[] Cells;
            internal int Index(PointF p) {
                int x=(int)Math.Floor(((double)p.X*WorldWidth-X)*Scale);
                int y=(int)Math.Floor(((double)p.Y*WorldWidth-Y)*Scale);
                return x<0||y<0||x>=Width||y>=Width?-1:y*Width+x;
            }
            PointF Center(int i) {
                return new PointF((float)((X+(i%Width+.5)/Scale)/WorldWidth),
                    (float)((Y+(i/Width+.5)/Scale)/WorldWidth));
            }
            bool Open(int i){return Cells[i]==0||Cells[i]>=4;}
            internal List<PointF> Approach(PointF point,int start) {
                if(!Open(start))return null;
                var parent=new int[Cells.Length];var queue=new Queue<int>();
                queue.Enqueue(start);parent[start]=-1;
                while(queue.Count>0){
                    int i=queue.Dequeue(),x=i%Width,y=i/Width;
                    if(Cells[i]>=4){
                        var path=new List<PointF>();
                        if(Cells[i]>4){
                            int dx=Cells[i]==5?-1:Cells[i]==6?1:0;
                            int dy=Cells[i]==7?-1:Cells[i]==8?1:0;
                            path.Add(new PointF((float)((X+(x+.5+dx)/Scale)/WorldWidth),(float)((Y+(y+.5+dy)/Scale)/WorldWidth)));
                        }
                        path.Add(Center(i));
                        int oldDx=0,oldDy=0;
                        while(i!=start){
                            int prev=parent[i]-1,dx=prev%Width-i%Width,dy=prev/Width-i/Width;
                            if((oldDx!=0||oldDy!=0)&&(dx!=oldDx||dy!=oldDy))path.Add(Center(i));
                            oldDx=dx;oldDy=dy;i=prev;
                        }
                        if(start!=Index(path[path.Count-1]))path.Add(Center(start));
                        if(path[path.Count-1]!=point)path.Add(point);path.Reverse();return path;
                    }
                    for(int k=0;k<8;k++){
                        int nx=x+NeighborX[k],ny=y+NeighborY[k];
                        if(nx<0||ny<0||nx>=Width||ny>=Width)continue;
                        int next=ny*Width+nx;
                        if(parent[next]!=0 || !Open(next))continue;
                        if(nx!=x && ny!=y && (!Open(y*Width+nx)||!Open(ny*Width+x)))continue;
                        parent[next]=i+1;queue.Enqueue(next);
                    }
                }
                return null;
            }
        }
        LocalArea[] localAreas;
        void LoadLocalAreas(int worldWidth){
            using(var b=OpenWorldLayer("nav-local.bin")){
                if(b.ReadUInt32()!=0x314C434C || b.ReadInt32()!=worldWidth)throw new InvalidDataException("Local area header");
                int count=b.ReadInt32();if(count<0||count>4096)throw new InvalidDataException("Local area count");
                var areas=new LocalArea[count];
                for(int i=0;i<count;i++){
                    int x=b.ReadInt32(),y=b.ReadInt32(),size=b.ReadInt32(),scale=b.ReadInt32();
                    if(x<0||y<0||size<1||size>128||x+size>worldWidth||y+size>worldWidth||
                        scale<1||scale>128||size*scale>2048)throw new InvalidDataException("Local area extent");
                    int width=size*scale;var cells=b.ReadBytes(width*width);
                    if(cells.Length!=width*width)throw new EndOfStreamException();
                    areas[i]=new LocalArea{X=x,Y=y,Scale=scale,Width=width,Cells=cells,WorldWidth=worldWidth};
                }
                localAreas=areas;
            }
        }
        bool TryApproach(PointF p,out List<PointF> path){
            path=null;
            foreach(var a in localAreas){
                int i=a.Index(p);if(i<0)continue;
                path=a.Approach(p,i);return path!=null;
            }
            return true;
        }
        internal RoadRoute FindRoute(PointF from,PointF to,RoadRouteMode mode){
            RoadRoute first=FindBaseRoute(from,to,mode);
            if(first.Success||!isLoaded||localAreas==null||mode==RoadRouteMode.Walking||
                !Enum.IsDefined(typeof(RoadRouteMode),mode)||!Finite(from.X)||!Finite(from.Y)||!Finite(to.X)||!Finite(to.Y))return first;
            List<PointF> a,b;
            if(!TryApproach(from,out a)||!TryApproach(to,out b)||(a==null&&b==null))return first;
            PointF entry=a==null?from:a[a.Count-1],exit=b==null?to:b[b.Count-1];
            RoadRoute route=FindBaseRoute(entry,exit,mode);
            if(!route.Success||route.Polyline.Length==0)return first;
            var points=new List<PointF>();int shift=0;
            if(a!=null){
                points.AddRange(a);
                if(points.Count>0 && points[points.Count-1]==route.Polyline[0])points.RemoveAt(points.Count-1);
                shift=points.Count;
            }
            points.AddRange(route.Polyline);
            if(b!=null){
                b.Reverse();
                if(b.Count>0 && b[0]==points[points.Count-1])b.RemoveAt(0);
                points.AddRange(b);
            }
            double startLength=0,endLength=0;
            for(int i=1;i<=shift;i++)startLength+=RoadRouter.DistanceMeters(points[i-1],points[i]);
            for(int i=shift+route.Polyline.Length;i<points.Count;i++)endLength+=RoadRouter.DistanceMeters(points[i-1],points[i]);
            if(route.JunctionIndices!=null)for(int i=0;i<route.JunctionIndices.Length;i++)route.JunctionIndices[i]+=shift;
            route.Polyline=points.ToArray();route.PlayerPoint=from;route.TargetPoint=to;
            route.EntryDistanceMeters+=startLength;route.ExitDistanceMeters+=endLength;
            route.TotalDistanceMeters+=startLength+endLength;route.UnknownDistanceMeters+=startLength+endLength;
            route.EstimatedSeconds+=(startLength+endLength)/TravelSpeedFor(mode,CostRough,1);
            return route;
        }
    }
}
