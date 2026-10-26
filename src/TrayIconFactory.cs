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
            new string[] { "1111", "1001", "1001", "1001", "1001", "1111" },
            new string[] { "0010", "0110", "0010", "0010", "0010", "0111" },
            new string[] { "1111", "0001", "1111", "1000", "1000", "1111" },
            new string[] { "1111", "0001", "0111", "0001", "0001", "1111" },
            new string[] { "1001", "1001", "1111", "0001", "0001", "0001" },
            new string[] { "1111", "1000", "1111", "0001", "0001", "1111" },
            new string[] { "1111", "1000", "1111", "1001", "1001", "1111" },
            new string[] { "1111", "0001", "0010", "0010", "0100", "0100" },
            new string[] { "1111", "1001", "1111", "1001", "1001", "1111" },
            new string[] { "1111", "1001", "1111", "0001", "0001", "1111" }
        };
        internal static Icon CreateIcon(int? percent, bool charging, bool connected, bool stale)
        {
            List<Bitmap> frames = new List<Bitmap>();
            try
            {
                foreach (int size in IconSizes)
                {
                    frames.Add(DrawMouseIcon(size, percent, charging, connected, stale));
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
                    frames.Add(DrawMouseIcon(size, percent, charging, connected, stale));
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

        private static Bitmap DrawMouseIcon(int size, int? percent, bool charging, bool connected, bool stale)
        {
            Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Color outlineColor = stale || !connected
                    ? Color.FromArgb(225, 107, 114, 128)
                    : Color.FromArgb(255, 10, 12, 17);
                float outlineWidth = Math.Max(1f, size / 32f);

                // Minimal top-down mouse silhouette.
                float bodyWidth = size * 0.72f;
                float bodyHeight = size * 0.94f;
                float bodyX = (size - bodyWidth) / 2f;
                float bodyY = (size - bodyHeight) / 2f;
                if (size <= 20)
                {
                    bodyWidth = size - 4f;
                    bodyHeight = size - 1f;
                    bodyX = 2f;
                    bodyY = 0.5f;
                }

                RectangleF body = new RectangleF(bodyX, bodyY, bodyWidth, bodyHeight);
                using (GraphicsPath bodyPath = RoundedRectangle(body, bodyWidth * 0.48f))
                using (SolidBrush bodyFill = new SolidBrush(stale || !connected
                    ? Color.FromArgb(245, 120, 126, 138)
                    : Color.FromArgb(250, 237, 241, 246)))
                using (Pen outline = new Pen(outlineColor, outlineWidth))
                {
                    graphics.FillPath(bodyFill, bodyPath);
                    graphics.DrawPath(outline, bodyPath);
                }

                float centerX = body.X + (body.Width / 2f);
                float outlineInset = Math.Max(1f, outlineWidth);

                // A single short button split keeps it recognisable without clutter.
                using (Pen split = new Pen(Color.FromArgb(220, 35, 38, 45), Math.Max(1f, outlineWidth * 0.8f)))
                {
                    graphics.DrawLine(
                        split,
                        centerX,
                        body.Top + outlineInset,
                        centerX,
                        body.Top + (body.Height * 0.24f));
                }

                // Scroll wheel: the only bright detail near the top.
                float wheelWidth = Math.Max(2f, size * 0.11f);
                float wheelHeight = Math.Max(3f, size * 0.16f);
                RectangleF wheel = new RectangleF(
                    centerX - (wheelWidth / 2f),
                    body.Top + (body.Height * 0.085f),
                    wheelWidth,
                    wheelHeight);
                using (GraphicsPath wheelPath = RoundedRectangle(wheel, wheelWidth / 2f))
                using (SolidBrush wheelFill = new SolidBrush(stale || !connected
                    ? Color.FromArgb(230, 90, 95, 105)
                    : Color.FromArgb(255, 30, 33, 40)))
                {
                    graphics.FillPath(wheelFill, wheelPath);
                }

                string text = percent.HasValue
                    ? (size <= 20 && percent.Value >= 100 ? "99" : percent.Value.ToString())
                    : "--";
                Color textColor = TextColor(percent, charging, connected, stale);

                if (size <= 20)
                {
                    const int glyphWidth = 4;
                    const int gap = 1;
                    int totalWidth = text.Length * glyphWidth + Math.Max(0, text.Length - 1) * gap;
                    int textX = (int)Math.Round(body.X + ((body.Width - totalWidth) / 2f));
                    int textY = (int)Math.Round(body.Top + (body.Height * 0.40f));
                    graphics.SmoothingMode = SmoothingMode.None;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    using (SolidBrush textBrush = new SolidBrush(textColor))
                    {
                        for (int i = 0; i < text.Length; i++)
                        {
                            DrawGlyph(graphics, textBrush, text[i], textX + i * (glyphWidth + gap), textY, 1);
                        }
                    }
                }
                else
                {
                    float maxWidth = body.Width - (size * 0.12f);
                    float fontSize = Math.Max(7f, size * 0.42f);
                    Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    try
                    {
                        SizeF measured = graphics.MeasureString(text, font);
                        while (measured.Width > maxWidth && fontSize > 6f)
                        {
                            fontSize -= 0.5f;
                            font.Dispose();
                            font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                            measured = graphics.MeasureString(text, font);
                        }

                        using (StringFormat format = new StringFormat())
                        {
                            format.Alignment = StringAlignment.Center;
                            format.LineAlignment = StringAlignment.Center;
                            RectangleF textRect = new RectangleF(
                                body.X + (size * 0.04f),
                                body.Top + (body.Height * 0.34f),
                                body.Width - (size * 0.08f),
                                body.Height * 0.52f);
                            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                            {
                                RectangleF shadowRect = textRect;
                                shadowRect.Offset(1f, 1f);
                                graphics.DrawString(text, font, shadow, shadowRect, format);
                            }
                            using (SolidBrush textBrush = new SolidBrush(textColor))
                            {
                                graphics.DrawString(text, font, textBrush, textRect, format);
                            }
                        }
                    }
                    finally
                    {
                        font.Dispose();
                    }
                }
            }
            return bitmap;
        }
        private static Color TextColor(int? percent, bool charging, bool connected, bool stale)
        {
            if (charging) return Color.FromArgb(255, 2, 132, 199);
            if (!connected || stale || !percent.HasValue) return Color.FromArgb(255, 75, 85, 99);
            if (percent.Value <= 10) return Color.FromArgb(255, 185, 28, 28);
            if (percent.Value <= 25) return Color.FromArgb(255, 180, 83, 9);
            return Color.FromArgb(255, 21, 128, 61);
        }

        private static void DrawGlyph(Graphics graphics, Brush brush, char character, int x, int y, int scale)
        {
            if (character == '-')
            {
                graphics.FillRectangle(brush, x, y + (3 * scale), 3 * scale, scale);
                return;
            }

            int digit = character - '0';
            if (digit < 0 || digit > 9) return;
            string[] rows = DigitGlyphs[digit];
            for (int row = 0; row < 6; row++)
            {
                for (int column = 0; column < 4; column++)
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


