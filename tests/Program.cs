using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RevitOpenGuard.Infrastructure;
using RevitOpenGuard.Policies;
class Program
{
 static int checks;
 static void Assert(bool condition,string name) { if(!condition) throw new Exception(name); checks++; }
 static void Reject(Action action,string name) { try { action(); } catch { checks++; return; } throw new Exception("Accepted invalid input: "+name); }
 static byte[] Serialize<T>(T data) { using(var stream=new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(stream,data); return stream.ToArray(); } }
 static byte[] Sign(Catalog catalog,string key) {
  byte[] payload=Serialize(catalog);
  using(var rsa=new RSACryptoServiceProvider()) {
   rsa.PersistKeyInCsp=false; rsa.FromXmlString(key);
   return Serialize(new SignedCatalog{Payload=Convert.ToBase64String(payload),Signature=Convert.ToBase64String(rsa.SignData(payload,"SHA256"))});
  }
 }
 sealed class Fake:HttpMessageHandler {
  readonly byte[] bytes; readonly HttpStatusCode status;
  internal Fake(byte[] data,HttpStatusCode code=HttpStatusCode.OK){bytes=data;status=code;}
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
   return Task.FromResult(new HttpResponseMessage(status){Content=new ByteArrayContent(bytes)});
  }
 }
 static int Main(string[] args) {
  if(args.Length==2 && args[0]=="--verify") {
   try { var catalog=CatalogStore.Verify(File.ReadAllBytes(args[1])); Console.WriteLine("Verified catalog "+catalog.Version); return 0; }
   catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
  }
  string temp=Path.Combine(Path.GetTempPath(),"OpenGuardCatalogTest-"+Guid.NewGuid().ToString("N"));
  try {
   CatalogStore.StorageDirectory=temp;
   string root=Path.GetFullPath(args[0]);
   string key=File.ReadAllText(args.Length > 1 ? args[1] : Path.Combine(root,"artifacts","keys","catalog-private.xml"));
   byte[] release=File.ReadAllBytes(Path.Combine(root,"catalog","catalog.signed.json"));
   Catalog baseline=CatalogStore.Verify(release);
   Assert(baseline.Rules.Count>0,"Signed baseline rules");
   Assert(CatalogStore.Current.Version==baseline.Version,"Offline embedded fallback");
   var known = baseline.Rules.Single(r=>r.Id=="Text.PerpendicularDimension");
   foreach(int year in Enumerable.Range(2022,6)) Assert(known.Matches(year,"Dimension is perpendicular to the current view and no longer valid"),"Year match "+year);
   Assert(!known.Matches(2024,"dimension is perpendicular to the current view"),"All text conditions required");
   Assert(!baseline.Rules[0].Matches(2021,""),"Year lower bound");
   Assert(!baseline.Rules[0].Matches(2028,""),"Year upper bound");
   var env=Catalog.Read<SignedCatalog>(release); env.Payload=Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"));
   Reject(()=>CatalogStore.Verify(Serialize(env)),"Tampered payload");
   env=Catalog.Read<SignedCatalog>(release); env.Signature=Convert.ToBase64String(new byte[384]);
   Reject(()=>CatalogStore.Verify(Serialize(env)),"Tampered signature");
   Reject(()=>CatalogStore.Verify(new byte[1024*1024+1]),"Oversized catalog");
   var next=Catalog.Read<Catalog>(Serialize(baseline));
   string firstVersion=new Version(new Version(baseline.Version).Major+1,0,0).ToString();
   next.Version=firstVersion; next.Notes="Test update";
   byte[] update=Sign(next,key);
   byte[]? pending=CatalogStore.CheckAsync(new Fake(update)).GetAwaiter().GetResult();
   Assert(pending!=null,"Detect newer signed catalog");
   Assert(CatalogStore.Current.Version==baseline.Version,"Check never applies without user action");
   CatalogStore.Apply(update);
   Assert(CatalogStore.Current.Version==firstVersion,"Apply new catalog");
   Assert(CatalogStore.CheckAsync(new Fake(update)).GetAwaiter().GetResult()==null,"Do not notify same version");
   Reject(()=>CatalogStore.Apply(release),"Downgrade");
   var invalid=Catalog.Read<Catalog>(Serialize(next)); invalid.Rules[0].Action="RunCode";
   Reject(()=>CatalogStore.Verify(Sign(invalid,key)),"Unknown action even if signed");
   invalid=Catalog.Read<Catalog>(Serialize(next)); invalid.SchemaVersion=99;
   Reject(()=>CatalogStore.Verify(Sign(invalid,key)),"Unknown schema");
   invalid=Catalog.Read<Catalog>(Serialize(next)); invalid.MinToolVersion="99.0.0";
   Reject(()=>CatalogStore.Verify(Sign(invalid,key)),"Requires future tool version");
   invalid=Catalog.Read<Catalog>(Serialize(next)); invalid.Rules.Add(invalid.Rules[0]);
   Reject(()=>CatalogStore.Verify(Sign(invalid,key)),"Duplicate rules");
   invalid=Catalog.Read<Catalog>(Serialize(next)); invalid.Rules[0].FailureId=null;
   Reject(()=>CatalogStore.Verify(Sign(invalid,key)),"Unbounded matcher");
   Reject(()=>CatalogStore.CheckAsync(new Fake(release,HttpStatusCode.ServiceUnavailable)).GetAwaiter().GetResult(),"HTTP failure");
   Assert(CatalogStore.Current.Version==firstVersion,"Network failure preserves active rules");
   next.Version=new Version(new Version(baseline.Version).Major+1,0,1).ToString(); CatalogStore.Apply(Sign(next,key));
   File.WriteAllText(Path.Combine(temp,"catalog.signed.json"),"corrupt");
   CatalogStore.RefreshBetweenOpenings();
   Assert(CatalogStore.Current.Version==firstVersion,"Corrupt cache falls back to signed backup");
   File.WriteAllText(Path.Combine(temp,"catalog.signed.json.bak"),"corrupt");
   CatalogStore.RefreshBetweenOpenings();
   Assert(CatalogStore.Current.Version==baseline.Version,"Corrupt cache and backup fall back to embedded baseline");
   if(args.Contains("--online")) {
    CatalogStore.CheckAsync().GetAwaiter().GetResult();
    Assert(File.Exists(Path.Combine(temp,"catalog-check.utc")),"Live HTTPS feed downloaded and verified");
   }
   Console.WriteLine("PASS: "+checks+" catalog checks on "+System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);return 0;
  } catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
  finally { if(Directory.Exists(temp)) Directory.Delete(temp,true); }
 }
}
