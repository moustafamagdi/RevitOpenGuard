using System;
using Autodesk.Revit.DB;

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
            // The built-in IDs are independent of Revit UI language.
            Guid id = failure.GetFailureDefinitionId().Guid;
            if (id == BuiltInFailures.DimensionFailures.LinearConstraintNotParallel.Guid)
                return new FailurePolicy("Dimension.LinearConstraintNotParallel", FailureResolutionType.DetachElements);
            if (id == BuiltInFailures.DimensionFailures.RadialDimensionCannotProjectToArc.Guid)
                return new FailurePolicy("Dimension.RadialDimensionCannotProjectToArc", FailureResolutionType.DeleteElements);
            if (id == BuiltInFailures.DimensionFailures.DimensionPerpendicularToView.Guid)
                return new FailurePolicy("Dimension.DimensionPerpendicularToView", FailureResolutionType.DeleteElements);

            // Compatibility fallback for the same opening-message families used in the exporter.
            string text = DialogPolicies.Normalize(failure.GetDescriptionText());
            if (text.Contains("cannot form radial dimension") || text.Contains("can't form radial dimension"))
                return new FailurePolicy("Text.InvalidRadialDimension", FailureResolutionType.DeleteElements);
            if (text.Contains("dimension is perpendicular to the current view") && text.Contains("no longer valid"))
                return new FailurePolicy("Text.PerpendicularDimension", FailureResolutionType.DeleteElements);

            bool annotation = text.Contains("dimension") || text.Contains("tag") || text.Contains("reference");
            if (annotation && DialogPolicies.ContainsAny(text, "no longer parallel", "reference is no longer",
                "references are no longer", "reference is missing", "references are missing", "missing reference",
                "lost reference", "invalid reference", "references to elements have been lost",
                "references are or have become invalid"))
                return new FailurePolicy("Text.BrokenAnnotationReference", FailureResolutionType.DetachElements);
            return null;
        }
    }
}
