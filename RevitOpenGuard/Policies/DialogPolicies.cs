using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace RevitOpenGuard.Policies
{
    internal sealed class DialogPolicy
    {
        public string Id { get; }
        public int Result { get; }
        public DialogPolicy(string id, int result) { Id = id; Result = result; }
    }

    internal static class DialogPolicies
    {
        // Add an exact ID only after checking both its button/result and target Revit version.
        // No invented Revit DialogIds: the inherited dialog rules are text signatures for now.
        private static readonly Dictionary<string, DialogPolicy> ById = new Dictionary<string, DialogPolicy>(StringComparer.Ordinal);

        public static string ReadMessage(DialogBoxShowingEventArgs args)
        {
            if (args is TaskDialogShowingEventArgs task) return task.Message ?? string.Empty;
            if (args is MessageBoxShowingEventArgs box) return box.Message ?? string.Empty;
            return string.Empty;
        }

        public static DialogPolicy? Match(DialogBoxShowingEventArgs args, string message)
        {
            if (!string.IsNullOrWhiteSpace(args.DialogId) && ById.TryGetValue(args.DialogId, out DialogPolicy exact))
                return exact;
            // Native custom dialogs with no message text cannot be classified by these rules.
            bool task = args is TaskDialogShowingEventArgs;
            if (!task && !(args is MessageBoxShowingEventArgs)) return null;
            string text = Normalize(message);
            if (text.Contains("import detected no valid elements in the file's paper space") && text.Contains("import from the model space"))
                return Standard(args, "CAD.EmptyPaperSpace", TaskDialogResult.Yes);
            if (text.Contains("some numerical data within the imported file was out of range") && text.Contains("this numerical data has been truncated"))
                return Standard(args, "CAD.TruncatedNumericalData", task ? TaskDialogResult.Close : TaskDialogResult.Ok);
            if (ContainsAny(text, "far from the origin", "large coordinate", "extents are greater", "geometry extents", "imported geometry", "slightly off axis"))
                return Standard(args, "CAD.GeometryWarning", TaskDialogResult.Ok);
            if (task && ContainsAny(text, "missing reference", "references are missing", "reference is missing", "lost references",
                "invalid references", "dimension references", "tag references", "references to elements have been lost"))
                return new DialogPolicy("Annotation.ReferenceRecoveryCommand", (int)TaskDialogResult.CommandLink1);
            if (task && ContainsAny(text, "cannot be displayed", "could not be loaded", "failed to load", "some elements were deleted"))
                return Standard(args, "Opening.LoadDisplayNotice", TaskDialogResult.Ok);
            return null;
        }

        private static DialogPolicy? Standard(DialogBoxShowingEventArgs args, string id, TaskDialogResult result)
        {
            if (args is MessageBoxShowingEventArgs box)
            {
                // Low nibble is the Win32 MB_* button layout, not its icon. Never return an
                // arbitrary nonzero result to a standard message box without the right button.
                int buttons = box.DialogType & 0x0F;
                bool available = result == TaskDialogResult.Ok ? buttons == 0 || buttons == 1
                    : result == TaskDialogResult.Yes && (buttons == 3 || buttons == 4);
                if (!available) return null;
            }
            return new DialogPolicy(id, (int)result);
        }

        internal static string Normalize(string? value) => Regex.Replace((value ?? string.Empty).Replace('\u2019', '\'').Replace('\u2018', '\'').Trim().ToLowerInvariant(), @"\s+", " ");

        internal static bool ContainsAny(string text, params string[] needles)
        {
            foreach (string needle in needles) if (text.IndexOf(needle, StringComparison.Ordinal) >= 0) return true;
            return false;
        }
    }
}
