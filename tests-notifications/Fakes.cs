using System;
using System.IO;
using System.Collections.Concurrent;
namespace RevitOpenGuard.Infrastructure {
 internal sealed class AuditLog {
  public static string RootDirectory=Path.Combine(Path.GetTempPath(),"OpenGuardNoticeTest-"+Guid.NewGuid().ToString("N"));
  internal static ConcurrentQueue<string> Entries=new ConcurrentQueue<string>();
  public void Write(string level,string eventName,string message,string openingId="",string model="",Exception? error=null) {Entries.Enqueue(level+" "+eventName+" "+message);}
 }
}
namespace RevitOpenGuard {
 internal static class App {internal static ServiceStub Service=new ServiceStub();}
 internal sealed class ServiceStub {internal bool IsOpening;}
}
namespace Autodesk.Revit.UI {
 public sealed class UIApplication {}
 public interface IExternalEventHandler {void Execute(UIApplication application);string GetName();}
 public sealed class ExternalEvent:IDisposable {
  readonly IExternalEventHandler handler;bool disposed;
  internal static readonly ConcurrentQueue<Action> Queue=new ConcurrentQueue<Action>();
  private ExternalEvent(IExternalEventHandler value){handler=value;}
  public static ExternalEvent Create(IExternalEventHandler handler)=>new ExternalEvent(handler);
  public void Raise(){if(disposed)throw new ObjectDisposedException("ExternalEvent");Queue.Enqueue(()=>{if(!disposed)handler.Execute(new UIApplication());});}
  public void Dispose(){disposed=true;}
 }
 public enum TaskDialogCommonButtons {Close}
 public enum TaskDialogCommandLinkId {CommandLink1}
 public enum TaskDialogResult {Close,CommandLink1}
 public sealed class TaskDialog {
  public string MainInstruction {get;set;}="";public string MainContent {get;set;}="";
  public TaskDialogCommonButtons CommonButtons {get;set;}
  internal static int Shows,Errors;internal static string LastInstruction="";
  internal static TaskDialogResult Response=TaskDialogResult.Close;
  public TaskDialog(string title){}
  public void AddCommandLink(TaskDialogCommandLinkId id,string caption){}
  public TaskDialogResult Show(){Shows++;LastInstruction=MainInstruction;return Response;}
  public static void Show(string title,string message){Errors++;}
 }
}
