using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Windows.Themes;

namespace ThemeDemo
{
    /// <summary>
    /// Verifies HackImage.StretchedImage (lazy, weak-cached, alpha preserving) and the
    /// SourceChanged event, on a synthetic nine-patch and on a real UxTheme part.
    /// </summary>
    internal static class StretchedProbe
    {
        private const string Switch = "--stretchedprobe";

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

        private static void Paint (DrawingContext dc, int x, int y, int w, int h, Color color)
        {
            dc.DrawRectangle (new SolidColorBrush (color), null, new Rect (x, y, w, h));
        }

        /// <summary>11 x 11 nine-patch, slice 3, one colour (and alpha) per region.</summary>
        private static BitmapSource MakeArt ()
        {
            var visual = new DrawingVisual ();
            using (DrawingContext dc = visual.RenderOpen ())
            {
                Paint (dc, 0, 0, 3, 3, Color.FromArgb (0, 0, 0, 0));            // top-left: transparent
                Paint (dc, 3, 0, 5, 3, Colors.Blue);                           // top
                Paint (dc, 8, 0, 3, 3, Colors.Lime);                           // top-right
                Paint (dc, 0, 3, 3, 5, Colors.Yellow);                         // left
                Paint (dc, 3, 3, 5, 5, Color.FromArgb (128, 255, 0, 255));     // centre: half transparent
                Paint (dc, 8, 3, 3, 5, Colors.Cyan);                           // right
                Paint (dc, 0, 8, 3, 3, Colors.Black);                          // bottom-left
                Paint (dc, 3, 8, 5, 3, Colors.White);                          // bottom
                Paint (dc, 8, 8, 3, 3, Colors.Gray);                           // bottom-right
            }

            var bitmap = new RenderTargetBitmap (11, 11, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render (visual);
            bitmap.Freeze ();
            return bitmap;
        }

        private static Color Pixel (BitmapSource bitmap, int x, int y)
        {
            var buffer = new byte [4];
            bitmap.CopyPixels (new Int32Rect (x, y, 1, 1), buffer, 4, 0);
            return Color.FromArgb (buffer [3], buffer [2], buffer [1], buffer [0]);
        }

        private static string Describe (BitmapSource bitmap)
        {
            if (bitmap == null)
                return "null";
            return string.Format ("{0}x{1} px @ {2:0.##} dpi, {3}", bitmap.PixelWidth, bitmap.PixelHeight,
                bitmap.DpiX, bitmap.Format);
        }

        private static string Format (Color color)
        {
            return string.Format ("#{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B);
        }

        private static double DpiScale (Visual visual)
        {
            PresentationSource source = PresentationSource.FromVisual (visual);
            if (source == null || source.CompositionTarget == null)
                return 1.0;
            return source.CompositionTarget.TransformToDevice.M11;
        }

        private static void Pump (Window window, int milliseconds)
        {
            int deadline = Environment.TickCount + milliseconds;
            while (Environment.TickCount < deadline)
            {
                window.Dispatcher.Invoke (new Action (delegate { }), System.Windows.Threading.DispatcherPriority.SystemIdle);
                System.Threading.Thread.Sleep (10);
                window.UpdateLayout ();
            }
        }

        private static void Save (BitmapSource bitmap, string path)
        {
            if (bitmap == null)
                return;
            var encoder = new PngBitmapEncoder ();
            encoder.Frames.Add (BitmapFrame.Create (bitmap));
            using (var stream = File.Create (path))
                encoder.Save (stream);
            Say ("  image: {0}", path);
        }

        public static int Run (bool transition)
        {
            _failures = 0;
            Log.Clear ();
            Say ("HackImage.StretchedImage / SourceChanged  (theme: {0})", transition ? "transition" : "plain");

            BitmapSource art = MakeArt ();
            var image = new HackImage ();
            int changes = 0;
            image.SourceChanged += delegate { changes++; };

            Say ("lazy + cache");
            Check (image.StretchedImage == null, "never laid out -> null", string.Empty);
            image.Source = art;
            Check (changes == 1, "setting Source raises SourceChanged", "changes=" + changes);
            image.HackThickness = new Thickness (3);
            image.Measure (new Size (120, 40));
            image.Arrange (new Rect (0, 0, 120, 40));

            BitmapSource first = image.StretchedImage;
            Check (first != null && first.PixelWidth == 120 && first.PixelHeight == 40,
                "default DPI -> 120x40", Describe (first));
            Check (first != null && first.Format == PixelFormats.Pbgra32, "format keeps alpha (Pbgra32)",
                first == null ? "null" : first.Format.ToString ());
            Check (first != null && first.IsFrozen, "result is frozen", string.Empty);
            Check (changes == 1, "thickness and layout do not raise SourceChanged", "changes=" + changes);

            Say ("pixels (120x40, slice 3)");
            string[] labels = { "top-left (transparent)", "top", "top-right", "left", "centre (A=128)", "right", "bottom-left", "bottom", "bottom-right" };
            int[] xs = { 1, 60, 118, 1, 60, 118, 1, 60, 118 };
            int[] ys = { 1, 1, 1, 20, 20, 20, 38, 38, 38 };
            Color[] expected =
            {
                Color.FromArgb (0, 0, 0, 0), Colors.Blue, Colors.Lime, Colors.Yellow,
                // Pbgra32 stores premultiplied colour: 50% magenta is (128, 128, 0, 128) in A,R,G,B.
                Color.FromArgb (128, 128, 0, 128), Colors.Cyan, Colors.Black, Colors.White, Colors.Gray
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Color actual = Pixel (first, xs [i], ys [i]);
                bool ok = actual.A == expected [i].A &&
                    (expected [i].A == 0 || (actual.R == expected [i].R && actual.G == expected [i].G && actual.B == expected [i].B));
                Check (ok, labels [i], Format (actual) + " expected " + Format (expected [i]));
            }

            Check (ReferenceEquals (first, image.StretchedImage), "second read returns the cached instance", string.Empty);

            Say ("invalidation");
            image.Arrange (new Rect (0, 0, 200, 40));
            BitmapSource wide = image.StretchedImage;
            Check (!ReferenceEquals (wide, first) && wide.PixelWidth == 200 && wide.PixelHeight == 40,
                "resize drops the cache -> 200x40", Describe (wide));
            Check (first.PixelWidth == 120 && first.IsFrozen, "the old bitmap stays valid", Describe (first));

            BitmapSource hidpi = image.GetStretchedImage (192);
            Check (hidpi != null && hidpi.PixelWidth == 400 && hidpi.PixelHeight == 80 && Math.Abs (hidpi.DpiX - 192) < 0.01,
                "GetStretchedImage(192) -> 400x80 @192", Describe (hidpi));
            BitmapSource back = image.StretchedImage;
            Check (!ReferenceEquals (back, hidpi) && back.PixelWidth == 200,
                "one-entry cache returns to the screen DPI", Describe (back));

            image.InvalidateStretchedImage ();
            Check (!ReferenceEquals (image.StretchedImage, back), "InvalidateStretchedImage forces a rebuild", string.Empty);

            Say ("SourceChanged scope");
            image.HackThickness = new Thickness (4);
            image.Arrange (new Rect (0, 0, 260, 40));
            image.SetSliceImages (new BitmapSource [9]);
            Check (changes == 1, "thickness, resize and SetSliceImages stay silent", "changes=" + changes);
            image.Source = null;
            Check (changes == 2, "Source = null raises SourceChanged", "changes=" + changes);
            Check (image.StretchedImage == null, "no source -> StretchedImage null", string.Empty);
            image.Source = art;
            image.SetCurrentValue (HackImage.SourceProperty, null);
            Check (changes == 4, "SetCurrentValue (transition path) raises SourceChanged", "changes=" + changes);

            Save (wide, Path.Combine (Directory.GetCurrentDirectory (), "stretched-9patch.png"));

            Say ("real UxTheme part");
            var themed = new HackImage
            {
                Width = 160,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            int themedChanges = 0;
            themed.SourceChanged += delegate { themedChanges++; };
            ThemeImage.SetClass (themed, "Button");
            ThemeImage.SetPart (themed, 1);
            ThemeImage.SetState (themed, 1);

            var root = new Grid ();
            root.Children.Add (themed);
            var window = new Window
            {
                Title = "StretchedImage probe",
                Width = 360,
                Height = 140,
                Content = root
            };
            window.Show ();
            Pump (window, 900);

            Check (themed.Source != null, "theme part loaded", themed.Source == null ? "Source null" : Describe (themed.Source as BitmapSource));
            Check (themedChanges >= 1, "theme part raises SourceChanged", "changes=" + themedChanges);

            double dpi = DpiScale (window);
            BitmapSource themedStretched = themed.StretchedImage;
            int expectedWidth = Math.Max (1, (int)Math.Round (themed.ActualWidth * dpi));
            int expectedHeight = Math.Max (1, (int)Math.Round (themed.ActualHeight * dpi));
            Check (themedStretched != null && themedStretched.PixelWidth == expectedWidth && themedStretched.PixelHeight == expectedHeight,
                string.Format ("stretched at screen DPI ({0:0.##}x) -> {1}x{2}", dpi, expectedWidth, expectedHeight),
                Describe (themedStretched));

            if (themedStretched != null && themed.Source != null)
            {
                int cx = themedStretched.PixelWidth / 2;
                int cy = themedStretched.PixelHeight - 4;
                Color stretchedPixel = Pixel (themedStretched, cx, cy);

                var shot = new RenderTargetBitmap (
                    Math.Max (1, (int)Math.Ceiling (root.ActualWidth * dpi)),
                    Math.Max (1, (int)Math.Ceiling (root.ActualHeight * dpi)),
                    96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
                shot.Render (root);
                Point origin = themed.TransformToAncestor (root).Transform (new Point (0, 0));
                int sx = (int)Math.Round ((origin.X + themed.ActualWidth / 2) * dpi);
                int sy = (int)Math.Round ((origin.Y + themed.ActualHeight - 4 / dpi) * dpi);
                Color screenPixel = (sx >= 0 && sy >= 0 && sx < shot.PixelWidth && sy < shot.PixelHeight)
                    ? Pixel (shot, sx, sy)
                    : Colors.Transparent;
                bool close = Math.Abs (stretchedPixel.A - screenPixel.A) <= 2 &&
                    Math.Abs (stretchedPixel.R - screenPixel.R) <= 2 &&
                    Math.Abs (stretchedPixel.G - screenPixel.G) <= 2 &&
                    Math.Abs (stretchedPixel.B - screenPixel.B) <= 2;
                Check (close, "pixels match what the window renders", "stretched " + Format (stretchedPixel) + " vs screen " + Format (screenPixel));
            }

            Save (themedStretched, Path.Combine (Directory.GetCurrentDirectory (), "stretched-button.png"));

            BitmapSource beforeClose = themed.StretchedImage;
            window.Close ();
            Check (!ReferenceEquals (beforeClose, themed.StretchedImage), "Unloaded drops the cached bitmap", string.Empty);

            Say (_failures == 0 ? "all stretched-image checks passed" : _failures + " check(s) failed");
            try
            {
                File.WriteAllLines ("stretchedprobe.log", Log.ToArray ());
            }
            catch (Exception ex)
            {
                Say ("log write failed: {0}", ex.Message);
            }
            return _failures == 0 ? 0 : 1;
        }
    }
}
