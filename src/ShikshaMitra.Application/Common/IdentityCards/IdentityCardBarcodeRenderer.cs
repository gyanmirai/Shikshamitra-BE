using System;
using System.Linq;
using System.Text;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardBarcodeRenderer
{
	public static string Render(IdentityCardLayoutElement element, string value)
	{
		value = value.ToUpperInvariant();
		if (value.Any(c => !IsCode39Character(c)) || element.H < 10) throw IdentityCardGeometry.Invalid("Barcode values require Code39 characters and at least 10 canvas units of height.");
		var encoded = $"*{value}*";
		if (encoded.Length <= 2)
		{
			encoded = "*ID*";
		}

		var narrow = element.W / (encoded.Length * 14.5f + 20);
		var x = element.X + narrow * 10;
		var bars = new StringBuilder();
		foreach (var character in encoded)
		{
			var pattern = Code39Pattern(character);
			for (var index = 0; index < pattern.Length; index++)
			{
				var wide = pattern[index] == 'w';
				var width = wide ? narrow * 2.5f : narrow;
				if (index % 2 == 0)
				{
					bars.Append(FormattableString.Invariant($"""<rect x="{x:0.##}" y="{element.Y}" width="{width:0.##}" height="{element.H - 8:0.##}" fill="#111827"/>"""));
				}

				x += width;
			}

			x += narrow;
		}

		var text = IdentityCardSvgFormatting.Escape(encoded.Trim('*'));
		return bars + FormattableString.Invariant($"""<text x="{element.X + (element.W / 2)}" y="{element.Y + element.H}" font-family="Arial, sans-serif" font-size="6" text-anchor="middle" fill="#111827">{text}</text>""");
	}

	private static bool IsCode39Character(char value) =>
		value is >= 'A' and <= 'Z' or >= '0' and <= '9' || value is '-' or '.' or ' ' or '$' or '/' or '+' or '%';

	private static string Code39Pattern(char value) =>
		value switch
		{
			'0' => "nnnwwnwnn",
			'1' => "wnnwnnnnw",
			'2' => "nnwwnnnnw",
			'3' => "wnwwnnnnn",
			'4' => "nnnwwnnnw",
			'5' => "wnnwwnnnn",
			'6' => "nnwwwnnnn",
			'7' => "nnnwnnwnw",
			'8' => "wnnwnnwnn",
			'9' => "nnwwnnwnn",
			'A' => "wnnnnwnnw",
			'B' => "nnwnnwnnw",
			'C' => "wnwnnwnnn",
			'D' => "nnnnwwnnw",
			'E' => "wnnnwwnnn",
			'F' => "nnwnwwnnn",
			'G' => "nnnnnwwnw",
			'H' => "wnnnnwwnn",
			'I' => "nnwnnwwnn",
			'J' => "nnnnwwwnn",
			'K' => "wnnnnnnww",
			'L' => "nnwnnnnww",
			'M' => "wnwnnnnwn",
			'N' => "nnnnwnnww",
			'O' => "wnnnwnnwn",
			'P' => "nnwnwnnwn",
			'Q' => "nnnnnnwww",
			'R' => "wnnnnnwwn",
			'S' => "nnwnnnwwn",
			'T' => "nnnnwnwwn",
			'U' => "wwnnnnnnw",
			'V' => "nwwnnnnnw",
			'W' => "wwwnnnnnn",
			'X' => "nwnnwnnnw",
			'Y' => "wwnnwnnnn",
			'Z' => "nwwnwnnnn",
			'-' => "nwnnnnwnw",
			'.' => "wwnnnnwnn",
			' ' => "nwwnnnwnn",
			'*' => "nwnnwnwnn",
			'$' => "nwnwnwnnn",
			'/' => "nwnwnnnwn",
			'+' => "nwnnnwnwn",
			'%' => "nnnwnwnwn",
			_ => "nwwnnnwnn"
		};
}
