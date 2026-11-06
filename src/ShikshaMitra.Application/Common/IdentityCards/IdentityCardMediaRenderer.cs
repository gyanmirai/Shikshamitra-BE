using QRCoder;
using System;
using System.Text;
using System.Xml.Linq;
using System.Globalization;

namespace ShikshaMitra.Common.IdentityCards;

internal static class IdentityCardMediaRenderer
{
	public static string RenderImageOrPlaceholder(
		IdentityCardLayoutElement element,
		byte[]? bytes,
		string themeColor,
		string label,
		string? shape = null)
	{
		var clipId = $"clip-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(element.Id)))[..16]}";
		var radius = string.Equals(shape, "circle", StringComparison.OrdinalIgnoreCase)
			? Math.Min(element.W, element.H) / 2
			: Math.Max(0, element.Style?.BorderRadius ?? 8);
		var border = IdentityCardSvgFormatting.NormalizeColor(element.Style?.BorderColor ?? themeColor);
		var strokeWidth = Math.Max(0, element.Style?.BorderWidth ?? 1);
		var frame = FormattableString.Invariant($"""<rect x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" rx="{radius}" fill="#FFFFFF" stroke="{IdentityCardSvgFormatting.Escape(border)}" stroke-width="{strokeWidth}"/>""");
		if (bytes is not { Length: > 0 })
		{
			return frame + FormattableString.Invariant($"""<text x="{element.X + (element.W / 2)}" y="{element.Y + (element.H / 2) + 4}" font-family="Arial, sans-serif" font-size="9" text-anchor="middle" fill="#64748B">{label}</text>""");
		}

		var mime = IsPng(bytes) ? "image/png" : "image/jpeg";
		var data = Convert.ToBase64String(bytes);
		var aspectRatio = element.Type == "logo" ? "xMidYMid meet" : "xMidYMid slice";
		return frame +
			FormattableString.Invariant($"""<clipPath id="{clipId}"><rect x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" rx="{radius}"/></clipPath>""") +
			FormattableString.Invariant($"""<image xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="data:{mime};base64,{data}" x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" preserveAspectRatio="{aspectRatio}" clip-path="url(#{clipId})"/>""");
	}

	public static string RenderEmbeddedSvg(IdentityCardLayoutElement element, string svg)
	{
		var root = XElement.Parse(svg);
		if (root.Attribute("viewBox") == null)
			root.SetAttributeValue("viewBox", $"0 0 {root.Attribute("width")!.Value} {root.Attribute("height")!.Value}");
		root.SetAttributeValue("x", element.X.ToString(CultureInfo.InvariantCulture));
		root.SetAttributeValue("y", element.Y.ToString(CultureInfo.InvariantCulture));
		root.SetAttributeValue("width", element.W.ToString(CultureInfo.InvariantCulture));
		root.SetAttributeValue("height", element.H.ToString(CultureInfo.InvariantCulture));
		root.SetAttributeValue("preserveAspectRatio", "xMidYMid meet");
		return root.ToString(SaveOptions.DisableFormatting);
	}

	public static string BuildQrSvg(string payload)
	{
		using var generator = new QRCodeGenerator();
		using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
		var qr = new SvgQRCode(data);
		return qr.GetGraphic(4);
	}

	private static bool IsPng(byte[] bytes) =>
		bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
}
