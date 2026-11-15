from pathlib import Path
p=Path('checks/RouteCheck.cs')
s=p.read_text()
a='for(int i=0;i<2;i++){\n if(i>0)'
s=s.replace(a,'''var fixture=Activator.CreateInstance(asm.GetType("ScumMiniMap.SquadState"),F,null,new object[]{null},null);
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
 if(i>0)''')
p.write_text(s,encoding='utf-8')
