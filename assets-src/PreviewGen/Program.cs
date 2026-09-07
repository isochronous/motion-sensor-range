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
		using var final = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(final))
		{
			g.Clear(Color.Transparent);
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			float scale = Math.Min((canvas - 20f) / crop.Width, (canvas - 20f) / crop.Height);
			int w = (int)(crop.Width * scale), h = (int)(crop.Height * scale);
			g.DrawImage(source, new Rectangle((canvas - w) / 2, (canvas - h) / 2, w, h), crop, GraphicsUnit.Pixel);

			DrawPlus(g, canvas - 46f, 100f, 54f, 16f);
			DrawMinus(g, canvas - 46f, 172f, 54f, 16f);
		}
		final.Save(outPath, ImageFormat.Png);
		Console.WriteLine($"Wrote {outPath} ({canvas}x{canvas})");
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
