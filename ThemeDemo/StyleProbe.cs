using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ThemeDemo
{
    /// <summary>
    /// Hack* buttons keep their art in dependency properties, so a Style can supply the
    /// images.  This probe pins the three cases that used to be broken by the empty
    /// Style assigned in the constructor: implicit styles, explicit styles, and a Style
    /// applied to a HackButton inside another control's ControlTemplate.
    /// </summary>
    internal static class StyleProbe
    {
        private const string Switch = "--styleprobe";

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

        private static string Describe (ImageSource source)
        {
            return source == null ? "null" : source.GetType ().Name;
        }

        private static T FindChild<T> (DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount (root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild (root, i);
                T hit = child as T;
                if (hit != null)
                    return hit;
                T deep = FindChild<T> (child);
                if (deep != null)
                    return deep;
            }
            return null;
        }

        private static DrawingImage Swatch (string color)
        {
            var drawing = new GeometryDrawing (
                (Brush)new BrushConverter ().ConvertFromString (color),
                null,
                new RectangleGeometry (new Rect (0, 0, 8, 8)));
            return new DrawingImage (drawing);
        }

        public static int Run (bool transition)
        {
            _failures = 0;
            Log.Clear ();
            Say ("Hack* button sources through a Style  (theme: {0})", transition ? "transition" : "plain");

            var resources = new ResourceDictionary ();
            resources ["ImgA"] = Swatch ("#C05050");
            resources ["ImgB"] = Swatch ("#50A050");
            resources ["ImgC"] = Swatch ("#5050C0");

            var implicitStyle = new Style (typeof (HackButton));
            implicitStyle.Setters.Add (new Setter (HackButton.NormalSourceProperty, resources ["ImgA"]));
            resources [typeof (HackButton)] = implicitStyle;

            var keyedStyle = new Style (typeof (HackButton));
            keyedStyle.Setters.Add (new Setter (HackButton.NormalSourceProperty, resources ["ImgA"]));
            keyedStyle.Setters.Add (new Setter (HackButton.HoverSourceProperty, resources ["ImgB"]));
            keyedStyle.Setters.Add (new Setter (HackButton.ActiveSourceProperty, resources ["ImgC"]));
            resources ["Keyed"] = keyedStyle;

            var repeatStyle = new Style (typeof (HackRepeatButton));
            repeatStyle.Setters.Add (new Setter (HackRepeatButton.NormalSourceProperty, resources ["ImgB"]));
            resources [typeof (HackRepeatButton)] = repeatStyle;

            var toggleStyle = new Style (typeof (HackToggleButton));
            toggleStyle.Setters.Add (new Setter (HackToggleButton.NormalSourceProperty, resources ["ImgC"]));
            resources [typeof (HackToggleButton)] = toggleStyle;

            // The reported scenario: a HackButton inside a MenuItem ControlTemplate that gets its
            // images from a Style instead of inline attributes.
            var templateGrid = new FrameworkElementFactory (typeof (Grid));
            templateGrid.SetValue (Panel.BackgroundProperty, Brushes.Transparent);
            var templateButton = new FrameworkElementFactory (typeof (HackButton));
            templateButton.SetValue (FrameworkElement.StyleProperty, keyedStyle);
            templateButton.SetValue (FrameworkElement.WidthProperty, 22.0);
            templateButton.SetValue (FrameworkElement.HeightProperty, 22.0);
            templateButton.Name = "InTemplate";
            templateGrid.AppendChild (templateButton);
            var menuStyle = new Style (typeof (MenuItem));
            menuStyle.Setters.Add (new Setter (Control.TemplateProperty, new ControlTemplate (typeof (MenuItem)) { VisualTree = templateGrid }));
            resources ["MenuStyle"] = menuStyle;

            var panel = new StackPanel ();
            var implicitButton = new HackButton { Content = "implicit" };
            var explicitButton = new HackButton { Content = "explicit", Style = keyedStyle };
            var inlineButton = new HackButton { Content = "inline", NormalSource = (ImageSource)resources ["ImgB"] };
            var repeatButton = new HackRepeatButton { Content = "repeat" };
            var toggleButton = new HackToggleButton { Content = "toggle" };
            panel.Children.Add (implicitButton);
            panel.Children.Add (explicitButton);
            panel.Children.Add (inlineButton);
            panel.Children.Add (repeatButton);
            panel.Children.Add (toggleButton);
            var menu = new Menu ();
            var menuItem = new MenuItem { Header = "menu", Style = menuStyle };
            menu.Items.Add (menuItem);
            panel.Children.Add (menu);

            var window = new Window
            {
                Title = "Style probe",
                Width = 460,
                Height = 360,
                Resources = resources,
                Content = panel
            };
            window.Show ();
            Pump (window, 500);
            window.UpdateLayout ();
            {
                menuItem.ApplyTemplate ();
                HackButton inTemplate = FindChild<HackButton> (menuItem);

                Check (implicitButton.Style == implicitStyle && implicitButton.Style.Setters.Count == 1,
                    "implicit Style is applied", implicitButton.Style == null ? "Style null" : "setters " + implicitButton.Style.Setters.Count);
                Check (implicitButton.NormalSource != null, "implicit Style setter reaches NormalSource", Describe (implicitButton.NormalSource));

                Check (explicitButton.HoverSource != null && explicitButton.ActiveSource != null,
                    "explicit Style sets HoverSource/ActiveSource",
                    "hover " + Describe (explicitButton.HoverSource) + ", active " + Describe (explicitButton.ActiveSource));

                Check (ReferenceEquals (inlineButton.NormalSource, resources ["ImgB"]),
                    "local value still wins over the implicit Style", Describe (inlineButton.NormalSource));

                Check (inTemplate != null, "HackButton exists inside the MenuItem template", inTemplate == null ? "not found" : "found");
                if (inTemplate != null)
                {
                    Check (ReferenceEquals (inTemplate.Style, keyedStyle),
                        "template-supplied Style is not shadowed by the control",
                        inTemplate.Style == null ? "Style null" : inTemplate.Style.Setters.Count + " setters");
                    Check (inTemplate.HoverSource != null && inTemplate.ActiveSource != null,
                        "template HackButton gets its images from the Style",
                        "hover " + Describe (inTemplate.HoverSource) + ", active " + Describe (inTemplate.ActiveSource));
                    Check (inTemplate.Template == null, "no themed ControlTemplate leaks onto HackButton",
                        inTemplate.Template == null ? "Template null" : inTemplate.Template.GetType ().Name);
                    Check (VisualTreeHelper.GetChildrenCount (inTemplate) == 2, "art + content visual children",
                        VisualTreeHelper.GetChildrenCount (inTemplate).ToString ());
                }

                Check (repeatButton.NormalSource != null, "HackRepeatButton implicit Style applies", Describe (repeatButton.NormalSource));
                Check (toggleButton.NormalSource != null, "HackToggleButton implicit Style applies", Describe (toggleButton.NormalSource));
                Check (implicitButton.Template == null, "plain HackButton has no themed template",
                    implicitButton.Template == null ? "Template null" : implicitButton.Template.GetType ().Name);

                Say (_failures == 0 ? "all style checks passed" : _failures + " check(s) failed");
                try
                {
                    File.WriteAllLines ("styleprobe.log", Log.ToArray ());
                }
                catch (Exception ex)
                {
                    Say ("log write failed: {0}", ex.Message);
                }
                window.Close ();
            }
            return _failures == 0 ? 0 : 1;
        }

        private static void Pump (Window window, int milliseconds)
        {
            int deadline = Environment.TickCount + milliseconds;
            while (Environment.TickCount < deadline)
            {
                window.Dispatcher.Invoke (new Action (delegate { }), DispatcherPriority.SystemIdle);
                System.Threading.Thread.Sleep (10);
                window.UpdateLayout ();
            }
        }
    }
}
