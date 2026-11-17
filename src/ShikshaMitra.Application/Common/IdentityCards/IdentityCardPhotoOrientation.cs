using SkiaSharp;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardPhotoOrientation
{
	public static SKBitmap Apply(SKBitmap bitmap, byte[] original)
	{
		using var data = SKData.CreateCopy(original);
		using var codec = SKCodec.Create(data);
		var origin = codec.EncodedOrigin;
		var w = bitmap.Width; var h = bitmap.Height;
		var swapped = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
		var result = new SKBitmap(swapped ? h : w, swapped ? w : h);
		using var canvas = new SKCanvas(result);
		canvas.Clear(SKColors.Transparent);
		var matrix = origin switch
		{
			SKEncodedOrigin.TopRight => new SKMatrix { ScaleX = -1, ScaleY = 1, TransX = w, Persp2 = 1 },
			SKEncodedOrigin.BottomRight => new SKMatrix { ScaleX = -1, ScaleY = -1, TransX = w, TransY = h, Persp2 = 1 },
			SKEncodedOrigin.BottomLeft => new SKMatrix { ScaleX = 1, ScaleY = -1, TransY = h, Persp2 = 1 },
			SKEncodedOrigin.LeftTop => new SKMatrix { SkewX = 1, SkewY = 1, Persp2 = 1 },
			SKEncodedOrigin.RightTop => new SKMatrix { SkewX = -1, SkewY = 1, TransX = h, Persp2 = 1 },
			SKEncodedOrigin.RightBottom => new SKMatrix { SkewX = -1, SkewY = -1, TransX = h, TransY = w, Persp2 = 1 },
			SKEncodedOrigin.LeftBottom => new SKMatrix { SkewX = 1, SkewY = -1, TransY = w, Persp2 = 1 },
			_ => SKMatrix.Identity
		};
		canvas.SetMatrix(matrix);
		canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
		return result;
	}
}
