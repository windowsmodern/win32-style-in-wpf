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
    /// Regression probe for the "hover image stays on screen" bug: when the resting image is
    /// null and the state image is set, entering the state assigns it directly (nothing to fade
    /// from), and leaving it has to fade that image out again.  The attached property's default
    /// is null, so the fade-out used to raise no change notification at all and the image stuck.
    /// </summary>
    internal static class NullTransitionProbe
    {
        private const string Switch = "--nulltransitionprobe";

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

        private static BitmapSource Art ()
        {
            var visual = new DrawingVisual ();
            using (DrawingContext dc = visual.RenderOpen ())
                dc.DrawRectangle (Brushes.Orange, null, new Rect (0, 0, 24, 24));
            var bitmap = new RenderTargetBitmap (24, 24, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render (visual);
            bitmap.Freeze ();
            return bitmap;
        }

        private static HackImage ArtOf (HackButton button)
        {
            object chrome = typeof (HackButton).GetField ("_chrome", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue (button);
            return (HackImage)chrome.GetType ().GetProperty ("Art", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue (chrome, null);
        }

        private static void SetReadOnly (DependencyObject element, FieldInfo keyField, bool value)
        {
            var key = (DependencyPropertyKey)keyField.GetValue (null);
            element.SetValue (key, value);
        }

        /// <summary>
        /// The state change HackButton.UpdateArt performs.  Called through reflection so the probe
        /// reproduces a single state transition (the button's two watchers can call it twice, which
        /// masks the bug by leaving the attached To property set).
        /// </summary>
        private static void SetState (HackButton button, ImageSource source, TimeSpan duration)
        {
            object chrome = typeof (HackButton).GetField ("_chrome", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue (button);
            chrome.GetType ().GetMethod ("SetSource", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke (chrome, new object[] { source, new Duration (duration), null });
        }

        private static void Pump (Window window, int milliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds (milliseconds);
            while (DateTime.UtcNow < deadline)
            {
                var frame = new System.Windows.Threading.DispatcherFrame ();
                var timer = new System.Windows.Threading.DispatcherTimer (System.Windows.Threading.DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds (10)
                };
                timer.Tick += delegate (object sender, EventArgs e)
                {
                    ((System.Windows.Threading.DispatcherTimer)sender).Stop ();
                    frame.Continue = false;
                };
                timer.Start ();
                System.Windows.Threading.Dispatcher.PushFrame (frame);
                window.UpdateLayout ();
            }
        }

        /// <summary>Non-transparent pixels in a fresh render of the button's art layer.</summary>
        private static int VisibleArtPixels (HackButton button)
        {
            var art = ArtOf (button);
            double w = art.ActualWidth, h = art.ActualHeight;
            if (w <= 0 || h <= 0)
                return -1;

            var shot = new RenderTargetBitmap ((int)Math.Ceiling (w), (int)Math.Ceiling (h), 96, 96, PixelFormats.Pbgra32);
            shot.Render (art);
            var pixels = new byte[shot.PixelWidth * shot.PixelHeight * 4];
            shot.CopyPixels (pixels, shot.PixelWidth * 4, 0);
            int visible = 0;
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0)
                    visible++;
            }
            return visible;
        }

        /// <summary>Pixels that are visible but not fully opaque (the cross-fade itself).</summary>
        private static int PartialPixels (BitmapSource bitmap)
        {
            if (bitmap == null)
                return -1;
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels (pixels, bitmap.PixelWidth * 4, 0);
            int count = 0;
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels [i] > 0 && pixels [i] < 255)
                    count++;
            }
            return count;
        }

        public static int Run (bool transition)
        {
            _failures = 0;
            Log.Clear ();
            Say ("Null resting image + transition  (theme: {0})", transition ? "transition" : "plain");

            // Only one state image is set, exactly like the reported Normal=null / Hover=image case.
            var button = new HackButton
            {
                Width = 90,
                Height = 48,
                NormalSource = null,
                ActiveSource = Art (),
                HackThickness = new Thickness (3),
                TransitionDuration = new Duration (TimeSpan.FromMilliseconds (200))
            };
            var window = new Window { Width = 200, Height = 120, Content = button };
            window.Show ();
            Pump (window, 200);

            HackImage art = ArtOf (button);
            Check (art.Source == null, "rest: no resting image", art.Source == null ? "null" : "set");
            Check (VisibleArtPixels (button) == 0, "rest: nothing painted", VisibleArtPixels (button).ToString ());

            TimeSpan duration = TimeSpan.FromMilliseconds (200);
            SetState (button, button.ActiveSource, duration);
            Pump (window, 60);
            Check (ReferenceEquals (art.Source, button.ActiveSource), "pressed: state image applied directly",
                art.Source == null ? "null" : "set");
            Check (VisibleArtPixels (button) > 0, "pressed: art visible", VisibleArtPixels (button).ToString ());

            SetState (button, null, duration);
            Pump (window, 80);
            Check (HackImageTransitions.GetIsRunning (art), "released: fade-out is running", "IsRunning=" + HackImageTransitions.GetIsRunning (art));
            Pump (window, 400);
            Check (art.Source == null && !HackImageTransitions.GetIsRunning (art), "released: source cleared after the fade",
                art.Source == null ? "null" : "set");
            int residual = VisibleArtPixels (button);
            Check (residual == 0, "released: no pixels left behind", residual.ToString ());

            // Fast in/out/in/out: the interrupted transitions must not write a stale target back.
            for (int i = 0; i < 3; i++)
            {
                SetState (button, button.ActiveSource, duration);
                Pump (window, 15);
                SetState (button, null, duration);
                Pump (window, 15);
            }
            Pump (window, 500);
            Check (art.Source == null && !HackImageTransitions.GetIsRunning (art), "rapid flicks: ends empty",
                art.Source == null ? "null" : "set");
            residual = VisibleArtPixels (button);
            Check (residual == 0, "rapid flicks: no pixels left behind", residual.ToString ());

            // null <-> image, interrupted: leaving during a fade-in must start a fade-out, not a snap.
            SetState (button, button.ActiveSource, duration);
            Pump (window, 20);
            SetState (button, null, duration);
            Pump (window, 60);
            Check (HackImageTransitions.GetIsRunning (art), "fade-out is running before the re-entry", string.Empty);
            SetState (button, button.ActiveSource, duration);
            Pump (window, 60);
            Check (HackImageTransitions.GetIsRunning (art), "re-entry restarts a fade from the interrupted frame", string.Empty);
            SetState (button, null, duration);
            Check (HackImageTransitions.GetIsRunning (art), "leaving during a fade-in starts a fade-out", string.Empty);
            Pump (window, 600);
            Check (art.Source == null && !HackImageTransitions.GetIsRunning (art), "interrupted null<->image sequence ends empty",
                art.Source == null ? "null" : "set");
            int rapidResidual = VisibleArtPixels (button);
            Check (rapidResidual == 0, "interrupted null<->image sequence leaves no pixels", rapidResidual.ToString ());

            // Switching the target while a fade is running must restart from the current frame,
            // otherwise the old target wins and the image looks stuck.
            var switchHost = new HackImage { Width = 40, Height = 40 };
            var switchWindow = new Window { Width = 140, Height = 120, Content = switchHost };
            switchWindow.Show ();
            Pump (switchWindow, 100);

            BitmapSource a = Art (), b = Art (), c = Art (), d = Art ();
            HackImageTransitions.SetDuration (switchHost, new Duration (TimeSpan.FromMilliseconds (250)));
            switchHost.Source = a;
            HackImageTransitions.SetTo (switchHost, b);
            Pump (switchWindow, 60);
            HackImageTransitions.SetTo (switchHost, c);
            Pump (switchWindow, 600);
            Check (ReferenceEquals (switchHost.Source, c), "switching the target mid-fade ends on the latest image",
                switchHost.Source == null ? "null" : (ReferenceEquals (switchHost.Source, b) ? "old target" : "other"));

            // A repeated SetTo(null) must not restart the fade-out (it would extend it).
            HackImageTransitions.SetDuration (switchHost, new Duration (TimeSpan.FromMilliseconds (400)));
            switchHost.Source = d;
            HackImageTransitions.SetTo (switchHost, null);
            Pump (switchWindow, 100);
            HackImageTransitions.SetTo (switchHost, null);
            Pump (switchWindow, 350);
            Check (switchHost.Source == null, "a repeated fade-out is not restarted",
                switchHost.Source == null ? "null" : "still showing an image");
            // A null side must behave like a fully transparent image of the other side: the fade
            // itself has to be visible (partial alpha), not an abrupt appearance/disappearance.
            HackImageTransitions.SetDuration (switchHost, new Duration (TimeSpan.FromMilliseconds (400)));
            switchHost.Source = null;
            HackImageTransitions.SetTo (switchHost, a);
            Pump (switchWindow, 150);
            int fadeIn = PartialPixels (switchHost.StretchedImage);
            Check (fadeIn > 0, "null -> image is visible mid-fade", "partial pixels=" + fadeIn);
            Pump (switchWindow, 500);

            HackImageTransitions.SetTo (switchHost, null);
            Pump (switchWindow, 150);
            int fadeOut = PartialPixels (switchHost.StretchedImage);
            Check (fadeOut > 0, "image -> null is visible mid-fade", "partial pixels=" + fadeOut);
            Pump (switchWindow, 500);

            switchWindow.Close ();

            Say (_failures == 0 ? "all null-transition checks passed" : _failures + " check(s) failed");
            try
            {
                File.WriteAllLines ("nulltransitionprobe.log", Log.ToArray ());
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
