using System;
using OpenTK.Graphics.ES11;

namespace DropboxPaint
{
	public static class Color
	{
		public const int PaletteHeight = 30;
		public const int PaletteSize = 5;

		public static int Selected { get; set; }

		public static void HslToRgb (float h, float s, float l, out float r, out float g, out float b)
		{
			// Check for saturation. If there isn't any just return the luminance value for each, which results in gray.
			if (s == 0.0) {
				r = l;
				g = l;
				b = l;
				return;
			}

			// Test for luminance and compute temporary values based on luminance and saturation
			float temp2;
			if (l < 0.5)
				temp2 = l * (1.0f + s);
			else
				temp2 = l + s - l * s;
			float temp1 = 2.0f * l - temp2;

			// Compute intermediate values based on hue
			float[] temp = {
				h + 1.0f / 3.0f,
				h,
				h - 1.0f / 3.0f,
			};
			for (int i = 0; i < temp.Length; ++i) {
				if (temp [i] < 0.0f)
					temp [i] += 1.0f;
				if (temp [i] > 1.0f)
					temp [i] -= 1.0f;

				if (6.0f * temp [i] < 1.0f)
					temp [i] = temp1 + (temp2 - temp1) * 6.0f * temp [i];
				else {
					if (2.0f * temp [i] < 1.0f)
						temp [i] = temp2;
					else {
						if (3.0f * temp [i] < 2.0f)
							temp [i] = temp1 + (temp2 - temp1) * ((2.0f / 3.0f) - temp [i]) * 6.0f;
						else
							temp [i] = temp1;
					}
				}
			}
			r = temp [0];
			g = temp [1];
			b = temp [2];
		}

		public static void ChangeBrushColor (int h)
		{
			float r, g, b;
			HslToRgb (h / (float) PaletteSize, PaintingView.Saturation, PaintingView.Luminosity,
			          out r, out g, out b);
			GL.Color4 (r, g, b, PaintingView.BrushOpacity);
		}
	}
}