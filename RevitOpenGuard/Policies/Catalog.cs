using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RevitOpenGuard.Policies
{
    [DataContract]
    internal sealed class Catalog
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "version")] public string Version { get; set; } = "";
        [DataMember(Name = "minToolVersion")] public string MinToolVersion { get; set; } = "";
        [DataMember(Name = "notes")] public string Notes { get; set; } = "";
        [DataMember(Name = "rules")] public List<CatalogRule> Rules { get; set; } = new List<CatalogRule>();

        public static T Read<T>(byte[] bytes)
        {
            // JSON files authored by Windows PowerShell may include a UTF-8 BOM.
            int offset = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0;
            using (var stream = new MemoryStream(bytes, offset, bytes.Length - offset))
                return (T)(new DataContractJsonSerializer(typeof(T)).ReadObject(stream) ?? throw new InvalidDataException("Empty JSON document."));
        }

        public void Validate()
        {
            if (SchemaVersion != 1 || !System.Version.TryParse(Version, out _)
                || !System.Version.TryParse(MinToolVersion, out var minimum) || minimum > new System.Version("0.2.0"))
                throw new InvalidDataException("This catalog requires a different Open Guard version.");
            if (Notes == null || Notes.Length > 4000 || Rules == null || Rules.Count == 0 || Rules.Count > 2000)
                throw new InvalidDataException("Invalid catalog size.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in Rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || rule.Id.Length > 160 || !ids.Add(rule.Id))
                    throw new InvalidDataException("Rule IDs must be unique and non-empty.");
                rule.Validate();
            }
        }
    }

    [DataContract]
    internal sealed class CatalogRule
    {
        [DataMember(Name = "id")] public string Id { get; set; } = "";
        [DataMember(Name = "kind")] public string Kind { get; set; } = "";
        [DataMember(Name = "failureId")] public string? FailureId { get; set; }
        [DataMember(Name = "dialogId")] public string? DialogId { get; set; }
        [DataMember(Name = "dialogKind")] public string? DialogKind { get; set; }
        [DataMember(Name = "allText")] public string[]? AllText { get; set; }
        [DataMember(Name = "anyText")] public string[]? AnyText { get; set; }
        [DataMember(Name = "action")] public string Action { get; set; } = "";
        [DataMember(Name = "minRevit")] public int MinRevit { get; set; }
        [DataMember(Name = "maxRevit")] public int MaxRevit { get; set; }

        public void Validate()
        {
            if (MinRevit < 2022 || MaxRevit > 2027 || MinRevit > MaxRevit)
                throw new InvalidDataException("Invalid Revit range: " + Id);
            bool text = (AllText?.Length ?? 0) > 0 || (AnyText?.Length ?? 0) > 0;
            foreach (var list in new[] { AllText, AnyText })
                if (list != null && (list.Length > 40 || list.Any(s => string.IsNullOrWhiteSpace(s) || s.Trim().Length < 5 || s.Length > 1000)))
                    throw new InvalidDataException("Invalid text matcher: " + Id);
            if (Kind == "Failure")
            {
                if (!string.IsNullOrEmpty(DialogId) || !string.IsNullOrEmpty(DialogKind)
                    || (Action != "DetachElements" && Action != "DeleteElements") || (!text && string.IsNullOrEmpty(FailureId)))
                    throw new InvalidDataException("Invalid failure rule: " + Id);
                if (!string.IsNullOrEmpty(FailureId) && !Guid.TryParse(FailureId, out _)
                    && !new[] { "BuiltIn:LinearConstraintNotParallel", "BuiltIn:RadialDimensionCannotProjectToArc", "BuiltIn:DimensionPerpendicularToView" }.Contains(FailureId))
                    throw new InvalidDataException("Unknown failure identifier: " + Id);
            }
            else if (Kind == "Dialog")
            {
                if (!string.IsNullOrEmpty(FailureId) || (DialogKind != "Any" && DialogKind != "TaskDialog" && DialogKind != "MessageBox")
                    || !new[] { "Ok", "Yes", "Close", "CommandLink1" }.Contains(Action)
                    || (!text && string.IsNullOrWhiteSpace(DialogId))
                    || ((Action == "CommandLink1" || Action == "Close") && DialogKind != "TaskDialog"))
                    throw new InvalidDataException("Invalid dialog rule: " + Id);
            }
            else throw new InvalidDataException("Unknown rule kind: " + Id);
        }

        public bool Matches(int year, string message)
        {
            if (year < MinRevit || year > MaxRevit) return false;
            string text = Normalize(message);
            return (AllText == null || AllText.All(s => text.Contains(Normalize(s))))
                && (AnyText == null || AnyText.Length == 0 || AnyText.Any(s => text.Contains(Normalize(s))));
        }

        internal static string Normalize(string? value) => System.Text.RegularExpressions.Regex.Replace(
            (value ?? "").Replace('\u2019', '\'').Replace('\u2018', '\'').Trim().ToLowerInvariant(), @"\s+", " ");
    }

    [DataContract]
    internal sealed class SignedCatalog
    {
        [DataMember(Name = "payload")] public string Payload { get; set; } = "";
        [DataMember(Name = "signature")] public string Signature { get; set; } = "";
    }
}
