using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using RevitOpenGuard;
using RevitOpenGuard.Infrastructure;
class Program {
 static int checks;
 static void Assert(bool value,string name){if(!value)throw new Exception(name);checks++;}
 static void Pump(){Assert(SpinWait.SpinUntil(()=>!ExternalEvent.Queue.IsEmpty,5000),"Background check signals a Revit UI event");if(ExternalEvent.Queue.TryDequeue(out var run))run();}
 sealed class Fake:HttpMessageHandler {
  readonly byte[] bytes;internal Uri? Uri;internal bool NoCache,NoStore,Pragma;
  internal Fake(byte[] value){bytes=value;}
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
   Uri=request.RequestUri;NoCache=request.Headers.CacheControl?.NoCache==true;NoStore=request.Headers.CacheControl?.NoStore==true;Pragma=request.Headers.Pragma.Any(p=>p.Name=="no-cache");
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});
  }
 }
 static int Main(string[] args){
  try{
   CatalogStore.StorageDirectory=AuditLog.RootDirectory;
   byte[] fixture=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"fixtures","catalog-1.0.2.signed.json"));
   Assert(CatalogStore.Verify(fixture).Version=="1.0.2","Real signed public test fixture");
   var first=new Fake(fixture);string trace="";
   var pending=CatalogStore.CheckAsync(first,s=>trace=s).GetAwaiter().GetResult();
   Assert(pending!=null&&CatalogStore.Current.Version=="1.0.0","Fetch detects update without applying");
   Assert(first.NoCache&&first.NoStore&&first.Pragma,"Cache bypass request headers");
   Assert(first.Uri?.Query.StartsWith("?check=")==true,"Fresh request URL");
   var second=new Fake(fixture);CatalogStore.CheckAsync(second).GetAwaiter().GetResult();
   Assert(first.Uri!=second.Uri,"Each check uses a distinct cache key");
   Assert(trace.Contains("Remote=1.0.2")&&trace.Contains("Local=1.0.0")&&trace.Contains("UpdateAvailable=True"),"Successful version comparison is observable");
   Assert(!CatalogStore.CheckDue,"A successful check just occurred");
   int calls=0;var log=new AuditLog();
   Func<Task<byte[]?>> fetch=()=>{Interlocked.Increment(ref calls);return Task.FromResult<byte[]?>(fixture);};
   CatalogUpdateNotice.Start(log,fetch);CatalogUpdateNotice.Start(log,fetch);
   Pump();Assert(calls==1,"Startup checks despite recent timestamp and only once per session");
   Assert(TaskDialog.Shows==1&&TaskDialog.LastInstruction.Contains("1.0.2"),"External event offers update without an Idling callback");
   Assert(CatalogStore.Current.Version=="1.0.0","Close does not apply update");
   CatalogUpdateNotice.OnIdle();Assert(TaskDialog.Shows==1,"No repeated modal notification during same session");
   CatalogUpdateNotice.Stop();
   CatalogUpdateNotice.WindowOpen=true;CatalogUpdateNotice.Start(log,fetch);Pump();
   Assert(TaskDialog.Shows==1,"No automatic dialog on top of manual update window");
   CatalogUpdateNotice.WindowOpen=false;CatalogUpdateNotice.RequestNotification();Pump();
   Assert(TaskDialog.Shows==2,"Manual-window close resumes pending notification");
   CatalogUpdateNotice.Stop();
   App.Service.IsOpening=true;CatalogUpdateNotice.Start(log,fetch);Pump();
   Assert(TaskDialog.Shows==2,"Never show update while a model is opening");
   App.Service.IsOpening=false;CatalogUpdateNotice.OnIdle();Assert(TaskDialog.Shows==3,"Pending notification survives model opening");
   CatalogUpdateNotice.Stop();
   CatalogUpdateNotice.Start(log,()=>Task.FromException<byte[]?>(new IOException("offline")));Pump();
   Assert(TaskDialog.Shows==3&&TaskDialog.Errors==0&&CatalogStore.Current.Version=="1.0.0","Network failure stays quiet and preserves catalog");
   CatalogUpdateNotice.Stop();
   TaskDialog.Response=TaskDialogResult.CommandLink1;CatalogUpdateNotice.Start(log,fetch);Pump();
   Assert(CatalogStore.Current.Version=="1.0.2"&&File.Exists(Path.Combine(CatalogStore.StorageDirectory,"catalog.signed.json")),"Only explicit Update applies verified rules");
   CatalogUpdateNotice.Stop();
   CatalogUpdateNotice.Start(log,fetch);Pump();Assert(TaskDialog.Shows==4,"No dialog when current catalog equals downloaded version");
   CatalogUpdateNotice.Stop();CatalogUpdateNotice.RequestNotification();Assert(ExternalEvent.Queue.IsEmpty,"Shutdown disposes signal and suppresses notifications");
   Assert(AuditLog.Entries.Any(s=>s.Contains("Showing update notification"))&&AuditLog.Entries.Any(s=>s.Contains("Catalog applied by user")),"Notification and application logged");
   if(args.Contains("--online")){
    string online="";CatalogStore.CheckAsync(trace:s=>online=s).GetAwaiter().GetResult();
    Assert(online.Contains("Remote=1.0.2"),"Real cache-bypassed HTTPS feed verifies");
   }
   Console.WriteLine("PASS: "+checks+" notification/HTTP checks on "+System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription+". Revit UI is simulated; real-host notification remains to be checked.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
  finally{CatalogUpdateNotice.Stop();if(Directory.Exists(AuditLog.RootDirectory))Directory.Delete(AuditLog.RootDirectory,true);}
 }
}
