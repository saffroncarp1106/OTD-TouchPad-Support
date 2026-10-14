using System;
using System.Globalization;
using System.Linq;
using AsusTouchpad;
using Eto.Forms;
using Eto.Drawing;

namespace OpenTabletDriver.UX.Wpf;
internal static class TouchpadSetup
{
    public static bool Ensure(bool force)
    {
        var devices = TouchpadNative.Enumerate();
        try
        {
            TouchpadProfile saved = null;
            try
            {
                saved = TouchpadProfile.Load();
            }
            catch
            {
                force = true;
            }

            var selected = TouchpadProfile.Resolve(devices, saved);
            if (!force && selected != null && ((saved != null && saved.ValidDimensions) || (saved == null && selected.Width.HasValue && selected.Height.HasValue)))
            {
                if (saved == null)
                    TouchpadProfile.Save(selected);
                return true;
            }

            if (!force && devices.Count == 0)
                return true;
            _ = new Application(Eto.Platforms.Wpf);
            var dialog = new Dialog<bool>
            {
                Title = "Select your touchpad",
                ClientSize = new Size(580, 340),
                Padding = 16
            };
            var chooser = new DropDown();
            foreach (var d in devices)
                chooser.Items.Add($"{d.Name} [{d.Vendor:X4}:{d.Product:X4}] — {d.Path.Split('#').ElementAtOrDefault(1)}");
            var width = new TextBox();
            var height = new TextBox();
            var guard = new CheckBox
            {
                Text = "Suppress relative movement while mapping",
                Checked = saved?.SuppressRelativeMovement ?? true
            };
            var note = new Label
            {
                Text = "Dimensions are in millimeters. The movement guard can also block another mouse while touching the pad.",
                Wrap = WrapMode.Word
            };
            chooser.SelectedIndexChanged += (_, _) =>
            {
                if (chooser.SelectedIndex < 0)
                    return;
                var d = devices[chooser.SelectedIndex];
                width.Text = (d.Width ?? 0).ToString(CultureInfo.InvariantCulture);
                height.Text = (d.Height ?? 0).ToString(CultureInfo.InvariantCulture);
            };
            if (devices.Count > 0)
                chooser.SelectedIndex = selected == null ? 0 : devices.IndexOf(selected);
            if (selected != null && saved != null)
            {
                width.Text = saved.Width.ToString(CultureInfo.InvariantCulture);
                height.Text = saved.Height.ToString(CultureInfo.InvariantCulture);
            }

            var use = new Button
            {
                Text = "Use touchpad",
                Enabled = devices.Count > 0
            };
            var cancel = new Button
            {
                Text = "Cancel"
            };
            use.Click += (_, _) =>
            {
                try
                {
                    TouchpadProfile.Save(devices[chooser.SelectedIndex], float.Parse(width.Text, CultureInfo.InvariantCulture), float.Parse(height.Text, CultureInfo.InvariantCulture), guard.Checked == true);
                    dialog.Close(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(dialog, ex.Message);
                }
            };
            cancel.Click += (_, _) => dialog.Close(false);
            var layout = new DynamicLayout
            {
                Spacing = new Size(8, 10)
            };
            layout.AddRow(new Label { Text = devices.Count == 0 ? "No compatible touchpad found." : "Choose the laptop touchpad to map:" });
            layout.AddRow(chooser);
            layout.AddRow(new Label { Text = "Width (mm)" }, width, new Label { Text = "Height (mm)" }, height);
            layout.AddRow(guard);
            layout.AddRow(note);
            layout.Add(null);
            layout.AddRow(null, cancel, use);
            dialog.Content = layout;
            dialog.DefaultButton = use;
            dialog.AbortButton = cancel;
            bool result = dialog.ShowModal();
            Eto.Platform.AllowReinitialize = true;
            return result;
        }
        finally
        {
            foreach (var device in devices)
                device.Dispose();
        }
    }
}