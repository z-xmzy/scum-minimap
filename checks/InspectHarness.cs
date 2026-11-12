using System;
using System.IO;
using System.Reflection;
class InspectHarness {
 [STAThread]static int Main(string[] args){
   try{Assembly.LoadFrom(Path.GetFullPath(args[0])).EntryPoint.Invoke(null,null);return 0;}
   catch(Exception e){for(;e!=null;e=e.InnerException){
       Console.Error.WriteLine(e.GetType().FullName);
       try{Console.Error.WriteLine(e.Message);}catch{}
       try{Console.Error.WriteLine(e.StackTrace);}catch{}
   }return 1;}
 }
}

