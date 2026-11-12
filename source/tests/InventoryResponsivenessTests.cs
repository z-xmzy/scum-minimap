using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

// A delayed capture must not stall the caller; stale captures must not change input ownership.
static class InventoryResponsivenessTests {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static Type type;
    static MethodInfo poll;
    static object Fixture() {
        object value=FormatterServices.GetUninitializedObject(type);
        GC.SuppressFinalize(value);
        return value;
    }
    static void Set(object value,string name,object data) { type.GetField(name,Flags).SetValue(value,data); }
    static T Get<T>(object value,string name) { return (T)type.GetField(name,Flags).GetValue(value); }
    static void Poll(object value,DateTime now,bool focused,Func<bool> capture) { poll.Invoke(value,new object[]{now,focused,capture}); }
    static void Drain(object value) {
        Task task=Get<Task>(value,"inventoryProbeTask");
        if(task!=null)try { Check(task.Wait(3000),"capture completed"); }catch(AggregateException) { }
    }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message); }
    static void Sample(object value,DateTime now,bool result) {
        Poll(value,now,true,()=>result);Drain(value);Poll(value,now,true,()=>result);
    }
    [STAThread] static int Main(string[] args) {
        try {
            type=Assembly.LoadFrom(args[0]).GetType("ScumMiniMap.MapWindow",true);
            // The original release calls its synchronous gate directly.
            poll=type.GetMethod("PollInventoryInputGate",Flags)??type.GetMethod("UpdateInventoryInputGate",Flags);
            DateTime now=new DateTime(2026,10,31,0,0,0,DateTimeKind.Utc);
            object fixture=Fixture();int calls=0;
            var watch=Stopwatch.StartNew();
            Poll(fixture,now,true,()=> { Interlocked.Increment(ref calls);Thread.Sleep(250);return true; });
            watch.Stop();Console.WriteLine("UI caller with simulated 250ms capture: "+watch.Elapsed.TotalMilliseconds.ToString("F2")+" ms");
            Check(watch.ElapsedMilliseconds<100,"slow screen capture does not block UI polling");
            for(int i=1;i<=20;i++)Poll(fixture,now.AddMilliseconds(i),true,()=> { Interlocked.Increment(ref calls);return false; });
            Drain(fixture);Check(calls==1,"only one capture is in flight");
            Poll(fixture,now.AddMilliseconds(25),true,()=>false);
            Check(Get<bool>(fixture,"inventoryInputLocked"),"completed inventory highlight locks input");
            int skipped=0;Poll(fixture,now.AddMilliseconds(144),true,()=> { skipped++;return false; });
            Check(skipped==0&&Get<Task>(fixture,"inventoryProbeTask")==null,"no new capture before the 120ms interval");
            Sample(fixture,now.AddMilliseconds(145),false);
            Check(Get<bool>(fixture,"inventoryInputLocked"),"one negative sample retains the inventory lock");
            Sample(fixture,now.AddMilliseconds(265),false);
            Check(!Get<bool>(fixture,"inventoryInputLocked"),"two negative samples release the inventory lock");

            foreach(string context in new[]{"focus","map","admin"}) {
                fixture=Fixture();var release=new ManualResetEventSlim();
                Poll(fixture,now,true,()=> { if(!release.Wait(3000))throw new TimeoutException();return true; });
                if(context=="map")Set(fixture,"fullMapActive",true);
                if(context=="admin")Set(fixture,"adminCommandBusy",true);
                Poll(fixture,now.AddMilliseconds(10),context!="focus",()=>false);
                release.Set();Drain(fixture);
                Set(fixture,"fullMapActive",false);Set(fixture,"adminCommandBusy",false);
                Poll(fixture,now.AddMilliseconds(20),true,()=>false);
                Check(!Get<bool>(fixture,"inventoryInputLocked"),"discard capture after "+context+" ownership changes");
                Drain(fixture);release.Dispose();
            }
            fixture=Fixture();Poll(fixture,now,true,()=> { throw new InvalidOperationException("synthetic capture failure"); });Drain(fixture);
            Poll(fixture,now,true,()=>false);
            Check(Get<bool>(fixture,"inventoryInputLocked"),"capture failure keeps input protected");
            fixture=Fixture();Poll(fixture,now,true,()=>true);Drain(fixture);
            Poll(fixture,now.AddSeconds(2),true,()=>false);
            Check(!Get<bool>(fixture,"inventoryInputLocked"),"expired capture cannot change the inventory gate");Drain(fixture);
            Console.WriteLine("Inventory responsiveness checks passed.");return 0;
        }catch(Exception error) { Console.Error.WriteLine(error);return 1; }
    }
}
