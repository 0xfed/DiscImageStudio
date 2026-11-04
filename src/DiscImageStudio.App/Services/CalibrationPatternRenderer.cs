using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiscImageStudio.Core;
using DiscImageStudio.Localization;
using DiscImageStudio.Core.Calibration;

namespace DiscImageStudio.Services;

internal static class CalibrationPatternRenderer
{
    internal static BitmapSource Render(CalibrationTarget target, double canvasRadius, int size,
        CalibrationParameters? actual = null, double rotation = 0, CancellationToken cancellationToken = default)
    {
        Func<CalibrationPoint, CalibrationPoint?>? inverse = actual is null
            ? null : target.CreateInverseMapping(actual, rotation);
        var bounds = actual ?? target.Parameters;
        // Canvas size controls the physical background only. Target.Sample owns
        // the source-image scale, which is captured in its generation parameters.
        byte[] pixels = new byte[checked(size * size)];
        double step = 2 * canvasRadius / size;
        Parallel.For(0, size, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            double ym = (y + 0.5) * step - canvasRadius;
            for (int x = 0; x < size; x++)
            {
                double xm = (x + 0.5) * step - canvasRadius;
                double r2 = xm * xm + ym * ym;
                byte value = 255;
                if (r2 >= bounds.InnerRadiusMm * bounds.InnerRadiusMm - 1e-10 && r2 <= bounds.OuterRadiusMm * bounds.OuterRadiusMm + 1e-10)
                {
                    CalibrationPoint? p = inverse is null ? new(xm, ym) : inverse(new(xm, ym));
                    if (p is not null)
                    {
                        value = target.VisualSelector is { } selector
                            ? selector.SamplePreview(p.X, p.Y, step)
                            : BinaryGrayscale.Quantize(target.Sample(p.X, p.Y));
                    }
                }
                pixels[y * size + x] = value;
            }
        });
        BitmapSource image = BitmapSource.Create(size, size, 96, 96, PixelFormats.Gray8, null, pixels, size);
        image.Freeze();
        return target.VisualSelector is { } visualSelector
            ? AddVisualSelectorLabels(image, visualSelector, canvasRadius, size) : image;
    }

    // These labels are preview guides, never part of the encoded payload. Fan the
    // labels out so all 21 thin bands remain identifiable without overlapping text.
    private static BitmapSource AddVisualSelectorLabels(BitmapSource image,
        CalibrationVisualSelector selector, double canvasRadius, int size)
    {
        var visual = new DrawingVisual();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawImage(image, new Rect(0, 0, size, size));
            double scale = size / (2 * canvasRadius);
            var textBrush = new SolidColorBrush(Color.FromRgb(15, 48, 80));
            var background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
            var outline = new Pen(textBrush, 1);
            foreach (CalibrationVisualBand band in selector.Bands)
            {
                double angle = (band.Bank == "I" ? -150 : 30) + (band.Index - 1) * 6;
                double radians = angle * Math.PI / 180;
                double x = size / 2.0 + band.CenterRadiusMm * Math.Cos(radians) * scale;
                double y = size / 2.0 + band.CenterRadiusMm * Math.Sin(radians) * scale;
                var label = new FormattedText(band.Bank + band.Index.ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"),
                    Math.Max(10, size / 75.0), textBrush, 1);
                var box = new Rect(x - label.Width / 2 - 2, y - label.Height / 2,
                    label.Width + 4, label.Height);
                drawing.DrawRoundedRectangle(background, outline, box, 2, 2);
                drawing.DrawText(label, new Point(x - label.Width / 2, y - label.Height / 2));
            }
        }
        var result = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    /// <summary>Original PNG pixels cropped in the ordinary encoder's image coordinates.</summary>
    internal static BitmapSource RenderEncodingImage(CalibrationTarget target, CancellationToken cancellationToken = default)
    {
        if (target.IsVisualSelectorPattern)
            throw new ArgumentException(L.T("Visual selector PNG export is unavailable. Generate its ISO or use streaming burn."), nameof(target));
        const int size = CalibrationReferencePattern.PixelSize;
        bool generatedPattern = target.Options.PatternKind != CalibrationPatternKind.FineCross;
        byte[] pixels = generatedPattern
            ? new byte[size * size] : CalibrationReferencePattern.ReadGrayPixels();
        CalibrationParameters parameters = target.Parameters;
        double inner2 = target.DrawingInnerRadiusMm * target.DrawingInnerRadiusMm;
        double outer2 = target.DrawingOuterRadiusMm * target.DrawingOuterRadiusMm;
        Parallel.For(0, size, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < size; x++)
            {
                CalibrationPoint p = CalibrationReferencePattern.PixelToDisc(x, y, parameters.OuterRadiusMm, parameters.Kind);
                double r2 = p.X * p.X + p.Y * p.Y;
                if (generatedPattern) pixels[y * size + x] = target.Sample(p.X, p.Y);
                else if (r2 < inner2 - 1e-10 || r2 > outer2 + 1e-10) pixels[y * size + x] = 255;
            }
        });
        BitmapSource image = BitmapSource.Create(size, size, 96, 96, PixelFormats.Gray8, null, pixels, size);
        image.Freeze();
        return image;
    }

    internal static void SavePng(BitmapSource image, string path)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }
}
