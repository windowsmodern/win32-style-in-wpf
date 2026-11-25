using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThemeDemo
{
    /// <summary>
    /// Verifies TrackContentState: a button that wraps another button shows its own hover/press
    /// images when the child is hovered/pressed, while the child keeps the input and the Click.
    /// </summary>
    internal static class VisualStateProbe
    {
        private const string Switch = "--visualstateprobe";

        private static readonly List<string> Log = new List<string> ();

        private static int _failures;

        private static void Say (string format, params object[] args)
        {
            string line = string.Format (format, args);
            Log.Add (line);
            Console.WriteLine (line);
        }

        private static void Check (bool ok, string label, string detail)
        {
            if (!ok)
                _failures++;
            Say ("  [{0}] {1}{2}", ok ? "ok" : "FAIL", label,
                string.IsNullOrEmpty (detail) ? string.Empty : "  --  " + detail);
        }

        public static bool IsRequested (string[] args)
        {
            if (args == null)
                return false;

            foreach (string arg in args)
            {
                if (string.Equals (arg, Switch, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static BitmapSource Art (Color color)
        {
            var visual = new DrawingVisual ();
            using (DrawingContext dc = visual.RenderOpen ())
                dc.DrawRectangle (new SolidColorBrush (color), null, new Rect (0, 0, 22, 22));
            var bitmap = new RenderTargetBitmap (22, 22, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render (visual);
            bitmap.Freeze ();
            return bitmap;
        }

        private static ImageSource Current (Control button)
        {
            object chrome = button.GetType ().GetField ("_chrome", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue (button);
            return ((HackImage)chrome.GetType ().GetProperty ("Art", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue (chrome, null)).Source;
        }

        private static string Name (ImageSource source, ImageSource normal, ImageSource hover, ImageSource active)
        {
            if (source == null) return "null";
            if (ReferenceEquals (source, normal)) return "normal";
            if (ReferenceEquals (source, hover)) return "hover";
            if (ReferenceEquals (source, active)) return "active";
            return "other";
        }

        private static void SetPressed (ButtonBase button, bool value)
        {
            var key = (DependencyPropertyKey)typeof (ButtonBase)
                .GetField ("IsPressedPropertyKey", BindingFlags.NonPublic | BindingFlags.Static).GetValue (null);
            button.SetValue (key, value);
        }

        private static void Configure (FrameworkElement b, ImageSource normal, ImageSource hover, ImageSource active)
        {
            b.Width = 22;
            b.Height = 22;
            Type type = b.GetType ();
            type.GetProperty ("NormalSource").SetValue (b, normal, null);
            type.GetProperty ("HoverSource").SetValue (b, hover, null);
            type.GetProperty ("ActiveSource").SetValue (b, active, null);
        }

        public static int Run (bool transition)
        {
            _failures = 0;
            Log.Clear ();
            Say ("TrackContentState  (theme: {0})", transition ? "transition" : "plain");

            ImageSource normal = Art (Colors.SteelBlue), hover = Art (Colors.Orange), active = Art (Colors.SeaGreen);
            ImageSource fgNormal = Art (Colors.White), fgHover = Art (Colors.Gold), fgActive = Art (Colors.DarkGreen);

            // Outer background button wrapping the foreground button, exactly like the menu item.
            var outer = new HackButton ();
            var inner = new HackButton ();
            Configure (outer, normal, hover, active);
            Configure (inner, fgNormal, fgHover, fgActive);
            outer.Content = inner;

            var panel = new StackPanel ();
            panel.Children.Add (outer);
            var window = new Window { Width = 120, Height = 100, Content = panel };
            window.Show ();
            window.UpdateLayout ();

            Check (outer.TrackContentState == false, "TrackContentState defaults to false", string.Empty);
            SetPressed (inner, true);
            Check (inner.IsPressed, "child button is pressed", "inner.IsPressed=" + inner.IsPressed);
            Check (Current (outer) == normal, "without the flag the parent keeps its normal image",
                Name (Current (outer), normal, hover, active));

            outer.TrackContentState = true;
            Check (Current (outer) == active, "tracking mirrors the child's pressed state",
                Name (Current (outer), normal, hover, active));
            Check (!outer.IsPressed, "parent itself is not pressed", "outer.IsPressed=" + outer.IsPressed);
            Check (Current (inner) == fgActive, "child keeps its own pressed image",
                Name (Current (inner), fgNormal, fgHover, fgActive));

            SetPressed (inner, false);
            Check (Current (outer) == normal, "releasing the child restores the parent image",
                Name (Current (outer), normal, hover, active));

            // Flag set before the content arrives: OnContentChanged has to attach the tracking.
            var lateOuter = new HackButton ();
            Configure (lateOuter, normal, hover, active);
            lateOuter.TrackContentState = true;
            var lateInner = new HackButton ();
            Configure (lateInner, fgNormal, fgHover, fgActive);
            lateOuter.Content = lateInner;
            panel.Children.Add (lateOuter);
            window.UpdateLayout ();
            SetPressed (lateInner, true);
            Check (Current (lateOuter) == active, "content set after the flag is tracked too",
                Name (Current (lateOuter), normal, hover, active));

            // The other two button classes share the same property.
            var repeatOuter = new HackRepeatButton ();
            var repeatInner = new HackRepeatButton ();
            Configure (repeatOuter, normal, hover, active);
            Configure (repeatInner, fgNormal, fgHover, fgActive);
            repeatOuter.Content = repeatInner;
            repeatOuter.TrackContentState = true;
            panel.Children.Add (repeatOuter);
            window.UpdateLayout ();
            SetPressed (repeatInner, true);
            Check (Current (repeatOuter) == active, "HackRepeatButton tracks its content",
                Name (Current (repeatOuter), normal, hover, active));

            var toggleOuter = new HackToggleButton ();
            var toggleInner = new HackToggleButton ();
            Configure (toggleOuter, normal, hover, active);
            Configure (toggleInner, fgNormal, fgHover, fgActive);
            toggleOuter.Content = toggleInner;
            toggleOuter.TrackContentState = true;
            panel.Children.Add (toggleOuter);
            window.UpdateLayout ();
            SetPressed (toggleInner, true);
            Check (Current (toggleOuter) == active, "HackToggleButton tracks its content",
                Name (Current (toggleOuter), normal, hover, active));

            // Disabled still outranks the child state.
            outer.IsEnabled = false;
            Check (Current (outer) == normal, "disabled wins over the child state (no disabled image set)",
                Name (Current (outer), normal, hover, active));

            Say (_failures == 0 ? "all content-state checks passed" : _failures + " check(s) failed");
            try
            {
                File.WriteAllLines ("visualstateprobe.log", Log.ToArray ());
            }
            catch (Exception ex)
            {
                Say ("log write failed: {0}", ex.Message);
            }
            window.Close ();
            return _failures == 0 ? 0 : 1;
        }
    }
}
