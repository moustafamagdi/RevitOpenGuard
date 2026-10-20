using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitOpenGuard.Infrastructure;
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
        public static string ReadMessage(DialogBoxShowingEventArgs args)
        {
            if (args is TaskDialogShowingEventArgs task) return task.Message ?? "";
            if (args is MessageBoxShowingEventArgs box) return box.Message ?? "";
            return "";
        }
        public static DialogPolicy? Match(DialogBoxShowingEventArgs args, string message)
        {
            bool task = args is TaskDialogShowingEventArgs;
            if (!task && !(args is MessageBoxShowingEventArgs)) return null;
            foreach (var rule in CatalogStore.Current.Rules)
            {
                if (rule.Kind != "Dialog" || !rule.Matches(CatalogStore.RevitYear, message)) continue;
                if (!string.IsNullOrEmpty(rule.DialogId) && !string.Equals(rule.DialogId, args.DialogId, StringComparison.Ordinal)) continue;
                if (rule.DialogKind != "Any" && rule.DialogKind != (task ? "TaskDialog" : "MessageBox")) continue;
                var result = (TaskDialogResult)Enum.Parse(typeof(TaskDialogResult), rule.Action);
                if (args is MessageBoxShowingEventArgs box)
                {
                    int buttons = box.DialogType & 0x0F;
                    bool available = result == TaskDialogResult.Ok ? buttons == 0 || buttons == 1
                        : result == TaskDialogResult.Yes && (buttons == 3 || buttons == 4);
                    if (!available) continue;
                }
                return new DialogPolicy(rule.Id, (int)result);
            }
            return null;
        }
    }
}
