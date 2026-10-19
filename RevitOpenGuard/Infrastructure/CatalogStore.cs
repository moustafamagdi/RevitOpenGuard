using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RevitOpenGuard.Policies;

namespace RevitOpenGuard.Infrastructure
{
    internal static class CatalogStore
    {
        internal const string FeedUrl = "https://raw.githubusercontent.com/moustafamagdi/RevitOpenGuard/catalog-updates/catalog.signed.json";
        private const int MaxBytes = 1024 * 1024;
        private static readonly object Gate = new object();
        internal static string StorageDirectory = AuditLog.RootDirectory;
        private static string Cache => Path.Combine(StorageDirectory, "catalog.signed.json");
        private static string LastCheck => Path.Combine(StorageDirectory, "catalog-check.utc");
        private static Catalog? _current;
        internal static int RevitYear { get; set; } = 2024;
        internal static Catalog Current { get { lock (Gate) return _current ?? (_current = Load()); } }
        internal static bool CheckDue
        {
            get
            {
                try { return !File.Exists(LastCheck) || DateTime.UtcNow - File.GetLastWriteTimeUtc(LastCheck) > TimeSpan.FromDays(1); }
                catch { return true; }
            }
        }

        internal static byte[] Resource(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new InvalidDataException("Missing resource: " + name))
            using (var memory = new MemoryStream()) { stream.CopyTo(memory); return memory.ToArray(); }
        }

        private static Catalog Load()
        {
            var baseline = Catalog.Read<Catalog>(Resource("OpenGuard.Catalog")); baseline.Validate();
            foreach (string path in new[] { Cache, Cache + ".bak" })
            {
                try
                {
                    if (!File.Exists(path) || new FileInfo(path).Length > MaxBytes) continue;
                    var candidate = Verify(File.ReadAllBytes(path));
                    if (new Version(candidate.Version) >= new Version(baseline.Version)) return candidate;
                }
                catch { /* An invalid cache must never prevent offline startup. */ }
            }
            return baseline;
        }

        internal static Catalog Verify(byte[] envelopeBytes)
        {
            if (envelopeBytes.Length > MaxBytes) throw new InvalidDataException("Catalog exceeds the size limit.");
            var envelope = Catalog.Read<SignedCatalog>(envelopeBytes);
            byte[] payload = Convert.FromBase64String(envelope.Payload);
            byte[] signature = Convert.FromBase64String(envelope.Signature);
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(System.Text.Encoding.UTF8.GetString(Resource("OpenGuard.PublicKey")));
                if (!rsa.VerifyData(payload, "SHA256", signature))
                    throw new InvalidDataException("Catalog signature is invalid.");
            }
            var catalog = Catalog.Read<Catalog>(payload); catalog.Validate(); return catalog;
        }

        internal static async Task<byte[]?> CheckAsync(HttpMessageHandler? transport = null)
        {
            // Never call the Revit API from this background operation.
            using (var handler = transport ?? new HttpClientHandler { AllowAutoRedirect = false })
            using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) })
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            using (var response = await client.GetAsync(FeedUrl, HttpCompletionOption.ResponseHeadersRead, cancellation.Token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidDataException("Catalog exceeds the size limit.");
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var memory = new MemoryStream())
                {
                    var buffer = new byte[8192]; int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellation.Token).ConfigureAwait(false)) > 0)
                    {
                        if (memory.Length + read > MaxBytes) throw new InvalidDataException("Catalog exceeds the size limit.");
                        memory.Write(buffer, 0, read);
                    }
                    byte[] bytes = memory.ToArray(); var candidate = Verify(bytes);
                    Directory.CreateDirectory(StorageDirectory);
                    File.WriteAllText(LastCheck, DateTime.UtcNow.ToString("O"));
                    return new Version(candidate.Version) > new Version(Current.Version) ? bytes : null;
                }
            }
        }

        internal static void Apply(byte[] bytes)
        {
            var catalog = Verify(bytes);
            string mutexName;
            using (var hash = SHA256.Create())
                mutexName = "Local\\OpenGuardCatalog-" + BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(StorageDirectory))).Replace("-", "");
            using (var mutex = new Mutex(false, mutexName))
            {
                bool acquired;
                try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) throw new IOException("Another Revit instance is updating the catalog. Try again shortly.");
                try { ApplyVerified(bytes, catalog); }
                finally { mutex.ReleaseMutex(); }
            }
        }

        private static void ApplyVerified(byte[] bytes, Catalog catalog)
        {
            lock (Gate)
            {
                // Another Revit process may have applied a more recent catalog in the meantime.
                var disk = Load();
                if (new Version(catalog.Version) < new Version(disk.Version) || new Version(catalog.Version) < new Version(Current.Version))
                    throw new InvalidDataException("An older catalog cannot replace a newer one.");
                Directory.CreateDirectory(StorageDirectory);
                string temporary = Cache + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                    if (File.Exists(Cache)) File.Replace(temporary, Cache, Cache + ".bak", true);
                    else File.Move(temporary, Cache);
                    _current = catalog;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        internal static void RefreshBetweenOpenings() { lock (Gate) _current = Load(); }
    }
}
