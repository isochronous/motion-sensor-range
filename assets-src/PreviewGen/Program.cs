using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Isolates the motion sensor from assets-src/captures/motion-sensor.png (crop the
// ceiling, soft-key the near-uniform background preserving the red glow's falloff,
// drop small speck components) and overlays +/- glyphs in the mod-icon style.
internal static class Program
{
	private const int CropTop = 73;
	private static readonly Color Background = Color.FromArgb(207, 199, 197);
	private const float KeyStart = 14f;   // color distance where alpha starts rising
	private const float KeyFull = 50f;    // color distance of fully opaque pixels
	private const int MinComponent = 150;

	private static readonly Color GlyphFill = Color.FromArgb(232, 230, 224);
	private static readonly Color GlyphOutline = Color.FromArgb(38, 42, 48);

	private static void Main(string[] args)
	{
		if (args[0] == "atlas")
		{
			// Compose from a clean game-atlas sprite: PreviewGen atlas <in> <out> <x> <y> <w> <h>
			ComposeFromAtlas(args[1], args[2],
				new Rectangle(int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6])));
			return;
		}
		if (args[0] == "chevrons")
		{
			ComposeChevrons(args[1], args[2],
				new Rectangle(int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6])));
			return;
		}
		string inPath = args[0];
		string outPath = args[1];
		using var source = new Bitmap(inPath);

		int width = source.Width;
		int height = source.Height - CropTop;
		using var keyed = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		var alphas = new byte[width * height];
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				Color c = source.GetPixel(x, y + CropTop);
				float distance = MathF.Sqrt(
					(c.R - Background.R) * (c.R - Background.R) +
					(c.G - Background.G) * (c.G - Background.G) +
					(c.B - Background.B) * (c.B - Background.B));
				float a = Math.Clamp((distance - KeyStart) / (KeyFull - KeyStart), 0f, 1f);
				if (a <= 0f)
					continue;
				// Un-mix the background from semi-transparent pixels (glow halo).
				int r = DeMix(c.R, Background.R, a);
				int g = DeMix(c.G, Background.G, a);
				int b = DeMix(c.B, Background.B, a);
				alphas[y * width + x] = (byte)(a * 255f);
				keyed.SetPixel(x, y, Color.FromArgb((byte)(a * 255f), r, g, b));
			}
		}

		RemoveSmallComponents(keyed, alphas, width, height);

		// Trim to content with padding.
		int minX = width, minY = height, maxX = 0, maxY = 0;
		for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
				if (alphas[y * width + x] > 8)
				{
					minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
					minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
				}
		int pad = 12;
		var box = Rectangle.Intersect(new Rectangle(0, 0, width, height),
			new Rectangle(minX - pad, minY - pad, maxX - minX + 1 + 2 * pad, maxY - minY + 1 + 2 * pad));

		using var final = new Bitmap(box.Width, box.Height, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(final))
		{
			g.Clear(Color.Transparent);
			g.DrawImage(keyed, new Rectangle(0, 0, box.Width, box.Height), box, GraphicsUnit.Pixel);
			g.SmoothingMode = SmoothingMode.AntiAlias;

			// +/- glyphs, echoing the two-tone icon style, at the lower right of the dome.
			float cx = box.Width - 52f;
			DrawPlus(g, cx, box.Height * 0.42f, 46f, 13f);
			DrawMinus(g, cx, box.Height * 0.72f, 46f, 13f);
		}
		final.Save(outPath, ImageFormat.Png);
		Console.WriteLine($"Wrote {outPath} ({final.Width}x{final.Height})");
	}

	private static void ComposeFromAtlas(string inPath, string outPath, Rectangle crop)
	{
		const int canvas = 256;
		using var source = new Bitmap(inPath);

		// Copy the crop, erasing the translucent drop shadow (pure black, alpha <= ~80,
		// bottom rows only) so it doesn't end up floating above the flipped sensor.
		using var sprite = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppArgb);
		for (int y = 0; y < crop.Height; y++)
			for (int x = 0; x < crop.Width; x++)
			{
				Color c = source.GetPixel(crop.X + x, crop.Y + y);
				bool shadow = y >= crop.Height - 20 && c.A < 120 && c.R < 40 && c.G < 40 && c.B < 40;
				if (c.A > 0 && !shadow)
					sprite.SetPixel(x, y, c);
			}
		sprite.RotateFlip(RotateFlipType.RotateNoneFlipY);

		using var final = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(final))
		{
			g.Clear(Color.Transparent);
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			float scale = Math.Min((canvas - 20f) / crop.Width, (canvas - 20f) / crop.Height);
			int w = (int)(crop.Width * scale), h = (int)(crop.Height * scale);
			g.DrawImage(sprite, new Rectangle((canvas - w) / 2, (canvas - h) / 2, w, h),
				new Rectangle(0, 0, sprite.Width, sprite.Height), GraphicsUnit.Pixel);

			// "+/-" centered over the sensor, closed-caption style: white text on a
			// semi-transparent black band for guaranteed contrast against the art.
			using var path = new GraphicsPath();
			path.AddString("+/−", new FontFamily("Arial"), (int)FontStyle.Bold, 108f,
				new PointF(0f, 0f), StringFormat.GenericTypographic);
			RectangleF bounds = path.GetBounds();
			using var move = new Matrix();
			move.Translate(canvas / 2f - bounds.X - bounds.Width / 2f, canvas / 2f - bounds.Y - bounds.Height / 2f);
			path.Transform(move);
			bounds = path.GetBounds();
			var band = new RectangleF(16f, bounds.Y - 9f, canvas - 32f, bounds.Height + 18f);
			using (var bandPath = RoundedRectF(band, 9f))
			using (var bandFill = new SolidBrush(Color.FromArgb(145, 0, 0, 0)))
				g.FillPath(bandFill, bandPath);
			using var fill = new SolidBrush(Color.White);
			g.FillPath(fill, path);
		}
		final.Save(outPath, ImageFormat.Png);
		Console.WriteLine($"Wrote {outPath} ({canvas}x{canvas})");
	}

	/// <summary>
	/// Alternate design: ceiling-mounted sensor on a dark grey background, flanked by
	/// white "&lt;" and "&gt;" chevrons, no outline.
	/// </summary>
	private static void ComposeChevrons(string inPath, string outPath, Rectangle crop)
	{
		const int canvas = 256;
		const float cornerRadius = 16f;
		using var sprite = ExtractFlippedSprite(inPath, crop);
		using var final = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(final))
		{
			g.Clear(Color.Transparent);
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.SmoothingMode = SmoothingMode.AntiAlias;

			var card = new RectangleF(1.5f, 1.5f, canvas - 3f, canvas - 3f);
			using var cardPath = RoundedRectF(card, cornerRadius);
			g.SetClip(cardPath);

			// Radial background: lighter behind the sensor, darker toward the edges.
			using (var bgEllipse = new GraphicsPath())
			{
				bgEllipse.AddEllipse(-60f, -80f, canvas + 120f, canvas + 140f);
				using var bg = new PathGradientBrush(bgEllipse)
				{
					CenterColor = Color.FromArgb(78, 78, 82),
					CenterPoint = new PointF(canvas / 2f, canvas * 0.42f),
					SurroundColors = new[] { Color.FromArgb(40, 40, 43) },
				};
				g.FillRectangle(bg, 0, 0, canvas, canvas);
			}

			float scale = 150f / crop.Width;
			int w = (int)(crop.Width * scale), h = (int)(crop.Height * scale);
			int spriteLeft = (canvas - w) / 2;
			int spriteTop = (canvas - h) / 2;

			// Soft green detection glow below the dome.
			using (var glowEllipse = new GraphicsPath())
			{
				glowEllipse.AddEllipse(canvas / 2f - 78f, spriteTop + h - 46f, 156f, 104f);
				using var glow = new PathGradientBrush(glowEllipse)
				{
					CenterColor = Color.FromArgb(70, 130, 235, 90),
					SurroundColors = new[] { Color.FromArgb(0, 130, 235, 90) },
				};
				g.FillPath(glow, glowEllipse);
			}

			g.DrawImage(sprite, new Rectangle(spriteLeft, spriteTop, w, h),
				new Rectangle(0, 0, sprite.Width, sprite.Height), GraphicsUnit.Pixel);

			// Bahnschrift Condensed has the tallest/thinnest chevrons of the installed
			// fonts (1.56 height:width vs Arial's 1.11). Small drop shadows lift them.
			using var shadow = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
			using var fill = new SolidBrush(Color.White);
			float leftX = spriteLeft / 2f + 12f, rightX = canvas - spriteLeft / 2f - 12f, midY = canvas / 2f;
			DrawCenteredText(g, shadow, "<", leftX + 2.5f, midY + 3f, "Bahnschrift Condensed", 150f);
			DrawCenteredText(g, shadow, ">", rightX + 2.5f, midY + 3f, "Bahnschrift Condensed", 150f);
			DrawCenteredText(g, fill, "<", leftX, midY, "Bahnschrift Condensed", 150f);
			DrawCenteredText(g, fill, ">", rightX, midY, "Bahnschrift Condensed", 150f);

			g.ResetClip();
			using var border = new Pen(Color.White, 3f);
			g.DrawPath(border, cardPath);
		}
		final.Save(outPath, ImageFormat.Png);
		Console.WriteLine($"Wrote {outPath} ({canvas}x{canvas})");
	}

	private static void DrawCenteredText(Graphics g, Brush brush, string text, float cx, float cy,
		string fontFamily = "Arial", float emSize = 100f)
	{
		using var path = new GraphicsPath();
		path.AddString(text, new FontFamily(fontFamily), (int)FontStyle.Bold, emSize,
			new PointF(0f, 0f), StringFormat.GenericTypographic);
		RectangleF bounds = path.GetBounds();
		using var move = new Matrix();
		move.Translate(cx - bounds.X - bounds.Width / 2f, cy - bounds.Y - bounds.Height / 2f);
		path.Transform(move);
		g.FillPath(brush, path);
	}

	private static Bitmap ExtractFlippedSprite(string inPath, Rectangle crop)
	{
		using var source = new Bitmap(inPath);
		var sprite = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppArgb);
		for (int y = 0; y < crop.Height; y++)
			for (int x = 0; x < crop.Width; x++)
			{
				Color c = source.GetPixel(crop.X + x, crop.Y + y);
				bool shadow = y >= crop.Height - 20 && c.A < 120 && c.R < 40 && c.G < 40 && c.B < 40;
				if (c.A > 0 && !shadow)
					sprite.SetPixel(x, y, c);
			}
		sprite.RotateFlip(RotateFlipType.RotateNoneFlipY);
		return sprite;
	}

	private static GraphicsPath RoundedRectF(RectangleF r, float radius)
	{
		float d = radius * 2f;
		var path = new GraphicsPath();
		path.AddArc(r.X, r.Y, d, d, 180f, 90f);
		path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
		path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
		path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
		path.CloseFigure();
		return path;
	}

	private static int DeMix(int channel, int background, float alpha)
	{
		return Math.Clamp((int)MathF.Round((channel - (1f - alpha) * background) / alpha), 0, 255);
	}

	private static void RemoveSmallComponents(Bitmap bmp, byte[] alphas, int width, int height)
	{
		var component = new int[width * height];
		int next = 0;
		var sizes = new List<int>();
		var stack = new Stack<int>();
		for (int i = 0; i < component.Length; i++)
		{
			if (alphas[i] <= 8 || component[i] != 0)
				continue;
			next++;
			int size = 0;
			stack.Push(i);
			component[i] = next;
			while (stack.Count > 0)
			{
				int p = stack.Pop();
				size++;
				int px = p % width, py = p / width;
				Visit(px - 1, py); Visit(px + 1, py); Visit(px, py - 1); Visit(px, py + 1);
				void Visit(int x, int y)
				{
					if (x < 0 || y < 0 || x >= width || y >= height)
						return;
					int q = y * width + x;
					if (alphas[q] > 8 && component[q] == 0)
					{
						component[q] = next;
						stack.Push(q);
					}
				}
			}
			sizes.Add(size);
		}
		for (int i = 0; i < component.Length; i++)
		{
			if (component[i] != 0 && sizes[component[i] - 1] < MinComponent)
			{
				alphas[i] = 0;
				bmp.SetPixel(i % width, i / width, Color.Transparent);
			}
		}
	}

	private static void DrawPlus(Graphics g, float cx, float cy, float length, float thickness)
	{
		DrawBar(g, cx, cy, length, thickness, vertical: false);
		DrawBar(g, cx, cy, length, thickness, vertical: true);
		// Redraw horizontal fill so the outline of the vertical bar doesn't cross it.
		using var fill = new SolidBrush(GlyphFill);
		g.FillPath(fill, Bar(cx, cy, length - 3f, thickness - 3f, vertical: false));
		g.FillPath(fill, Bar(cx, cy, length - 3f, thickness - 3f, vertical: true));
	}

	private static void DrawMinus(Graphics g, float cx, float cy, float length, float thickness)
	{
		DrawBar(g, cx, cy, length, thickness, vertical: false);
	}

	private static void DrawBar(Graphics g, float cx, float cy, float length, float thickness, bool vertical)
	{
		using var path = Bar(cx, cy, length, thickness, vertical);
		using var fill = new SolidBrush(GlyphFill);
		using var outline = new Pen(GlyphOutline, 2.4f) { LineJoin = LineJoin.Round };
		g.FillPath(fill, path);
		g.DrawPath(outline, path);
	}

	private static GraphicsPath Bar(float cx, float cy, float length, float thickness, bool vertical)
	{
		float w = vertical ? thickness : length;
		float h = vertical ? length : thickness;
		float x = cx - w / 2f, y = cy - h / 2f, r = thickness / 2.6f, d = r * 2f;
		var path = new GraphicsPath();
		path.AddArc(x, y, d, d, 180f, 90f);
		path.AddArc(x + w - d, y, d, d, 270f, 90f);
		path.AddArc(x + w - d, y + h - d, d, d, 0f, 90f);
		path.AddArc(x, y + h - d, d, d, 90f, 90f);
		path.CloseFigure();
		return path;
	}
}
