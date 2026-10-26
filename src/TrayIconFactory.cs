using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace MouseBatteryDisplay
{
    internal static class TrayIconFactory
    {
        private static readonly int[] IconSizes = new int[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

        private static readonly string[][] DigitGlyphs = new string[][]
        {
            new string[] { "111", "101", "101", "101", "111" },
            new string[] { "010", "110", "010", "010", "111" },
            new string[] { "111", "001", "111", "100", "111" },
            new string[] { "111", "001", "111", "001", "111" },
            new string[] { "101", "101", "111", "001", "001" },
            new string[] { "111", "100", "111", "001", "111" },
            new string[] { "111", "100", "111", "101", "111" },
            new string[] { "111", "001", "001", "010", "010" },
            new string[] { "111", "101", "111", "101", "111" },
            new string[] { "111", "101", "111", "001", "111" }
        };

        internal static Icon CreateIcon(int? percent, bool charging, bool connected, bool stale)
        {
            List<Bitmap> frames = new List<Bitmap>();
            try
            {
                foreach (int size in IconSizes)
                {
                    frames.Add(DrawBadge(size, percent, charging, connected, stale));
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    WriteIco(stream, frames);
                    stream.Position = 0;
                    using (Icon temporary = new Icon(stream))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
            }
            finally
            {
                foreach (Bitmap frame in frames)
                {
                    frame.Dispose();
                }
            }
        }

        internal static void SaveIco(string path, int? percent, bool charging, bool connected, bool stale)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            List<Bitmap> frames = new List<Bitmap>();
            try
            {
                foreach (int size in IconSizes)
                {
                    frames.Add(DrawBadge(size, percent, charging, connected, stale));
                }
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    WriteIco(stream, frames);
                }
            }
            finally
            {
                foreach (Bitmap frame in frames)
                {
                    frame.Dispose();
                }
            }
        }

        private static Bitmap DrawBadge(int size, int? percent, bool charging, bool connected, bool stale)
        {
            Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int scale = Math.Max(1, (int)Math.Round(size / 16.0));
                int pad = Math.Max(1, size / 32);
                float radius = Math.Max(2f, size * 0.18f);

                Color accent = AccentColor(percent, charging, connected, stale);
                Color border = charging
                    ? Color.FromArgb(235, 56, 189, 248)
                    : (stale || !connected ? Color.FromArgb(190, 156, 163, 175) : Color.FromArgb(220, 229, 231, 235));

                RectangleF badge = new RectangleF(
                    pad,
                    pad,
                    size - (pad * 2) - 1,
                    size - (pad * 2) - 1);

                using (GraphicsPath path = RoundedRectangle(badge, radius))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(238, 17, 19, 23)))
                using (Pen outline = new Pen(border, Math.Max(1f, size / 32f)))
                {
                    graphics.FillPath(fill, path);
                    graphics.DrawPath(outline, path);
                }

                int barHeight = Math.Max(2, scale * 2);
                int barY = size - pad - barHeight - scale;
                int barX = pad + scale * 2;
                int barWidth = size - ((barX) * 2);
                if (barWidth < 4)
                {
                    barX = pad + 1;
                    barWidth = size - ((barX) * 2);
                }
                if (barWidth < 4) barWidth = 4;

                using (SolidBrush track = new SolidBrush(Color.FromArgb(85, 255, 255, 255)))
                {
                    graphics.FillRectangle(track, barX, barY, barWidth, barHeight);
                }

                int value = percent.HasValue ? Math.Max(0, Math.Min(100, percent.Value)) : 0;
                int fillWidth = percent.HasValue ? (int)Math.Round(barWidth * (value / 100.0)) : 0;
                if (percent.HasValue && value > 0 && fillWidth < 1) fillWidth = 1;
                if (fillWidth > 0)
                {
                    using (SolidBrush fillBrush = new SolidBrush(accent))
                    {
                        graphics.FillRectangle(fillBrush, barX, barY, fillWidth, barHeight);
                    }
                }

                string text = percent.HasValue ? percent.Value.ToString() : "--";
                int glyphWidth = 3 * scale;
                int glyphHeight = 5 * scale;
                int gap = scale;
                int totalWidth = text.Length * glyphWidth + Math.Max(0, text.Length - 1) * gap;
                int textX = (size - totalWidth) / 2;
                int textY = (size - glyphHeight) / 2 - scale;
                if (textY < pad + 1) textY = pad + 1;

                graphics.SmoothingMode = SmoothingMode.None;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(245, 249, 250, 251)))
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        int x = textX + i * (glyphWidth + gap);
                        DrawGlyph(graphics, textBrush, text[i], x, textY, scale);
                    }
                }
            }
            return bitmap;
        }

        private static Color AccentColor(int? percent, bool charging, bool connected, bool stale)
        {
            if (charging) return Color.FromArgb(255, 56, 189, 248);
            if (!connected || stale || !percent.HasValue) return Color.FromArgb(255, 156, 163, 175);
            if (percent.Value <= 10) return Color.FromArgb(255, 239, 68, 68);
            if (percent.Value <= 25) return Color.FromArgb(255, 245, 158, 11);
            return Color.FromArgb(255, 34, 197, 94);
        }

        private static void DrawGlyph(Graphics graphics, Brush brush, char character, int x, int y, int scale)
        {
            if (character == '-')
            {
                graphics.FillRectangle(brush, x, y + (2 * scale), 3 * scale, scale);
                return;
            }

            int digit = character - '0';
            if (digit < 0 || digit > 9) return;
            string[] rows = DigitGlyphs[digit];
            for (int row = 0; row < 5; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    if (rows[row][column] == '1')
                    {
                        graphics.FillRectangle(
                            brush,
                            x + column * scale,
                            y + row * scale,
                            scale,
                            scale);
                    }
                }
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            if (diameter > rectangle.Width) diameter = rectangle.Width;
            if (diameter > rectangle.Height) diameter = rectangle.Height;

            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void WriteIco(Stream stream, List<Bitmap> frames)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)frames.Count);

            int offset = 6 + (16 * frames.Count);
            List<byte[]> images = new List<byte[]>();
            List<int> widths = new List<int>();
            List<int> heights = new List<int>();

            foreach (Bitmap frame in frames)
            {
                byte[] image = BuildBitmapImage(frame);
                images.Add(image);
                widths.Add(frame.Width);
                heights.Add(frame.Height);
                writer.Write((byte)(frame.Width >= 256 ? 0 : frame.Width));
                writer.Write((byte)(frame.Height >= 256 ? 0 : frame.Height));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(image.Length);
                writer.Write(offset);
                offset += image.Length;
            }

            for (int i = 0; i < images.Count; i++)
            {
                writer.Write(images[i]);
            }
            writer.Flush();
        }

        private static byte[] BuildBitmapImage(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int stride = width * 4;
            byte[] xor = new byte[stride * height];

            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[stride];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (y * data.Stride)), row, 0, stride);
                    Array.Copy(row, 0, xor, (height - 1 - y) * stride, stride);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            int andStride = ((width + 31) / 32) * 4;
            byte[] and = new byte[andStride * height];
            int total = 40 + xor.Length + and.Length;

            using (MemoryStream header = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(header);
                writer.Write(40);
                writer.Write(width);
                writer.Write(height * 2);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(0);
                writer.Write(xor.Length);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Flush();

                byte[] result = new byte[total];
                byte[] headerBytes = header.ToArray();
                Array.Copy(headerBytes, 0, result, 0, headerBytes.Length);
                Array.Copy(xor, 0, result, 40, xor.Length);
                Array.Copy(and, 0, result, 40 + xor.Length, and.Length);
                return result;
            }
        }
    }
}
