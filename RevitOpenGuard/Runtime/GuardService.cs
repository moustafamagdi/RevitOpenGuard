using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitOpenGuard.Infrastructure;
using RevitOpenGuard.Policies;

namespace RevitOpenGuard.Runtime
{
    internal sealed class GuardService : IDisposable
    {
        private readonly AuditLog _log = new AuditLog();
        private readonly SettingsStore _settings;
        private readonly Stack<OpeningScope> _openings = new Stack<OpeningScope>();
        private readonly HashSet<Document> _knownDocuments = new HashSet<Document>();
        private Action? _unsubscribe;
        private UIApplication? _uiApplication;
        private bool _handlingFailures;
        private bool _handlingDialog;
        public bool Enabled { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsOpening => _openings.Count > 0;
        public event EventHandler? StateChanged;

        public GuardService()
        {
            _settings = new SettingsStore(_log);
            Enabled = _settings.LoadEnabled();
        }

        public void Attach(UIControlledApplication application)
        {
            var database = application.ControlledApplication;
            _unsubscribe = () =>
            {
                database.DocumentOpening -= OnDocumentOpening;
                database.DocumentOpened -= OnDocumentOpened;
                database.FailuresProcessing -= OnFailuresProcessing;
                application.DialogBoxShowing -= OnDialogBoxShowing;
                application.Idling -= OnIdling;
            };
            database.DocumentOpening += OnDocumentOpening;
            database.DocumentOpened += OnDocumentOpened;
            database.FailuresProcessing += OnFailuresProcessing;
            application.DialogBoxShowing += OnDialogBoxShowing;
            application.Idling += OnIdling;
            LogStartup(database.VersionNumber, database.VersionBuild);
        }

        // Supports command-only loading for a development session. The installed Application
        // manifest is still required to restore the service automatically on Revit startup.
        public void Attach(UIApplication application)
        {
            _uiApplication = application;
            var database = application.Application;
            _unsubscribe = () =>
            {
                database.DocumentOpening -= OnDocumentOpening;
                database.DocumentOpened -= OnDocumentOpened;
                database.FailuresProcessing -= OnFailuresProcessing;
                application.DialogBoxShowing -= OnDialogBoxShowing;
                application.Idling -= OnIdling;
            };
            database.DocumentOpening += OnDocumentOpening;
            database.DocumentOpened += OnDocumentOpened;
            database.FailuresProcessing += OnFailuresProcessing;
            application.DialogBoxShowing += OnDialogBoxShowing;
            application.Idling += OnIdling;
            RefreshKnownDocuments();
            LogStartup(database.VersionNumber, database.VersionBuild);
        }

        private void LogStartup(string version, string build)
        {
            CatalogStore.RevitYear = int.Parse(version, CultureInfo.InvariantCulture);
            _log.Write("INFO", "Startup", "Version=0.2.0; Revit=" + version + "; Build=" + build + "; Enabled=" + Enabled
                + "; Catalog=" + CatalogStore.Current.Version + "; Settings=" + _settings.FilePath + "; Log=" + _log.CurrentPath);
            CatalogUpdateNotice.Start();
        }

        public bool Toggle(out string persistenceError)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(GuardService));
            Enabled = !Enabled;
            if (!Enabled) ResetOpenings("User disabled Open Guard");
            bool saved = _settings.TrySave(Enabled, out persistenceError);
            _log.Write("INFO", "Toggle", "Enabled=" + Enabled + "; Remembered=" + saved);
            try { StateChanged?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { _log.Write("WARN", "Ribbon", "State changed, but the ribbon could not refresh.", error: ex); }
            return saved;
        }

        private OpeningScope? ActiveScope => !IsDisposed && Enabled && _openings.Count > 0 && _openings.Peek().IsProject
            ? _openings.Peek() : null;

        private void OnDocumentOpening(object? sender, DocumentOpeningEventArgs args)
        {
            if (IsDisposed || !Enabled) return;
            try
            {
                RefreshKnownDocuments();
                if (_openings.Count == 0) CatalogStore.RefreshBetweenOpenings();
                // Also push non-project openings, to suspend a parent scope while a family opens.
                var scope = new OpeningScope(args.PathName ?? string.Empty,
                    args.DocumentType == DocumentType.Project, _knownDocuments);
                _openings.Push(scope);
                _log.Write("INFO", "OpeningStarted", "DocumentType=" + args.DocumentType
                    + "; Catalog=" + CatalogStore.Current.Version + "; Protected=" + scope.IsProject, scope.Id, scope.Path);
            }
            catch (Exception ex)
            {
                ResetOpenings("Opening scope could not be established");
                _log.Write("ERROR", "OpeningStarted", "No automation without a valid opening scope.", error: ex);
            }
        }

        private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs args)
        {
            if (IsDisposed) return;
            // Pop before any post-open work: normal editing must not inherit the opening scope.
            OpeningScope? scope = _openings.Count > 0 ? _openings.Pop() : null;
            try
            {
                Document? document = args.Document;
                if (document != null && document.IsValidObject) _knownDocuments.Add(document);
                if (scope != null)
                {
                    _log.Write("INFO", "OpeningFinished", "Status=" + args.Status
                        + "; DialogsHandled=" + scope.DialogsHandled + "; RepairRequests=" + scope.RepairsApplied
                        + "; Seconds=" + (DateTime.UtcNow - scope.StartedUtc).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture),
                        scope.Id, scope.Path);
                }
            }
            catch (Exception ex)
            {
                _log.Write("WARN", "OpeningFinished", "Opening scope closed; completion metadata unavailable.",
                    scope?.Id ?? "", scope?.Path ?? "", ex);
            }
        }

        private void OnIdling(object? sender, IdlingEventArgs args)
        {
            if (IsDisposed) return;
            if (sender is UIApplication application) _uiApplication = application;
            // No polling, no SetRaiseWithoutDelay, no arbitrary timeouts on long model opens.
            // Revit's normal Idling event is the fallback if an abnormal open missed its post event.
            if (_openings.Count > 0) ResetOpenings("Revit returned to interactive idle without a matching post-open event");
            CatalogUpdateNotice.OnIdle();
        }

        private void RefreshKnownDocuments()
        {
            if (_uiApplication == null) return;
            var snapshot = new HashSet<Document>();
            foreach (Document document in _uiApplication.Application.Documents)
                if (document.IsValidObject) snapshot.Add(document);
            _knownDocuments.Clear();
            foreach (Document document in snapshot) _knownDocuments.Add(document);
        }

        private void OnDialogBoxShowing(object? sender, DialogBoxShowingEventArgs args)
        {
            OpeningScope? scope = ActiveScope;
            if (scope == null || _handlingDialog) return;
            _handlingDialog = true;
            try
            {
                string message = DialogPolicies.ReadMessage(args);
                string details = "Type=" + args.GetType().Name + "; DialogId=" + args.DialogId + "; Message=" + message;
                DialogPolicy? policy = DialogPolicies.Match(args, message);
                if (policy == null)
                {
                    Observe(scope, "UnknownDialog", details);
                    return;
                }
                string signature = (args.DialogId ?? "") + "|" + message + "|" + policy.Result;
                if (!scope.AllowDialogAttempt(signature))
                {
                    Observe(scope, "DialogRepeatLimit", details);
                    return;
                }
                bool accepted = args.OverrideResult(policy.Result);
                if (accepted) scope.DialogsHandled++;
                _log.Write(accepted ? "INFO" : "WARN", accepted ? "DialogAnswered" : "DialogResultRejected",
                    "Policy=" + policy.Id + "; Result=" + policy.Result + "; " + details, scope.Id, scope.Path);
            }
            catch (Exception ex)
            {
                _log.Write("WARN", "DialogError", "Left Revit's dialog handling in control.", scope.Id, scope.Path, ex);
            }
            finally { _handlingDialog = false; }
        }

        private void OnFailuresProcessing(object? sender, FailuresProcessingEventArgs args)
        {
            OpeningScope? scope = ActiveScope;
            if (scope == null || _handlingFailures) return;
            _handlingFailures = true;
            try
            {
                FailuresAccessor accessor = args.GetFailuresAccessor();
                Document document = accessor.GetDocument();
                if (document == null || !document.IsValidObject || document.IsFamilyDocument || scope.PreviouslyOpen.Contains(document)) return;
                if (args.GetProcessingResult() == FailureProcessingResult.ProceedWithRollBack || !accessor.IsTransactionBeingCommitted()) return;

                bool resolvedAny = false;
                foreach (FailureMessageAccessor failure in accessor.GetFailureMessages())
                {
                    try
                    {
                        string ids = string.Join(",", failure.GetFailingElementIds()
                            .Select(ElementIdValue).OrderBy(id => id).Select(id => id.ToString(CultureInfo.InvariantCulture)));
                        string definition = failure.GetFailureDefinitionId().Guid.ToString();
                        string description = failure.GetDescriptionText() ?? "";
                        string details = "FailureId=" + definition + "; Severity=" + failure.GetSeverity()
                            + "; ElementIds=" + ids + "; Description=" + description;
                        FailurePolicy? policy = FailurePolicies.Match(failure);
                        if (policy == null)
                        {
                            Observe(scope, "UnknownFailure", details + "; Available=" + DescribeResolutions(failure));
                            continue;
                        }
                        bool available = policy.Resolution != FailureResolutionType.DetachElements
                            || failure.HasResolutionOfType(policy.Resolution);
                        if (!available || !accessor.IsFailureResolutionPermitted(failure, policy.Resolution))
                        {
                            Observe(scope, "ResolutionUnavailable", "Policy=" + policy.Id + "; " + details);
                            continue;
                        }
                        string key = document.GetHashCode() + "|" + definition + "|" + ids + "|" + policy.Resolution;
                        if (accessor.GetAttemptedResolutionTypes(failure).Contains(policy.Resolution)
                            || !scope.AttemptedRepairs.Add(key))
                        {
                            Observe(scope, "ResolutionAlreadyAttempted", "Policy=" + policy.Id + "; " + details);
                            continue;
                        }
                        failure.SetCurrentResolutionType(policy.Resolution);
                        accessor.ResolveFailure(failure);
                        resolvedAny = true;
                        scope.RepairsApplied++;
                        _log.Write("INFO", "RecoveryRequested", "Policy=" + policy.Id + "; Resolution="
                            + policy.Resolution + "; " + details, scope.Id, scope.Path);
                    }
                    catch (Exception ex)
                    {
                        _log.Write("WARN", "FailureError", "One failure could not be handled; continuing with the remaining failures.",
                            scope.Id, scope.Path, ex);
                    }
                }
                // Never override another handler's rollback or force a commit without a resolution.
                if (resolvedAny && args.GetProcessingResult() != FailureProcessingResult.ProceedWithRollBack)
                    args.SetProcessingResult(FailureProcessingResult.ProceedWithCommit);
            }
            catch (Exception ex)
            {
                _log.Write("WARN", "FailureProcessing", "Could not process this opening failure callback.", scope.Id, scope.Path, ex);
            }
            finally { _handlingFailures = false; }
        }

        private static string DescribeResolutions(FailureMessageAccessor failure)
        {
            var names = new List<string>();
            foreach (FailureResolutionType resolution in Enum.GetValues(typeof(FailureResolutionType)))
            {
                try { if (failure.HasResolutionOfType(resolution)) names.Add(resolution.ToString()); }
                catch { /* Some enum values are not meaningful for this failure. */ }
            }
            return string.Join(",", names);
        }

        private static long ElementIdValue(ElementId id)
        {
#if REVIT_LEGACY_IDS
            return id.IntegerValue;
#else
            return id.Value;
#endif
        }

        private void Observe(OpeningScope scope, string category, string details)
        {
            if (scope.Observations.Add(category + "|" + details))
                _log.Write("WARN", category, details, scope.Id, scope.Path);
        }

        private void ResetOpenings(string reason)
        {
            while (_openings.Count > 0)
            {
                OpeningScope scope = _openings.Pop();
                _log.Write("WARN", "OpeningScopeCleared", reason, scope.Id, scope.Path);
            }
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            try { _unsubscribe?.Invoke(); }
            catch (Exception ex) { _log.Write("WARN", "Shutdown", "Could not remove all subscriptions.", error: ex); }
            _unsubscribe = null;
            ResetOpenings("Add-in shutdown");
            _knownDocuments.Clear();
            _uiApplication = null;
            StateChanged = null;
            _log.Write("INFO", "Shutdown", "Open Guard stopped.");
        }
    }
}
