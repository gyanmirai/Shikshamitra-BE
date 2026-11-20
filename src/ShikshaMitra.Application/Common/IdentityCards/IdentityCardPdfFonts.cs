using System.Collections.Generic;
using QuestPDF.Drawing;
using SkiaSharp;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardPdfFonts
{
	private static readonly object Gate = new();
	private static readonly HashSet<string> Registered = [];
	public static string Register(SKTypeface typeface)
	{
		var key = $"{typeface.FamilyName}:{typeface.FontWeight}:{typeface.FontWidth}:{typeface.FontSlant}";
		lock (Gate)
		{
			if (!Registered.Contains(key))
			{
				using var stream = typeface.OpenStream();
				if (stream == null) throw IdentityCardGeometry.Invalid("The selected font is unavailable for printing.");
				using var data = SKData.Create(stream);
				FontManager.RegisterFontFromBinaryData(data.ToArray());
				Registered.Add(key);
			}
		}
		return typeface.FamilyName;
	}
}
