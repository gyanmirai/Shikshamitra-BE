using QRCoder;
using System;
using System.Text;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardMediaRenderer
{
	public static string RenderImageOrPlaceholder(
		EmployeeICardLayoutElement element,
		byte[]? bytes,
		string themeColor,
		string label,
		string? shape = null)
	{
		var clipId = $"clip-{element.Id}";
		var radius = string.Equals(shape, "circle", StringComparison.OrdinalIgnoreCase)
			? Math.Min(element.W, element.H) / 2
			: Math.Max(0, element.Style?.BorderRadius ?? 8);
		var border = EmployeeICardSvgFormatting.NormalizeColor(element.Style?.BorderColor ?? themeColor);
		var strokeWidth = Math.Max(0, element.Style?.BorderWidth ?? 1);
		var frame = $"""<rect x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" rx="{radius}" fill="#FFFFFF" stroke="{EmployeeICardSvgFormatting.Escape(border)}" stroke-width="{strokeWidth}"/>""";
		if (bytes is not { Length: > 0 })
		{
			return frame + $"""<text x="{element.X + (element.W / 2)}" y="{element.Y + (element.H / 2) + 4}" font-family="Arial, sans-serif" font-size="9" text-anchor="middle" fill="#64748B">{label}</text>""";
		}

		var mime = IsPng(bytes) ? "image/png" : "image/jpeg";
		var data = Convert.ToBase64String(bytes);
		return frame +
			$"""<clipPath id="{clipId}"><rect x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" rx="{radius}"/></clipPath>""" +
			$"""<image href="data:{mime};base64,{data}" x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" preserveAspectRatio="xMidYMid slice" clip-path="url(#{clipId})"/>""";
	}

	public static string RenderEmbeddedSvg(EmployeeICardLayoutElement element, string svg)
	{
		var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
		return $"""<image href="data:image/svg+xml;base64,{encoded}" x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" preserveAspectRatio="xMidYMid meet"/>""";
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
