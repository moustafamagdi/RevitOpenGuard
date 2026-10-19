using System;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using RevitOpenGuard.Infrastructure;

namespace RevitOpenGuard
{
    internal static class CatalogUpdateNotice
    {
        private static Task<byte[]?>? _check;
        private static bool _started;
        private static bool _stopped;
        private static DateTime _lastAttempt = DateTime.MinValue;
        private static ExternalEvent? _notification;
        private static AuditLog? _log;
        private static Func<Task<byte[]?>>? _fetch;
        internal static bool WindowOpen;

        internal static void Trace(string details) => _log?.Write("INFO", "CatalogCheck", details);

        private static void BeginCheck()
        {
            _lastAttempt = DateTime.UtcNow;
            Trace("Automatic check started.");
            _check = Task.Run(_fetch!);
            // Explicitly wake the Revit UI after the network task finishes. Do not show
            // dialogs or access model state from the background thread.
            _ = _check.ContinueWith(task =>
            {
                if (task.IsFaulted) _ = task.Exception;
                if (ReferenceEquals(_check, task)) RequestNotification();
            }, TaskScheduler.Default);
        }

        internal static void Start(AuditLog log, Func<Task<byte[]?>>? fetch = null)
        {
            if (_started) return;
            _started = true; _stopped = false; _log = log;
            _fetch = fetch ?? (() => CatalogStore.CheckAsync(trace: details => Trace("Automatic; " + details)));
            try { _notification = ExternalEvent.Create(new NotificationHandler()); }
            catch (Exception ex) { _log.Write("WARN", "CatalogCheck", "UI notification signal unavailable; retaining Idling fallback.", error: ex); }
            BeginCheck();
        }

        internal static void RequestNotification()
        {
            if (_stopped || _check == null || !_check.IsCompleted) return;
            try { _notification?.Raise(); }
            catch (Exception ex) { _log?.Write("WARN", "CatalogCheck", "UI signal failed; retaining Idling fallback.", error: ex); }
        }

        internal static void OnIdle()
        {
            if (_stopped || WindowOpen || App.Service?.IsOpening == true) return;
            if (_check == null)
            {
                if (_started && DateTime.UtcNow - _lastAttempt > TimeSpan.FromDays(1) && CatalogStore.CheckDue) BeginCheck();
                return;
            }
            if (!_check.IsCompleted) return;
            var result = _check; _check = null;
            if (result.IsFaulted)
            {
                _log?.Write("WARN", "CatalogCheck", "Using the saved offline catalog.", error: result.Exception);
                return;
            }
            if (result.IsCanceled || result.Result == null)
            {
                Trace("No update notification required."); return;
            }
            try
            {
                byte[] bytes = result.Result;
                var candidate = CatalogStore.Verify(bytes);
                if (new Version(candidate.Version) <= new Version(CatalogStore.Current.Version)) return;
                Trace("Showing update notification; Remote=" + candidate.Version + "; Local=" + CatalogStore.Current.Version);
                var dialog = new TaskDialog("Open Guard - Error catalog update")
                {
                    MainInstruction = "A new error catalog is available: " + candidate.Version,
                    MainContent = candidate.Notes,
                    CommonButtons = TaskDialogCommonButtons.Close
                };
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Update error catalog");
                if (dialog.Show() == TaskDialogResult.CommandLink1)
                {
                    CatalogStore.Apply(bytes);
                    Trace("Catalog applied by user; Version=" + CatalogStore.Current.Version);
                }
                else Trace("Catalog update deferred by user; Version=" + candidate.Version);
            }
            catch (Exception ex)
            {
                _log?.Write("WARN", "CatalogCheck", "Could not offer or apply the catalog update; existing rules retained.", error: ex);
                TaskDialog.Show("Open Guard", "Catalog could not be updated. The previous catalog is still active.\n" + ex.Message);
            }
        }

        internal static void Stop()
        {
            _stopped = true; _started = false; _check = null; _fetch = null;
            _notification?.Dispose(); _notification = null;
        }

        private sealed class NotificationHandler : IExternalEventHandler
        {
            public void Execute(UIApplication application) => OnIdle();
            public string GetName() => "Open Guard catalog update notification";
        }
    }
}
