using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitOpenGuard
{
    [Transaction(TransactionMode.Manual)]
    public sealed class ToggleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var service = App.EnsureInitialized(commandData.Application);
                if (!service.Toggle(out string error))
                    TaskDialog.Show("Revit Open Guard", "Open Guard is " + (service.Enabled ? "ON" : "OFF")
                        + " for this session, but the choice could not be saved for the next startup:\n" + error);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "Open Guard could not change state: " + ex.Message;
                return Result.Failed;
            }
        }
    }

    public sealed class ToggleAvailability : IExternalCommandAvailability
    {
        // Available with zero open documents. No document transaction or selection is required.
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories) => true;
    }
}
