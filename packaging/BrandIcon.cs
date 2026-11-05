// Deterministic Capico app icon. Compile with System.Drawing.dll; pass Assets as argument.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class BrandIcon
{
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[0]);
        using (var master = new Bitmap(1024, 1024, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(master))
            using (var background = new LinearGradientBrush(new Point(0, 0), new Point(1024, 1024),
                Color.FromArgb(24, 41, 65), Color.FromArgb(10, 19, 34)))
            using (var shape = new GraphicsPath())
            using (var stroke = new Pen(Color.FromArgb(105, 233, 212), 112))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                shape.AddArc(24, 24, 280, 280, 180, 90);
                shape.AddArc(720, 24, 280, 280, 270, 90);
                shape.AddArc(720, 720, 280, 280, 0, 90);
                shape.AddArc(24, 720, 280, 280, 90, 90);
                shape.CloseFigure();
                g.FillPath(background, shape);
                stroke.StartCap = stroke.EndCap = LineCap.Round;
                g.DrawArc(stroke, 260, 260, 504, 504, 43, 274);
            }
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            var frames = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            using (var frame = new Bitmap(sizes[i], sizes[i], PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(frame))
            using (var memory = new MemoryStream())
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(master, 0, 0, sizes[i], sizes[i]);
                frame.Save(memory, ImageFormat.Png);
                frames[i] = memory.ToArray();
                if (sizes[i] == 256) File.WriteAllBytes(Path.Combine(args[0], "CapicoVanity.png"), frames[i]);
            }
            using (var writer = new BinaryWriter(File.Create(Path.Combine(args[0], "CapicoVanity.ico"))))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write(frames[i].Length); writer.Write(offset);
                    offset += frames[i].Length;
                }
                foreach (var frame in frames) writer.Write(frame);
            }
        }
    }
}
