using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.UI;
using RevitOpenGuard.Runtime;

namespace RevitOpenGuard
{
    public sealed class App : IExternalApplication
    {
        internal static GuardService? Service { get; private set; }
        private static PushButton? _button;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                if (Service == null || Service.IsDisposed)
                {
                    Service = new GuardService();
                    Service.Attach(application);
                    Service.StateChanged += OnStateChanged;
                }
                CreateRibbon(application);
                RefreshButton();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Service?.Dispose();
                Service = null;
                TaskDialog.Show("Revit Open Guard", "Open Guard could not start:\n" + ex.Message);
                return Result.Failed;
            }
        }

        internal static GuardService EnsureInitialized(UIApplication application)
        {
            if (Service == null || Service.IsDisposed)
            {
                var service = new GuardService();
                try { service.Attach(application); }
                catch { service.Dispose(); throw; }
                Service = service;
                Service.StateChanged += OnStateChanged;
            }
            return Service;
        }

        private static void CreateRibbon(UIControlledApplication application)
        {
            const string tab = "MM Tools";
            try { application.CreateRibbonTab(tab); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { /* Tab may belong to another MM add-in. */ }
            RibbonPanel panel = application.GetRibbonPanels(tab).FirstOrDefault(p => p.Name == "Model Opening")
                ?? application.CreateRibbonPanel(tab, "Model Opening");
            _button = panel.GetItems().OfType<PushButton>().FirstOrDefault(b => b.Name == "RevitOpenGuard.Toggle");
            if (_button == null)
            {
                var data = new PushButtonData("RevitOpenGuard.Toggle", "Open Guard\nOFF",
                    Assembly.GetExecutingAssembly().Location, typeof(ToggleCommand).FullName)
                {
                    AvailabilityClassName = typeof(ToggleAvailability).FullName,
                    ToolTip = "Automatically handle known model-opening blockers. Click to turn ON or OFF.",
                    LongDescription = "ON is remembered after restarting Revit. Handles known opening dialogs, removes broken references, and deletes invalid dimensions when Revit permits the recovery. It does not Save or Sync. Unknown messages remain visible."
                };
                _button = panel.AddItem(data) as PushButton;
            }
        }

        private static void OnStateChanged(object? sender, EventArgs e) => RefreshButton();

        private static void RefreshButton()
        {
            if (_button == null) return;
            bool enabled = Service?.Enabled == true;
            _button.ItemText = enabled ? "Open Guard\nON" : "Open Guard\nOFF";
            _button.ToolTip = enabled
                ? "ON: known opening blockers are handled automatically. Click to disable."
                : "OFF: normal Revit opening dialogs. Click to enable and remember this choice.";
            _button.LargeImage = CreateIcon(enabled, 32);
            _button.Image = CreateIcon(enabled, 16);
        }

        private static ImageSource CreateIcon(bool enabled, int size)
        {
            var drawing = new DrawingGroup();
            using (DrawingContext dc = drawing.Open())
            {
                dc.DrawRoundedRectangle(enabled ? Brushes.SeaGreen : Brushes.DimGray, null, new Rect(1, 1, 30, 30), 5, 5);
                var pen = new Pen(Brushes.White, 3);
                dc.DrawLine(pen, new Point(9, 16), new Point(14, 21));
                dc.DrawLine(pen, new Point(14, 21), new Point(24, 10));
            }
            drawing.Transform = new ScaleTransform(size / 32d, size / 32d);
            drawing.Freeze();
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            if (Service != null) Service.StateChanged -= OnStateChanged;
            Service?.Dispose();
            Service = null;
            _button = null;
            return Result.Succeeded;
        }
    }
}
