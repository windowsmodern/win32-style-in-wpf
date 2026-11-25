using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThemeDemo
{
    /// <summary>
    /// Verifies TrackContentState: the outer button follows the content button's hover/press state
    /// and the content button follows the outer button's state (both directions), without the outer
    /// stealing input from the content.
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

        private static string Name (ImageSource source, ImageSource normal, ImageSource hover, ImageSource active, ImageSource disabled)
        {
            if (source == null) return "null";
            if (ReferenceEquals (source, normal)) return "normal";
            if (ReferenceEquals (source, hover)) return "hover";
            if (ReferenceEquals (source, active)) return "active";
            if (ReferenceEquals (source, disabled)) return "disabled";
            return "other";
        }

        private static void SetPressed (ButtonBase button, bool value)
        {
            var key = (DependencyPropertyKey)typeof (ButtonBase)
                .GetField ("IsPressedPropertyKey", BindingFlags.NonPublic | BindingFlags.Static).GetValue (null);
            button.SetValue (key, value);
        }

        private static void Configure (FrameworkElement b, ImageSource normal, ImageSource hover, ImageSource active, ImageSource disabled)
        {
            b.Width = 22;
            b.Height = 22;
            Type type = b.GetType ();
            type.GetProperty ("NormalSource").SetValue (b, normal, null);
            type.GetProperty ("HoverSource").SetValue (b, hover, null);
            type.GetProperty ("ActiveSource").SetValue (b, active, null);
            type.GetProperty ("DisabledSource").SetValue (b, disabled, null);
        }

        public static int Run (bool transition)
        {
            _failures = 0;
            Log.Clear ();
            Say ("TrackContentState  (theme: {0})", transition ? "transition" : "plain");

            ImageSource normal = Art (Colors.SteelBlue), hover = Art (Colors.Orange), active = Art (Colors.SeaGreen), disabled = Art (Colors.Gray);
            ImageSource fgNormal = Art (Colors.White), fgHover = Art (Colors.Gold), fgActive = Art (Colors.DarkGreen), fgDisabled = Art (Colors.DimGray);

            var outer = new HackButton ();
            var inner = new HackButton ();
            Configure (outer, normal, hover, active, disabled);
            Configure (inner, fgNormal, fgHover, fgActive, fgDisabled);
            outer.Content = inner;

            var panel = new StackPanel ();
            panel.Children.Add (outer);
            var window = new Window { Width = 140, Height = 120, Content = panel };
            window.Show ();
            window.UpdateLayout ();

            Check (outer.TrackContentState == false, "TrackContentState defaults to false", string.Empty);

            // child -> parent
            SetPressed (inner, true);
            Check (inner.IsPressed, "child button is pressed", "inner.IsPressed=" + inner.IsPressed);
            Check (Current (outer) == normal, "without the flag the parent keeps its normal image",
                Name (Current (outer), normal, hover, active, disabled));

            outer.TrackContentState = true;
            Check (Current (outer) == active, "parent follows the child's pressed state",
                Name (Current (outer), normal, hover, active, disabled));
            Check (!outer.IsPressed, "parent itself is not pressed", "outer.IsPressed=" + outer.IsPressed);
            Check (Current (inner) == fgActive, "child keeps its own pressed image",
                Name (Current (inner), fgNormal, fgHover, fgActive, fgDisabled));

            // parent -> child (the part that used to be missing)
            SetPressed (inner, false);
            Check (Current (outer) == normal && Current (inner) == fgNormal, "both return to normal",
                Name (Current (outer), normal, hover, active, disabled) + " / " + Name (Current (inner), fgNormal, fgHover, fgActive, fgDisabled));

            SetPressed (outer, true);
            Check (Current (outer) == active, "parent shows its own pressed image",
                Name (Current (outer), normal, hover, active, disabled));
            Check (Current (inner) == fgActive, "child follows the parent's pressed state",
                Name (Current (inner), fgNormal, fgHover, fgActive, fgDisabled));
            SetPressed (outer, false);
            Check (Current (inner) == fgNormal, "child returns to normal with the parent",
                Name (Current (inner), fgNormal, fgHover, fgActive, fgDisabled));

            // disabled travels down as well
            outer.IsEnabled = false;
            Check (Current (inner) == fgDisabled, "child follows the parent's disabled state",
                Name (Current (inner), fgNormal, fgHover, fgActive, fgDisabled));
            outer.IsEnabled = true;

            // child set after the flag is linked both ways
            var lateOuter = new HackButton ();
            Configure (lateOuter, normal, hover, active, disabled);
            lateOuter.TrackContentState = true;
            var lateInner = new HackButton ();
            Configure (lateInner, fgNormal, fgHover, fgActive, fgDisabled);
            lateOuter.Content = lateInner;
            panel.Children.Add (lateOuter);
            window.UpdateLayout ();
            SetPressed (lateInner, true);
            Check (Current (lateOuter) == active, "content set after the flag is tracked", string.Empty);
            SetPressed (lateInner, false);
            SetPressed (lateOuter, true);
            Check (Current (lateInner) == fgActive, "late content follows the parent too", string.Empty);
            SetPressed (lateOuter, false);

            // the other two button classes behave the same
            var repeatOuter = new HackRepeatButton ();
            var repeatInner = new HackRepeatButton ();
            Configure (repeatOuter, normal, hover, active, disabled);
            Configure (repeatInner, fgNormal, fgHover, fgActive, fgDisabled);
            repeatOuter.Content = repeatInner;
            repeatOuter.TrackContentState = true;
            panel.Children.Add (repeatOuter);
            window.UpdateLayout ();
            SetPressed (repeatInner, true);
            Check (Current (repeatOuter) == active, "HackRepeatButton follows its content", string.Empty);
            SetPressed (repeatInner, false);
            SetPressed (repeatOuter, true);
            Check (Current (repeatInner) == fgActive, "HackRepeatButton content follows it", string.Empty);
            SetPressed (repeatOuter, false);

            var toggleOuter = new HackToggleButton ();
            var toggleInner = new HackToggleButton ();
            Configure (toggleOuter, normal, hover, active, disabled);
            Configure (toggleInner, fgNormal, fgHover, fgActive, fgDisabled);
            toggleOuter.Content = toggleInner;
            toggleOuter.TrackContentState = true;
            panel.Children.Add (toggleOuter);
            window.UpdateLayout ();
            SetPressed (toggleInner, true);
            Check (Current (toggleOuter) == active, "HackToggleButton follows its content", string.Empty);
            SetPressed (toggleInner, false);
            SetPressed (toggleOuter, true);
            Check (Current (toggleInner) == fgActive, "HackToggleButton content follows it", string.Empty);
            SetPressed (toggleOuter, false);

            // TrackOwnerState: the MenuItem owns the input, the two layers only render the state.
            var menuItem = new MenuItem { Header = "m", Width = 22, Height = 22 };
            var innerFactory = new FrameworkElementFactory (typeof (HackButton));
            innerFactory.Name = "PART_Foreground";
            innerFactory.SetValue (HackButton.TrackOwnerStateProperty, true);
            innerFactory.SetValue (HackButton.NormalSourceProperty, (ImageSource)fgNormal);
            innerFactory.SetValue (HackButton.HoverSourceProperty, (ImageSource)fgHover);
            innerFactory.SetValue (HackButton.ActiveSourceProperty, (ImageSource)fgActive);
            var outerFactory = new FrameworkElementFactory (typeof (HackButton));
            outerFactory.Name = "PART_Background";
            outerFactory.SetValue (HackButton.TrackOwnerStateProperty, true);
            outerFactory.SetValue (HackButton.NormalSourceProperty, (ImageSource)normal);
            outerFactory.SetValue (HackButton.HoverSourceProperty, (ImageSource)hover);
            outerFactory.SetValue (HackButton.ActiveSourceProperty, (ImageSource)active);
            outerFactory.AppendChild (innerFactory);
            var gridFactory = new FrameworkElementFactory (typeof (Grid));
            gridFactory.SetValue (Panel.BackgroundProperty, Brushes.Transparent);
            gridFactory.AppendChild (outerFactory);
            menuItem.Template = new ControlTemplate (typeof (MenuItem)) { VisualTree = gridFactory };
            var menu = new Menu ();
            menu.Items.Add (menuItem);
            panel.Children.Add (menu);
            window.UpdateLayout ();
            menuItem.ApplyTemplate ();
            window.UpdateLayout ();

            var menuBackground = (HackButton)menuItem.Template.FindName ("PART_Background", menuItem);
            var menuForeground = (HackButton)menuItem.Template.FindName ("PART_Foreground", menuItem);
            Check (menuBackground != null && menuBackground.TrackOwnerState && !menuBackground.IsHitTestVisible,
                "background layer tracks the owner and is hit-test invisible",
                menuBackground == null ? "missing" : "IsHitTestVisible=" + menuBackground.IsHitTestVisible);
            Check (menuForeground != null && menuForeground.TrackOwnerState && !menuForeground.IsHitTestVisible,
                "foreground layer tracks the owner and is hit-test invisible",
                menuForeground == null ? "missing" : "IsHitTestVisible=" + menuForeground.IsHitTestVisible);
            object menuHit = menuItem.InputHitTest (new Point (11, 11));
            Check (menuHit is Grid, "the MenuItem receives the hit, not a HackButton",
                menuHit == null ? "null" : menuHit.GetType ().Name);

            menuItem.RaiseEvent (new MouseButtonEventArgs (Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
            });
            Check (menuBackground != null && Current (menuBackground) == active,
                "MenuItem mouse-down drives the background layer",
                menuBackground == null ? "missing" : Name (Current (menuBackground), normal, hover, active, disabled));
            Check (menuForeground != null && Current (menuForeground) == fgActive,
                "MenuItem mouse-down drives the foreground layer",
                menuForeground == null ? "missing" : Name (Current (menuForeground), fgNormal, fgHover, fgActive, fgDisabled));

            window.RaiseEvent (new MouseButtonEventArgs (Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent
            });
            Check (menuBackground != null && Current (menuBackground) == normal,
                "MenuItem mouse-up restores the background layer",
                menuBackground == null ? "missing" : Name (Current (menuBackground), normal, hover, active, disabled));

            // Outside a template the parent element becomes the state source.
            var hostButton = new Button { Width = 22, Height = 22 };
            var driven = new HackButton ();
            Configure (driven, normal, hover, active, disabled);
            driven.TrackOwnerState = true;
            hostButton.Content = driven;
            panel.Children.Add (hostButton);
            window.UpdateLayout ();
            Check (!driven.IsHitTestVisible, "parent-driven layer is hit-test invisible", "IsHitTestVisible=" + driven.IsHitTestVisible);
            SetPressed (hostButton, true);
            Check (Current (driven) == active, "TrackOwnerState follows a plain parent button",
                Name (Current (driven), normal, hover, active, disabled));
            SetPressed (hostButton, false);
            hostButton.IsEnabled = false;
            Check (Current (driven) == disabled, "TrackOwnerState follows the parent disabled state",
                Name (Current (driven), normal, hover, active, disabled));

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
