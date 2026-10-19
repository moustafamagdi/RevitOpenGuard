using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RevitOpenGuard.Infrastructure
{
    internal sealed class SettingsStore
    {
        private readonly AuditLog _log;
        private bool _unsupportedVersion;
        public string FilePath { get; } = Path.Combine(AuditLog.RootDirectory, "settings.xml");
        public SettingsStore(AuditLog log) { _log = log; }

        public bool LoadEnabled()
        {
            if (!File.Exists(FilePath)) return false;
            try
            {
                var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(FilePath, readerSettings))
                {
                    XElement? root = XDocument.Load(reader).Root;
                    if (root == null || root.Name != "RevitOpenGuard") throw new InvalidDataException("Unrecognized settings document.");
                    if ((string?)root.Attribute("version") != "1")
                    {
                        _unsupportedVersion = true;
                        throw new InvalidDataException("Unsupported settings version. File will not be overwritten.");
                    }
                    if (!bool.TryParse((string?)root.Element("Enabled"), out bool enabled))
                        throw new InvalidDataException("Enabled must be true or false.");
                    return enabled;
                }
            }
            catch (Exception ex)
            {
                _log.Write("WARN", "SettingsRead", "Starting OFF because saved state could not be read.", error: ex);
                return false;
            }
        }

        public bool TrySave(bool enabled, out string error)
        {
            string? temporary = null;
            error = string.Empty;
            try
            {
                if (_unsupportedVersion) throw new InvalidDataException("Settings belong to a different version; original file left unchanged.");
                Directory.CreateDirectory(AuditLog.RootDirectory);
                temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                string xml = new XDocument(new XElement("RevitOpenGuard", new XAttribute("version", "1"),
                    new XElement("Enabled", enabled))).ToString();
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(xml);
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak", true);
                else File.Move(temporary, FilePath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _log.Write("WARN", "SettingsWrite", "Session state changed but could not be remembered.", error: ex);
                return false;
            }
            finally
            {
                try { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
        }
    }
}
