using System;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitOpenGuard.Infrastructure;

namespace RevitOpenGuard
{
    [Transaction(TransactionMode.Manual)]
    public sealed class CatalogUpdatesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var service = App.EnsureInitialized(data.Application);
            if (service.IsOpening) return Result.Cancelled;
            new CatalogUpdateWindow(() => service.IsOpening).ShowDialog();
            return Result.Succeeded;
        }
    }

    internal sealed class CatalogUpdateWindow : System.Windows.Window
    {
        private readonly System.Windows.Controls.TextBlock _status;
        private readonly System.Windows.Controls.Button _check;
        private readonly System.Windows.Controls.Button _apply;
        private readonly Func<bool> _opening;
        private byte[]? _pending;
        public CatalogUpdateWindow(Func<bool> opening)
        {
            _opening = opening;
            CatalogUpdateNotice.WindowOpen = true;
            Closed += (sender, args) => { CatalogUpdateNotice.WindowOpen = false; CatalogUpdateNotice.RequestNotification(); };
            Title = "MM Tools | Open Guard error catalog"; Width = 540; Height = 340;
            ResizeMode = System.Windows.ResizeMode.NoResize;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(24) };
            Content = panel;
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Error catalog", FontSize = 23, Margin = new System.Windows.Thickness(0, 0, 0, 16) });
            _status = new System.Windows.Controls.TextBlock { Text = "Installed catalog: " + CatalogStore.Current.Version + "\n\nCheck for rules published by Mostafa Magdy.\nYour saved catalog works without an internet connection.",
                TextWrapping = System.Windows.TextWrapping.Wrap, Height = 150 };
            panel.Children.Add(_status);
            var buttons = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            panel.Children.Add(buttons);
            _check = new System.Windows.Controls.Button { Content = "Check for updates", Padding = new System.Windows.Thickness(12, 7, 12, 7) };
            _apply = new System.Windows.Controls.Button { Content = "Update", IsEnabled = false, Margin = new System.Windows.Thickness(12, 0, 0, 0), Padding = new System.Windows.Thickness(12, 7, 12, 7) };
            buttons.Children.Add(_check); buttons.Children.Add(_apply);
            _check.Click += async (sender, args) =>
            {
                _check.IsEnabled = false; _apply.IsEnabled = false; _pending = null;
                _status.Text = "Checking for updates...";
                try
                {
                    _pending = await CatalogStore.CheckAsync(trace: details => CatalogUpdateNotice.Trace("Manual; " + details));
                    if (_pending == null) _status.Text = "Your error catalog is up to date.\nVersion: " + CatalogStore.Current.Version;
                    else
                    {
                        var catalog = CatalogStore.Verify(_pending);
                        _status.Text = "Update available: " + catalog.Version + "\n\n" + catalog.Notes;
                        _apply.IsEnabled = true;
                    }
                }
                catch (Exception ex) { _status.Text = "Could not check for updates. Your existing catalog is still active.\n\n" + ex.Message; }
                finally { _check.IsEnabled = true; }
            };
            _apply.Click += (sender, args) =>
            {
                try
                {
                    if (_opening()) throw new InvalidOperationException("Wait until model opening has finished.");
                    if (_pending == null) return;
                    CatalogStore.Apply(_pending);
                    _apply.IsEnabled = false; _pending = null;
                    _status.Text = "Updated successfully.\nActive catalog: " + CatalogStore.Current.Version + "\n\nThe rules apply to the next model opening.";
                }
                catch (Exception ex) { _status.Text = "Could not update the catalog.\n" + ex.Message; }
            };
        }
    }
}
