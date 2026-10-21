using System;
using Autodesk.Revit.DB;
using RevitOpenGuard.Infrastructure;
namespace RevitOpenGuard.Policies
{
    internal sealed class FailurePolicy
    {
        public string Id { get; }
        public FailureResolutionType Resolution { get; }
        public FailurePolicy(string id, FailureResolutionType resolution) { Id = id; Resolution = resolution; }
    }
    internal static class FailurePolicies
    {
        public static FailurePolicy? Match(FailureMessageAccessor failure)
        {
            Guid id = failure.GetFailureDefinitionId().Guid;

            // Compiled opening recovery, independent of the selected signed catalog version.
            // Revit displays: "The Tap must be attached to duct." -> Delete Element(s).
            // Match the Error definition only: never infer deletion from similar warning/pipe
            // text. GuardService supplies the ON/opening scope, permitted-resolution check,
            // per-opening/per-transaction repeat guards, and reported-element audit logging.
            if (id == BuiltInFailures.ConnectorFailures.FittingMustBeOnDuct.Guid)
            {
                return failure.GetSeverity() == FailureSeverity.Error
                    ? new FailurePolicy("Connector.FittingMustBeOnDuct", FailureResolutionType.DeleteElements)
                    : null;
            }

            string message = failure.GetDescriptionText() ?? "";
            foreach (var rule in CatalogStore.Current.Rules)
            {
                if (rule.Kind != "Failure" || !rule.Matches(CatalogStore.RevitYear, message)) continue;
                if (!string.IsNullOrEmpty(rule.FailureId))
                {
                    Guid expected;
                    switch (rule.FailureId)
                    {
                        case "BuiltIn:LinearConstraintNotParallel": expected = BuiltInFailures.DimensionFailures.LinearConstraintNotParallel.Guid; break;
                        case "BuiltIn:RadialDimensionCannotProjectToArc": expected = BuiltInFailures.DimensionFailures.RadialDimensionCannotProjectToArc.Guid; break;
                        case "BuiltIn:DimensionPerpendicularToView": expected = BuiltInFailures.DimensionFailures.DimensionPerpendicularToView.Guid; break;
                        default: expected = Guid.Parse(rule.FailureId); break;
                    }
                    if (id != expected) continue;
                }
                return new FailurePolicy(rule.Id, rule.Action == "DetachElements" ? FailureResolutionType.DetachElements : FailureResolutionType.DeleteElements);
            }
            return null;
        }
    }
}
