using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;
[assembly: AssemblyTitle("Revit Open Guard Setup")]
[assembly: AssemblyCompany("Mostafa Magdy")]
[assembly: AssemblyVersion("0.2.0.0")]
namespace RevitOpenGuard.Setup {
static class Program {
 internal static readonly int[] Years = {2022,2023,2024,2025,2026,2027};
 const string Key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RevitOpenGuard";
 internal static string Root { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Autodesk\Revit\Addins"); } }
 internal static string SetupFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"MoustafaMagdi\RevitOpenGuard\Setup"); } }
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  if(args.Contains("/selftest")) return Test();
  Application.Run(new SetupWindow(args.Contains("/uninstall"))); return 0;
 }
 internal static bool Detected(int year) {
  string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Autodesk","Revit "+year);
  if(File.Exists(Path.Combine(installed,"Revit.exe"))) return true;
  using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk\Revit\Autodesk Revit "+year)) return key!=null;
 }
 internal static void CheckClosed() {
  if(Process.GetProcessesByName("Revit").Length>0) throw new IOException("Close Revit before uninstalling. Installation can run with Revit open.");
 }
 internal static byte[] Read(ZipArchiveEntry entry) {
  if(entry==null) throw new IOException("Incomplete setup package.");
  using(var input=entry.Open()) using(var output=new MemoryStream()) { input.CopyTo(output); return output.ToArray(); }
 }
 internal static void Install(string root,int[] years,string setupFolder,bool register,Action<string> installed=null) {
  if(years.Length==0 || years.Any(y=>!Years.Contains(y))) throw new IOException("Select at least one supported Revit version.");
  var backups=new Dictionary<string,byte[]>(); var written=new List<string>();
  var manifests=new Dictionary<string,byte[]>();
  Action<string,byte[]> write=(path,bytes)=> {
   if(File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes)) { if(installed!=null) installed(path); return; }
   backups[path]=File.Exists(path)?File.ReadAllBytes(path):null;
   Directory.CreateDirectory(Path.GetDirectoryName(path)); written.Add(path); File.WriteAllBytes(path,bytes);
   if(!File.ReadAllBytes(path).SequenceEqual(bytes)) throw new IOException("File verification failed: "+path);
   if(installed!=null) installed(path);
  };
  try {
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload"))
   using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)) {
    byte[] template=Read(zip.GetEntry("RevitOpenGuard.addin"));
    foreach(int year in years.Distinct()) {
     string revitRoot=Path.Combine(root,year.ToString());
     byte[] dll=Read(zip.GetEntry(year+"/RevitOpenGuard.dll"));
     string hash;
     using(var sha=System.Security.Cryptography.SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(dll)).Replace("-","");
     string folder=Path.Combine(revitRoot,"RevitOpenGuard",hash);
     write(Path.Combine(folder,"RevitOpenGuard.dll"),dll);
     var deps=zip.GetEntry(year+"/RevitOpenGuard.deps.json");
     if(deps!=null) write(Path.Combine(folder,deps.Name),Read(deps));
     var manifest=new System.Xml.XmlDocument();
     using(var source=new MemoryStream(template)) manifest.Load(source);
     manifest.SelectSingleNode("/RevitAddIns/AddIn/Assembly").InnerText=Path.Combine(folder,"RevitOpenGuard.dll");
     using(var output=new MemoryStream()) { manifest.Save(output); manifests.Add(Path.Combine(revitRoot,"RevitOpenGuard.addin"),output.ToArray()); }
    }
   }
   string setup=Path.Combine(setupFolder,"RevitOpenGuardSetup.exe");
   if(!String.Equals(setup,Assembly.GetExecutingAssembly().Location,StringComparison.OrdinalIgnoreCase)) write(setup,File.ReadAllBytes(Assembly.GetExecutingAssembly().Location));
   if(register) using(var key=Registry.CurrentUser.CreateSubKey(Key)) {
    key.SetValue("DisplayName","Revit Open Guard (2022-2027)"); key.SetValue("DisplayVersion","0.2.0");
    key.SetValue("Publisher","Mostafa Magdy"); key.SetValue("InstallLocation",setupFolder); key.SetValue("DisplayIcon",setup);
    key.SetValue("UninstallString","\""+setup+"\" /uninstall");
    key.SetValue("NoModify",1,RegistryValueKind.DWord); key.SetValue("NoRepair",1,RegistryValueKind.DWord);
   }
   // The only .addin publications occur after every payload, dependency and uninstaller is ready.
   foreach(var manifest in manifests) {
    string path=manifest.Key; string pending=path+".pending-"+Guid.NewGuid().ToString("N");
    backups[path]=File.Exists(path)?File.ReadAllBytes(path):null;
    try {
     File.WriteAllBytes(pending,manifest.Value); written.Add(path);
     if(File.Exists(path)) File.Replace(pending,path,null); else File.Move(pending,path);
    } finally { if(File.Exists(pending)) File.Delete(pending); }
    if(installed!=null) installed(path);
   }
   if(register) Registry.CurrentUser.DeleteSubKeyTree(Key+"2024",false);
  } catch {
   foreach(string path in written.AsEnumerable().Reverse()) {
    try { if(backups[path]==null) File.Delete(path); else File.WriteAllBytes(path,backups[path]); } catch {}
   }
   throw;
  }
 }
 internal static void Uninstall(string root,int[] years,string setupFolder,bool register) {
  foreach(int year in years) {
   string revitRoot=Path.Combine(root,year.ToString()); string folder=Path.Combine(revitRoot,"RevitOpenGuard");
   // Remove the manifest first, then only files owned by this installer.
   File.Delete(Path.Combine(revitRoot,"RevitOpenGuard.addin"));
   if(!Directory.Exists(folder)) continue;
   foreach(string version in Directory.GetDirectories(folder)) {
    string name=Path.GetFileName(version);
    if(name.Length!=64 || !name.All(Uri.IsHexDigit)) continue;
    foreach(string file in new[]{"RevitOpenGuard.dll","RevitOpenGuard.deps.json"}) File.Delete(Path.Combine(version,file));
    if(!Directory.EnumerateFileSystemEntries(version).Any()) Directory.Delete(version);
   }
   foreach(string file in new[]{"RevitOpenGuard.dll","RevitOpenGuard.pdb","RevitOpenGuardSetup.exe"}) File.Delete(Path.Combine(folder,file));
   if(!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
  }
  if(register) { Registry.CurrentUser.DeleteSubKeyTree(Key,false); Registry.CurrentUser.DeleteSubKeyTree(Key+"2024",false); }
  string setup=Path.Combine(setupFolder,"RevitOpenGuardSetup.exe");
  if(!String.Equals(setup,Assembly.GetExecutingAssembly().Location,StringComparison.OrdinalIgnoreCase)) File.Delete(setup);
  if(Directory.Exists(setupFolder) && !Directory.EnumerateFileSystemEntries(setupFolder).Any()) Directory.Delete(setupFolder);
 }
 static int Test() {
  string test=Path.Combine(Path.GetTempPath(),"OpenGuardSetupTest-"+Guid.NewGuid().ToString("N"));
  string root=Path.Combine(test,"Addins"),setup=Path.Combine(test,"Setup");
  string report=Path.Combine(Path.GetTempPath(),"RevitOpenGuard-setup-test.txt");
  try {
   var order=new List<string>(); Install(root,Years,setup,false,order.Add);
   int first=order.FindIndex(p=>p.EndsWith(".addin"));
   if(first<0 || order.Skip(first).Any(p=>!p.EndsWith(".addin"))) throw new Exception("Manifest publication order failed.");
   foreach(int year in Years) {
    var xml=new System.Xml.XmlDocument(); xml.Load(Path.Combine(root,year.ToString(),"RevitOpenGuard.addin"));
    if(xml.SelectSingleNode("//FullClassName").InnerText!="RevitOpenGuard.App") throw new Exception("Wrong entry point.");
    string dll=xml.SelectSingleNode("//Assembly").InnerText;
    if(!Path.IsPathRooted(dll)||!File.Exists(dll)) throw new Exception("Invalid installed assembly.");
    var assembly=Assembly.ReflectionOnlyLoad(File.ReadAllBytes(dll));
    if(assembly.GetReferencedAssemblies().Single(a=>a.Name=="RevitAPI").Version.Major!=year-2000) throw new Exception("Wrong year payload: "+year);
    using(var locked=new FileStream(dll,FileMode.Open,FileAccess.Read,FileShare.Read)) Install(root,new[]{year},setup,false);
    string previous=Path.Combine(root,year.ToString(),"RevitOpenGuard",new string('A',64),"RevitOpenGuard.dll");
    Directory.CreateDirectory(Path.GetDirectoryName(previous)); File.WriteAllText(previous,"previous loaded version");
    xml.SelectSingleNode("//Assembly").InnerText=previous;
    xml.Save(Path.Combine(root,year.ToString(),"RevitOpenGuard.addin"));
    using(var locked=new FileStream(previous,FileMode.Open,FileAccess.Read,FileShare.Read)) Install(root,new[]{year},setup,false);
    xml.Load(Path.Combine(root,year.ToString(),"RevitOpenGuard.addin"));
    if(xml.SelectSingleNode("//Assembly").InnerText!=dll || !File.Exists(previous)) throw new Exception("Loaded previous version was not preserved during update.");
    File.WriteAllText(Path.Combine(root,year.ToString(),"RevitOpenGuard","keep.txt"),"preserve");
   }
   // A failure before publication must not expose a manifest or replace an earlier installation.
   string failureRoot=Path.Combine(test,"Failure");
   bool failed=false;
   try { Install(failureRoot,new[]{2024},setup,false,p=>{if(p.EndsWith(".dll")) throw new IOException("Simulated copy failure");}); }
   catch(IOException) { failed=true; }
   if(!failed || File.Exists(Path.Combine(failureRoot,"2024","RevitOpenGuard.addin"))) throw new Exception("Failed install published a manifest.");
   Uninstall(root,Years,setup,false);
   foreach(int year in Years) {
    string folder=Path.Combine(root,year.ToString(),"RevitOpenGuard");
    if(File.Exists(Path.Combine(root,year.ToString(),"RevitOpenGuard.addin")) || Directory.GetFiles(folder,"RevitOpenGuard.dll",SearchOption.AllDirectories).Any()) throw new Exception("Uninstall failed.");
    if(!File.Exists(Path.Combine(folder,"keep.txt"))) throw new Exception("Unrelated file removed.");
   }
   using(var window=new SetupWindow(false)) using(var preview=new Bitmap(window.Width,window.Height)) {
    window.ShowInTaskbar=false; window.StartPosition=FormStartPosition.Manual; window.Location=new Point(-20000,-20000);
    window.Show(); window.Update();
    window.DrawToBitmap(preview,new Rectangle(Point.Empty,window.Size));
    preview.Save(Path.Combine(Path.GetTempPath(),"RevitOpenGuard-setup-preview.png"));
    window.Close();
   }
   File.WriteAllText(report,"PASS: six year-specific API payloads, absolute manifests published last, locked-DLL reinstall and update, failure rollback, uninstall, unrelated file preservation."); return 0;
  } catch(Exception ex) { File.WriteAllText(report,ex.ToString()); return 1; }
  finally {
   string resolved=Path.GetFullPath(test),temp=Path.GetFullPath(Path.GetTempPath());
   if(resolved.StartsWith(temp,StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("OpenGuardSetupTest-")) Directory.Delete(resolved,true);
  }
 }
}
class SetupWindow:Form {
 Label status=new Label(); Button action=new Button(); CheckedListBox versions=new CheckedListBox(); bool remove;
 public SetupWindow(bool uninstall) {
  remove=uninstall; Text="MM Tools | Open Guard Setup"; ClientSize=new Size(590,510);
  FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
  Font=new Font("Segoe UI",10); BackColor=Color.White; AutoScaleMode=AutoScaleMode.Dpi;
  Controls.Add(new Label{Text="MM TOOLS",Font=new Font("Segoe UI",24,FontStyle.Bold),ForeColor=Color.FromArgb(20,65,103),Location=new Point(28,20),AutoSize=true});
  Controls.Add(new Label{Text="Open Guard  |  Revit 2022-2027",Font=new Font("Segoe UI",14),Location=new Point(30,75),AutoSize=true});
  status.Text=remove?"Remove Open Guard from all supported Revit versions.\nYour settings, error catalog and logs will be kept.\nClose Revit before uninstalling.":"Install for your Windows account. No administrator required.\nRevit can remain open. The .addin files are published last.\nSelect versions (detected installations are preselected):";
  status.Location=new Point(30,120); status.Size=new Size(530,100); Controls.Add(status);
  versions.Location=new Point(32,225); versions.Size=new Size(520,155); versions.CheckOnClick=true;
  foreach(int year in Program.Years) versions.Items.Add("Revit "+year+(Program.Detected(year)?"  (detected)":""),remove || Program.Detected(year));
  versions.Enabled=!remove; Controls.Add(versions);
  Controls.Add(new Label{Text="First installation starts OFF. Updates to an already loaded add-in\ntake effect next Revit session. Catalog rules update separately.",Location=new Point(30,385),Size=new Size(530,48)});
  action.Text=remove?"Uninstall":"Install"; action.Location=new Point(365,450); action.Size=new Size(185,40); action.BackColor=Color.FromArgb(20,65,103); action.ForeColor=Color.White; action.Click+=Run; Controls.Add(action);
 }
 void Run(object sender,EventArgs e) {
  action.Enabled=false; UseWaitCursor=true;
  try {
   if(remove) { Program.CheckClosed(); Program.Uninstall(Program.Root,Program.Years,Program.SetupFolder,true); }
   else {
    int[] years=versions.CheckedIndices.Cast<int>().Select(i=>Program.Years[i]).ToArray();
    Program.Install(Program.Root,years,Program.SetupFolder,true);
   }
   status.Text=remove?"Open Guard removed.\nSettings, error catalog and logs were kept.":"Installed successfully.\nMM Tools > Model Opening > Open Guard\nError Catalog Updates checks for new recovery rules.";
   action.Text="Close"; action.Click-=Run; action.Click+=delegate{Close();};
  } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Setup could not finish",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
  finally {action.Enabled=true; UseWaitCursor=false;}
 }
}
}
