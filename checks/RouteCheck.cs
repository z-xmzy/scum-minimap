using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Text;
class RouteCheck {
 static BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static IEnumerable<DependencyObject> Kids(DependencyObject p){foreach(var item in LogicalTreeHelper.GetChildren(p)){var c=item as DependencyObject;if(c==null)continue;yield return c;foreach(var k in Kids(c))yield return k;}}
 static T Ui<T>(string tag) where T:FrameworkElement { return Kids(w).OfType<T>().Single(v=>(string)v.Tag==tag); }
 static void Click(string tag){var b=Ui<Button>(tag);Check(b.IsEnabled,"enabled "+tag);b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static void Check(bool ok,string n){if(!ok)throw new Exception(n);Console.WriteLine("PASS "+n);}
 static Window w;static object rt,engine;static Assembly asm;
 static void Open(){w=(Window)Call(rt,"BuildSquadTools",0);w.ShowActivated=false;w.WindowStartupLocation=WindowStartupLocation.Manual;w.Left=-32000;w.Top=-32000;w.Show();w.UpdateLayout();System.Windows.Forms.Application.DoEvents();}
 static void Pos(double u,double v){var p=Activator.CreateInstance(asm.GetType("ScumMiniMap.Position"));Set(p,"X",617718-u*1521618);Set(p,"Y",618618-v*1523618);Set(engine,"position",p);Set(engine,"updated",DateTime.UtcNow);}
 static void At(double u,double v,int expected){Pos(u,v);Call(rt,"UpdateSquad");if(expected<0)Check(Field(engine,"searchTarget")==null,"normal finishes at end");else Check((int)Field(rt,"activeStop")==expected&&Field(engine,"searchTarget")!=null,"next stop "+expected);}
 [STAThread]static int Main(string[] a){try{
 asm=Assembly.LoadFrom(Path.GetFullPath(a[0]));var profile=Path.GetFullPath(a[1]);Directory.CreateDirectory(profile);
 File.WriteAllText(Path.Combine(profile,"settings.ini"),"Welcomed=True\nLanguage=zh-CN\nWpfDefaultsRevision=1\n");
 rt=Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime"),F,null,new object[]{profile,true},null);engine=Field(rt,"engine");
 try{
 Open();var list=Ui<ComboBox>("lx-list");var codeBox=Ui<TextBox>("lx-code");
 if(a[2]=="save"){
 var squad=Field(rt,"squad");Call(squad,"AddStop","璧风偣 A",.5,.5);Call(squad,"AddStop","閫旂粡 B",.501,.50);Call(squad,"AddStop","鍦板牎 C",.5,.52);
 for(int i=0;i<2;i++){Ui<TextBox>("lx-name").Text="娴嬭瘯 馃殬 "+i;Ui<ComboBox>("lx-kind").SelectedIndex=i;Click("lx-save");var code=(string)Call(list.SelectedItem,"Code");Check(code.StartsWith("LX-")&&code.All(c=>!char.IsWhiteSpace(c)),"one line LX- code");File.WriteAllText(Path.Combine(a[3],i+".txt"),code);}
 Check(list.Items.Count==2,"two named routes persisted");
 }else if(a[2]=="reload"){Check(list.Items.Count==2,"local library restored in fresh process");}
 else{
 string normal=File.ReadAllText(Path.Combine(a[3],"0.txt")),loop=File.ReadAllText(Path.Combine(a[3],"1.txt"));
 foreach(var code in new[]{normal,loop,loop}){codeBox.Text=code;Click("lx-import");Check((string)Call(list.SelectedItem,"Code")==code,"import round trip");}
 Check(list.Items.Count==2&&Field(engine,"searchTarget")==null,"idempotent import leaves navigation idle");
 foreach(var bad in new[]{normal.Substring(3),normal.Substring(0,normal.Length-2),normal.Substring(0,8)+(normal[8]=='A'?'B':'A')+normal.Substring(9),"LX-"+new string('A',11000)}){codeBox.Text=bad;Click("lx-import");Check(list.Items.Count==2&&Field(engine,"searchTarget")==null,"invalid input preserves routes");}
 var saved=list.SelectedItem;var decode=saved.GetType().GetMethod("Decode",BindingFlags.Static|BindingFlags.NonPublic);
 try{decode.Invoke(null,new object[]{loop,"other-map"});throw new Exception("wrong map accepted");}catch(TargetInvocationException){Check(true,"wrong map rejected");}
 var fixture=Activator.CreateInstance(asm.GetType("ScumMiniMap.SquadState"),F,null,new object[]{null},null);
 for(int j=0;j<16;j++)Call(fixture,"AddStop",new string('站',93)+j,.32+j*.002,.48);
 var s16=fixture.GetType().GetProperty("Stops",F).GetValue(fixture,null);
 var max=Activator.CreateInstance(saved.GetType(),F,null,new object[]{new string('路',64),"default",true,s16},null);
 var maxCode=(string)Call(max,"Code");
 var decoded=decode.Invoke(null,new object[]{maxCode,"default"});
 Check(maxCode.Length<10000&&((Array)Field(decoded,"Stops")).Length==16,"16 long Unicode stops round trip");
 Call(rt,"StartSavedRoute",decoded);
 Check(((System.Collections.IEnumerable)Call(rt,"RemainingStops")).Cast<object>().Count()==17,"full-size loop includes closing leg");
 Call(rt,"UpdateRoutePreviews");Call(rt,"StopSquadNavigation");
 for(int i=0;i<2;i++){
 if(i>0){Open();list=Ui<ComboBox>("lx-list");}
 list.SelectedItem=list.Items.Cast<object>().Single(x=>(bool)Field(x,"Loop")==(i==1));
 Pos(.501,.50);Set(engine,"diagnosticMode",false);Click("lx-start");
 Check((int)Field(rt,"activeStop")==0&&((string)Field(Field(engine,"searchTarget"),"Name")).Contains("璧风偣 A"),"starts at A even when player is at B");
 At(.5,.5,1);Call(rt,"UpdateSquad");Check((int)Field(rt,"activeStop")==1,"same GPS sample cannot advance twice");At(.501,.5,2);
 if(i==0)At(.5,.52,-1);
 else{At(.5,.52,0);Check((bool)Field(rt,"returningToStart"),"loop targets return leg");At(.5,.5,1);At(.501,.5,2);At(.5,.52,0);At(.5,.5,1);Call(rt,"StopSquadNavigation");Check(Field(engine,"searchTarget")==null&&Field(rt,"activeSavedRoute")==null,"manual stop ends loop");}
 Set(engine,"diagnosticMode",true);
 }
 }
 }finally{Set(engine,"diagnosticMode",true);if(w!=null&&w.IsVisible)w.Close();((IDisposable)rt).Dispose();}
 return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
}