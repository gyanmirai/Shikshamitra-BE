using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;

namespace ShikshaMitra.Common.PDF;

internal static class PortablePdfFonts
{
	public const string DefaultFamily = "Lato";
	public const string GujaratiFamily = "Noto Sans Gujarati";

	private static readonly Lazy<bool> Registered = new(() =>
	{
		FontManager.RegisterFontFromEmbeddedResource(typeof(PortablePdfFonts).Assembly,
			"ShikshaMitra.Pdf.Fonts.NotoSansGujarati.ttf");
		return true;
	});

	public static void EnsureRegistered() => _ = Registered.Value;

	public static TextStyle WithPortableFonts(this TextStyle style)
	{
		EnsureRegistered();
		return style.FontFamily(DefaultFamily, GujaratiFamily);
	}
}