using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitOpenGuard.Runtime
{
    internal sealed class OpeningScope
    {
        public string Id { get; } = Guid.NewGuid().ToString("N").Substring(0, 12);
        public string Path { get; }
        public bool IsProject { get; }
        public DateTime StartedUtc { get; } = DateTime.UtcNow;
        public HashSet<Document> PreviouslyOpen { get; }
        public HashSet<string> AttemptedRepairs { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> Observations { get; } = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _dialogAttempts = new Dictionary<string, int>(StringComparer.Ordinal);
        public int DialogsHandled { get; set; }
        public int RepairsApplied { get; set; }

        public OpeningScope(string path, bool isProject, IEnumerable<Document> previouslyOpen)
        {
            Path = path;
            IsProject = isProject;
            PreviouslyOpen = new HashSet<Document>(previouslyOpen);
        }

        public bool AllowDialogAttempt(string signature)
        {
            _dialogAttempts.TryGetValue(signature, out int count);
            _dialogAttempts[signature] = count + 1;
            // A finite repetition ceiling, not a timeout on opening a large model.
            return count < 100;
        }
    }
}
