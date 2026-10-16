using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

/// <summary>生成多尺寸 app.ico（PNG 压缩条目）与预览图，无需任何外部素材。</summary>
internal static class IconGen
{
    private static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string icoPath = Path.Combine(dir, "app.ico");
        string previewPath = Path.Combine(dir, "icon-preview.png");

        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] images = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            images[i] = RenderPng(sizes[i]);
        }

        File.WriteAllBytes(previewPath, images[images.Length - 1]);

        using (FileStream stream = new FileStream(icoPath, FileMode.Create, FileAccess.Write))
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)sizes.Length);

                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    int dimension = sizes[i] >= 256 ? 0 : sizes[i];
                    writer.Write((byte)dimension);
                    writer.Write((byte)dimension);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)32);
                    writer.Write((uint)images[i].Length);
                    writer.Write((uint)offset);
                    offset += images[i].Length;
                }
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write(images[i]);
                }
            }
        }

        Console.WriteLine("app.ico 已生成: " + icoPath);
        return 0;
    }

    private static byte[] RenderPng(int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                float pad = size * 0.055f;
                RectangleF rect = new RectangleF(pad, pad, size - pad * 2f, size - pad * 2f);
                using (GraphicsPath background = RoundedRect(rect, size * 0.23f))
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    rect,
                    Color.FromArgb(255, 62, 146, 245),
                    Color.FromArgb(255, 18, 58, 150),
                    45f))
                {
                    g.FillPath(brush, background);
                }

                PointF[] speaker = new PointF[]
                {
                    new PointF(size * 0.20f, size * 0.42f),
                    new PointF(size * 0.31f, size * 0.42f),
                    new PointF(size * 0.45f, size * 0.27f),
                    new PointF(size * 0.45f, size * 0.73f),
                    new PointF(size * 0.31f, size * 0.58f),
                    new PointF(size * 0.20f, size * 0.58f)
                };
                using (SolidBrush white = new SolidBrush(Color.White))
                {
                    g.FillPolygon(white, speaker);
                }

                float penWidth = Math.Max(1f, size * 0.075f);
                using (Pen pen = new Pen(Color.White, penWidth))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    float cx = size * 0.47f;
                    float cy = size * 0.50f;
                    float[] radii = new float[] { 0.16f, 0.27f };
                    for (int i = 0; i < radii.Length; i++)
                    {
                        float r = size * radii[i];
                        g.DrawArc(pen, cx - r, cy - r, r * 2f, r * 2f, -52f, 104f);
                    }
                }
            }

            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }
}
