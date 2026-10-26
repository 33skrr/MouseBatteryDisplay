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

                Color accent = AccentColor(percent, charging, connected, stale);
                Color outlineColor = stale || !connected
                    ? Color.FromArgb(220, 120, 126, 138)
                    : Color.FromArgb(255, 10, 12, 17);
                float outlineWidth = Math.Max(1f, size / 32f);

                // A top-down mouse silhouette: rounded body, two buttons and a scroll wheel.
                float bodyWidth = size * 0.64f;
                float bodyHeight = size * 0.90f;
                float bodyX = (size - bodyWidth) / 2f;
                float bodyY = (size - bodyHeight) / 2f;
                if (size <= 20)
                {
                    bodyWidth = size - 6f;
                    bodyHeight = size - 2f;
                    bodyX = 3f;
                    bodyY = 1f;
                }

                RectangleF body = new RectangleF(bodyX, bodyY, bodyWidth, bodyHeight);
                using (GraphicsPath bodyPath = RoundedRectangle(body, bodyWidth * 0.48f))
                {
                    using (SolidBrush bodyFill = new SolidBrush(stale || !connected
                        ? Color.FromArgb(245, 96, 100, 110)
                        : Color.FromArgb(250, 234, 238, 244)))
                    {
                        graphics.FillPath(bodyFill, bodyPath);
                    }

                    int value = percent.HasValue ? Math.Max(0, Math.Min(100, percent.Value)) : 0;
                    if (percent.HasValue && value > 0)
                    {
                        float innerHeight = Math.Max(1f, body.Height - (outlineWidth * 2f));
                        float fillHeight = Math.Max(1.5f, innerHeight * (value / 100f));
                        graphics.SetClip(bodyPath);
                        using (SolidBrush batteryFill = new SolidBrush(Color.FromArgb(32, accent)))
                        {
                            graphics.FillRectangle(
                                batteryFill,
                                body.X + outlineWidth,
                                body.Bottom - outlineWidth - fillHeight,
                                Math.Max(1f, body.Width - (outlineWidth * 2f)),
                                fillHeight);
                        }
                        graphics.ResetClip();
                    }

                    using (Pen outline = new Pen(outlineColor, outlineWidth))
                    {
                        graphics.DrawPath(outline, bodyPath);
                    }
                }

                float centerX = body.X + (body.Width / 2f);
                float outlineInset = Math.Max(1f, outlineWidth);

                // Button split.
                using (Pen split = new Pen(Color.FromArgb(205, 237, 242, 247), Math.Max(1f, outlineWidth * 0.8f)))
                {
                    graphics.DrawLine(
                        split,
                        centerX,
                        body.Top + outlineInset,
                        centerX,
                        body.Top + (body.Height * 0.28f));
                }

                // Scroll wheel.
                float wheelWidth = Math.Max(2f, size * 0.105f);
                float wheelHeight = Math.Max(3f, size * 0.155f);
                RectangleF wheel = new RectangleF(
                    centerX - (wheelWidth / 2f),
                    body.Top + (body.Height * 0.105f),
                    wheelWidth,
                    wheelHeight);
                using (GraphicsPath wheelPath = RoundedRectangle(wheel, wheelWidth / 2f))
                using (SolidBrush wheelFill = new SolidBrush(Color.FromArgb(255, 28, 31, 38)))
                using (Pen wheelPen = new Pen(Color.FromArgb(210, 237, 242, 247), Math.Max(1f, outlineWidth * 0.7f)))
                {
                    graphics.FillPath(wheelFill, wheelPath);
                    graphics.DrawPath(wheelPen, wheelPath);
                }

                string text = percent.HasValue
                    ? (size <= 20 && percent.Value >= 100 ? "99" : percent.Value.ToString())
                    : "--";

                // Small dark display on the mouse back for the percentage.
                float screenInset = size <= 20 ? 1f : Math.Max(2f, size * 0.075f);
                float screenX = body.X + screenInset;
                float screenWidth = Math.Max(6f, body.Width - (screenInset * 2f));
                float barHeight = Math.Max(2f, size * 0.07f);
                float bottomInset = size <= 20 ? 1f : Math.Max(2f, size * 0.08f);
                float barY = body.Bottom - bottomInset - barHeight;
                float screenHeight = size <= 20
                    ? 5.5f
                    : Math.Min(body.Height * 0.34f, size * 0.30f);
                float screenY = size <= 20
                    ? 6f
                    : barY - 1f - screenHeight;

                RectangleF screen = new RectangleF(screenX, screenY, screenWidth, screenHeight);
                using (GraphicsPath screenPath = RoundedRectangle(screen, Math.Max(1f, screenHeight * 0.22f)))
                using (SolidBrush screenFill = new SolidBrush(Color.FromArgb(230, 10, 12, 16)))
                using (Pen screenPen = new Pen(Color.FromArgb(150, outlineColor), Math.Max(1f, outlineWidth * 0.7f)))
                {
                    graphics.FillPath(screenFill, screenPath);
                    graphics.DrawPath(screenPen, screenPath);
                }

                // Thin battery gauge at the bottom of the mouse body.
                float barX = screenX + size * 0.04f;
                float barWidth = Math.Max(4f, body.Width - (2f * (screenInset + size * 0.04f)));
                using (SolidBrush barTrack = new SolidBrush(Color.FromArgb(95, 10, 12, 17)))
                {
                    graphics.FillRectangle(barTrack, barX, barY, barWidth, barHeight);
                }
                if (percent.HasValue)
                {
                    int barValue = Math.Max(0, Math.Min(100, percent.Value));
                    float barFillWidth = barWidth * (barValue / 100f);
                    if (barValue > 0 && barFillWidth < 1f) barFillWidth = 1f;
                    if (barFillWidth > 0)
                    {
                        using (SolidBrush barFill = new SolidBrush(accent))
                        {
                            graphics.FillRectangle(barFill, barX, barY, barFillWidth, barHeight);
                        }
                    }
                }

                if (size <= 20)
                {
                    int glyphWidth = 3;
                    int glyphHeight = 5;
                    int gap = 1;
                    int totalWidth = text.Length * glyphWidth + Math.Max(0, text.Length - 1) * gap;
                    int textX = (int)Math.Round(screen.X + ((screen.Width - totalWidth) / 2f));
                    int textY = (int)Math.Round(screen.Y + ((screen.Height - glyphHeight) / 2f));
                    graphics.SmoothingMode = SmoothingMode.None;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(250, 249, 250, 251)))
                    {
                        for (int i = 0; i < text.Length; i++)
                        {
                            DrawGlyph(graphics, textBrush, text[i], textX + i * (glyphWidth + gap), textY, 1);
                        }
                    }
                }
                else
                {
                    float fontSize = Math.Max(6.5f, size * 0.30f);
                    Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    try
                    {
                        SizeF measured = graphics.MeasureString(text, font);
                        float maxWidth = screen.Width - Math.Max(1f, size * 0.05f);
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
                                screen.X,
                                screen.Y - (size * 0.01f),
                                screen.Width,
                                screen.Height);
                            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                            {
                                RectangleF shadowRect = textRect;
                                shadowRect.Offset(1f, 1f);
                                graphics.DrawString(text, font, shadow, shadowRect, format);
                            }
                            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(250, 249, 250, 251)))
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

