using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FanDropWin
{
    /// Glass fan overlay: a blurred snapshot of the screen behind it, frosted wedges,
    /// and cursor-angle hover tracking so the wedge lights up as soon as you enter it.
    class FanOverlay : Window
    {
        const double Size = 460, Inner = 66, Outer = 190, Pop = 12;
        static readonly Duration Anim = TimeSpan.FromMilliseconds(140);

        readonly Canvas canvas = new() { Width = Size, Height = Size };
        readonly Image backdrop = new() { Width = Size, Height = Size, Stretch = Stretch.Fill, IsHitTestVisible = false };
        readonly DispatcherTimer hover;
        readonly List<Slot> slots = new();
        List<FanAction> actions = new();
        FanMode mode;
        string[] files;
        ImageSource thumb;
        int highlighted = -1;
        public bool Busy;

        Image centerThumb; TextBlock centerText;

        class Slot
        {
            public Path Glass; public Canvas Group; public TranslateTransform Move; public ScaleTransform Scale;
            public Image Thumb; public TextBlock Text; public double Mid;
        }

        public FanOverlay()
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
            Width = Height = Size; AllowDrop = true; Content = canvas;
            backdrop.Effect = new BlurEffect { Radius = 28, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };

            SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(h, -20, Native.GetWindowLong(h, -20) | 0x08000000 | 0x80); // NOACTIVATE | TOOLWINDOW
            };
            hover = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Highlight(CursorWedge()), Dispatcher);
            IsVisibleChanged += (_, _) => { if (IsVisible) hover.Start(); else hover.Stop(); };

            DragEnter += (_, e) => { LoadFiles(e); e.Effects = DragDropEffects.Copy; e.Handled = true; };
            DragOver += (_, e) => { Highlight(CursorWedge()); e.Effects = DragDropEffects.Copy; e.Handled = true; };
            Drop += async (_, e) =>
            {
                int i = CursorWedge();
                if (files == null || i < 0 || i >= actions.Count) { Log.Write("drop missed any slot"); Hide(); return; }
                Busy = true; hover.Stop(); var a = actions[i];
                centerText.Text = a.Label + "\nâ€¦";
                try { var r = await a.Run(files); Log.Write(r); centerText.Text = r; }
                catch (Exception ex) { Log.Write("error: " + ex); centerText.Text = "Error â€“ see log"; }
                await System.Threading.Tasks.Task.Delay(900);
                Busy = false; Hide();
            };
        }

        public void ShowAt(int x, int y, FanMode m)
        {
            mode = m; files = null; thumb = null;
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = x / dpi.DpiScaleX - Size / 2; Top = y / dpi.DpiScaleY - Size / 2;
            backdrop.Source = CaptureScreen(x, y, (int)(Size * dpi.DpiScaleX), (int)(Size * dpi.DpiScaleY));
            Build(Actions.For(m, FileKind.Other), m.ToString());
            Opacity = 0; Show();
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, Anim));
        }

        /// Debug: render the fan for a file with one wedge hovered, to a PNG.
        public void RenderPreview(string file, int hot, string outPng)
        {
            mode = FanMode.Convert;
            backdrop.Source = CaptureScreen(700, 400, (int)Size, (int)Size);
            files = new[] { file }; thumb = Thumbnail.For(file);
            Build(Actions.For(mode, Actions.KindOf(file)), System.IO.Path.GetFileName(file));
            canvas.Measure(new System.Windows.Size(Size, Size)); canvas.Arrange(new Rect(0, 0, Size, Size));
            SetHot(slots[hot], true);
            foreach (var s in new[] { slots[hot] })
            {   // jump animations to their end state for the snapshot
                s.Move.BeginAnimation(TranslateTransform.XProperty, null); s.Move.X = Pop * Math.Cos(s.Mid);
                s.Move.BeginAnimation(TranslateTransform.YProperty, null); s.Move.Y = Pop * Math.Sin(s.Mid);
                s.Thumb.BeginAnimation(HeightProperty, null); s.Thumb.Height = 46;
                s.Thumb.BeginAnimation(OpacityProperty, null); s.Thumb.Opacity = 1;
            }
            canvas.UpdateLayout();
            var rtb = new RenderTargetBitmap((int)Size, (int)Size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(canvas);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = System.IO.File.Create(outPng); enc.Save(fs);
        }

        static BitmapSource CaptureScreen(int cx, int cy, int w, int h)
        {
            try
            {
                using var bmp = new System.Drawing.Bitmap(w, h);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                    g.CopyFromScreen(cx - w / 2, cy - h / 2, 0, 0, new System.Drawing.Size(w, h));
                var hb = bmp.GetHbitmap();
                try
                {
                    var src = Imaging.CreateBitmapSourceFromHBitmap(hb, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze(); return src;
                }
                finally { DeleteObject(hb); }
            }
            catch (Exception ex) { Log.Write("capture failed: " + ex.Message); return null; }
        }
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);

        void LoadFiles(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) { Log.Write("drag without files; types: " + string.Join(",", e.Data.GetFormats())); return; }
            var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length == dropped.Length && files[0] == dropped[0]) return;
            files = dropped;
            thumb = Thumbnail.For(files[0]);
            var kind = Actions.KindOf(files[0]);
            var list = Actions.For(mode, kind);
            var label = System.IO.Path.GetFileName(files[0]) + (files.Length > 1 ? $"  +{files.Length - 1}" : "");
            if (list.Count == 0) { list = Actions.For(FanMode.Tools, kind); label = $"No {mode.ToString().ToLower()} for {kind} Â· Tools\n" + label; }
            Build(list, label);
        }

        // ---------- layout ----------

        void Build(List<FanAction> list, string label)
        {
            actions = list; canvas.Children.Clear(); slots.Clear(); highlighted = -1;

            // Invisible full-circle surface so drag events fire everywhere inside the fan.
            var hit = new Ellipse { Width = Size, Height = Size, Fill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
            canvas.Children.Add(hit);

            int n = Math.Max(list.Count, 1);
            double sweep = 360.0 / n, gap = 2.5;
            var clip = new GeometryGroup { FillRule = FillRule.Nonzero };
            var geoms = new List<Geometry>();
            for (int i = 0; i < list.Count; i++)
            {
                double a0 = -90 + i * sweep + gap / 2, a1 = -90 + (i + 1) * sweep - gap / 2;
                var g = WedgeGeometry(a0, a1); geoms.Add(g); clip.Children.Add(g);
            }
            clip.Children.Add(new EllipseGeometry(new Point(Size / 2, Size / 2), Inner - 6, Inner - 6));

            // Frosted backdrop: blurred screen + dark tint, clipped to the fan's shape.
            var frost = new Grid { Width = Size, Height = Size, Clip = clip, IsHitTestVisible = false };
            if (backdrop.Parent is Panel old) old.Children.Remove(backdrop);
            frost.Children.Add(backdrop);
            frost.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(70, 18, 20, 28)) });
            canvas.Children.Add(frost);

            for (int i = 0; i < list.Count; i++)
            {
                double a0 = -90 + i * sweep + gap / 2, a1 = -90 + (i + 1) * sweep - gap / 2;
                double mid = (a0 + a1) / 2 * Math.PI / 180, r = (Inner + Outer) / 2 + 6;
                var s = new Slot { Mid = mid, Move = new TranslateTransform(), Scale = new ScaleTransform(1, 1, Size / 2, Size / 2) };
                s.Group = new Canvas { Width = Size, Height = Size, IsHitTestVisible = false, RenderTransform = new TransformGroup { Children = { s.Scale, s.Move } } };
                s.Glass = new Path { Data = geoms[i], Fill = GlassFill(false), Stroke = GlassStroke(false), StrokeThickness = 1.2 };
                s.Group.Children.Add(s.Glass);

                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                s.Thumb = new Image { Source = thumb, Width = 46, Height = 46, Margin = new Thickness(0, 0, 0, 4), Opacity = 0, Stretch = Stretch.Uniform,
                    Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.5 } };
                s.Thumb.Height = 0; // collapsed until hovered
                s.Text = new TextBlock { Text = list[i].Label, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center,
                    Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.7 } };
                stack.Children.Add(s.Thumb); stack.Children.Add(s.Text);
                var host = new Grid { Width = 110, Height = 90 };
                stack.VerticalAlignment = VerticalAlignment.Center; host.Children.Add(stack);
                Canvas.SetLeft(host, Size / 2 + r * Math.Cos(mid) - 55); Canvas.SetTop(host, Size / 2 + r * Math.Sin(mid) - 45);
                s.Group.Children.Add(host);
                slots.Add(s); canvas.Children.Add(s.Group);
            }

            // Center glass disc with file preview.
            var disc = new Ellipse { Width = (Inner - 6) * 2, Height = (Inner - 6) * 2, Fill = GlassFill(false), Stroke = GlassStroke(false), StrokeThickness = 1.2, IsHitTestVisible = false };
            Canvas.SetLeft(disc, Size / 2 - Inner + 6); Canvas.SetTop(disc, Size / 2 - Inner + 6);
            canvas.Children.Add(disc);
            var cstack = new StackPanel { Width = 110, IsHitTestVisible = false };
            centerThumb = new Image { Source = thumb, Width = 44, Height = thumb != null ? 44 : 0, Margin = new Thickness(0, 0, 0, 3), Stretch = Stretch.Uniform };
            centerText = new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
                MaxHeight = 44, TextTrimming = TextTrimming.CharacterEllipsis, Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 0, Opacity = 0.7 } };
            var modeText = new TextBlock { Text = mode.ToString().ToUpper(), Foreground = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)), FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 2) };
            cstack.Children.Add(modeText); cstack.Children.Add(centerThumb); cstack.Children.Add(centerText);
            cstack.Measure(new System.Windows.Size(110, double.PositiveInfinity));
            Canvas.SetLeft(cstack, Size / 2 - 55); Canvas.SetTop(cstack, Size / 2 - cstack.DesiredSize.Height / 2);
            canvas.Children.Add(cstack);
        }

        static Brush GlassFill(bool hot)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            if (hot)
            {
                b.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 255, 255), 0));
                b.GradientStops.Add(new GradientStop(Color.FromArgb(70, 60, 150, 255), 1));
            }
            else
            {
                b.GradientStops.Add(new GradientStop(Color.FromArgb(70, 255, 255, 255), 0));
                b.GradientStops.Add(new GradientStop(Color.FromArgb(22, 255, 255, 255), 1));
            }
            return b;
        }

        static Brush GlassStroke(bool hot)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(hot ? 240 : 150), 255, 255, 255), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(hot ? 120 : 40), 255, 255, 255), 1));
            return b;
        }

        // ---------- hover ----------

        void Highlight(int i)
        {
            if (i == highlighted || Busy) return;
            if (highlighted >= 0 && highlighted < slots.Count) SetHot(slots[highlighted], false);
            highlighted = i;
            if (i >= 0 && i < slots.Count) SetHot(slots[i], true);
        }

        void SetHot(Slot s, bool hot)
        {
            s.Glass.Fill = GlassFill(hot);
            s.Glass.Stroke = GlassStroke(hot);
            s.Glass.StrokeThickness = hot ? 2 : 1.2;
            s.Glass.Effect = hot ? new DropShadowEffect { Color = Color.FromRgb(80, 160, 255), BlurRadius = 20, ShadowDepth = 0, Opacity = 0.75 } : null;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            s.Move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(hot ? Pop * Math.Cos(s.Mid) : 0, Anim) { EasingFunction = ease });
            s.Move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(hot ? Pop * Math.Sin(s.Mid) : 0, Anim) { EasingFunction = ease });
            s.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(hot ? 1.04 : 1, Anim) { EasingFunction = ease });
            s.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(hot ? 1.04 : 1, Anim) { EasingFunction = ease });
            bool showThumb = hot && thumb != null;
            s.Thumb.Source = thumb;
            s.Thumb.BeginAnimation(HeightProperty, new DoubleAnimation(showThumb ? 46 : 0, Anim) { EasingFunction = ease });
            s.Thumb.BeginAnimation(OpacityProperty, new DoubleAnimation(showThumb ? 1 : 0, Anim));
            s.Text.FontWeight = hot ? FontWeights.Bold : FontWeights.SemiBold;
        }

        /// Wedge under the cursor, by angle only: anywhere past the center disc counts.
        int CursorWedge()
        {
            if (actions.Count == 0 || !IsVisible) return -1;
            Native.GetCursorPos(out var sp);
            Point p;
            try { p = canvas.PointFromScreen(new Point(sp.X, sp.Y)); } catch { return -1; }
            double dx = p.X - Size / 2, dy = p.Y - Size / 2, d = Math.Sqrt(dx * dx + dy * dy);
            if (d < Inner - 6 || d > Size / 2 + 40) return -1;
            double ang = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
            return Math.Min((int)(ang / (360.0 / actions.Count)), actions.Count - 1);
        }

        static Geometry WedgeGeometry(double a0, double a1)
        {
            Point P(double r, double a) => new(Size / 2 + r * Math.Cos(a * Math.PI / 180), Size / 2 + r * Math.Sin(a * Math.PI / 180));
            bool large = a1 - a0 > 180;
            var f = new PathFigure { StartPoint = P(Inner, a0), IsClosed = true };
            f.Segments.Add(new LineSegment(P(Outer, a0), true));
            f.Segments.Add(new ArcSegment(P(Outer, a1), new System.Windows.Size(Outer, Outer), 0, large, SweepDirection.Clockwise, true));
            f.Segments.Add(new LineSegment(P(Inner, a1), true));
            f.Segments.Add(new ArcSegment(P(Inner, a0), new System.Windows.Size(Inner, Inner), 0, large, SweepDirection.Counterclockwise, true));
            var g = new PathGeometry(new[] { f }); g.Freeze(); return g;
        }
    }
}

