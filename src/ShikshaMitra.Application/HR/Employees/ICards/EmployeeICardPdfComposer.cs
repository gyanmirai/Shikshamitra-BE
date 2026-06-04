using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardPdfComposer
{
	public static byte[] Compose(
		EmployeeICardTemplate template,
		IReadOnlyCollection<Employee> employees,
		IReadOnlyDictionary<Guid, List<EmployeeOrganizationDto>> organizationsByUserId,
		IReadOnlyDictionary<string, bool> fields,
		EmployeeICardLayout layout,
		EmployeeICardPrintSettings printSettings,
		int cardsPerPage,
		byte[]? logoBytes,
		IReadOnlyDictionary<Guid, byte[]?> photoBytesByEmployeeId)
	{
		var themeColor = EmployeeICardSvgFormatting.NormalizeColor(template.ThemeColor ?? template.BackgroundColor ?? "#1D4ED8");
		var accentColor = EmployeeICardSvgFormatting.NormalizeColor(template.BackgroundColor ?? "#F8FAFC");
		var cardSize = EmployeeICardPdfLayoutResolver.ResolveCardSize(layout);
		var sheetLayout = EmployeeICardPdfLayoutResolver.ResolveSheetLayout(cardsPerPage, cardSize.Width, cardSize.Height, printSettings);

		using var stream = new MemoryStream();
		Document.Create(container =>
		{
			foreach (var pageEmployees in employees.Chunk(cardsPerPage))
			{
				container.Page(page =>
				{
					page.Size(PageSizes.A4);
					page.Margin(printSettings.Margin);
					page.DefaultTextStyle(x => x.FontSize(8).FontColor("#111827"));
					page.Content().Column(column =>
					{
						column.Spacing(printSettings.Gap);

						foreach (var rowEmployees in pageEmployees.Chunk(sheetLayout.Columns))
						{
							column.Item().Row(row =>
							{
								row.Spacing(printSettings.Gap);
								foreach (var employee in rowEmployees)
								{
									row.ConstantItem(sheetLayout.CardWidth).Element(card => ComposeCard(
										card,
										template,
										employee,
										EmployeeICardPdfFieldResolver.ResolveOrganizationName(employee, organizationsByUserId),
										fields,
										layout,
										logoBytes,
										photoBytesByEmployeeId.GetValueOrDefault(employee.Id),
										themeColor,
										accentColor,
										sheetLayout.CardWidth,
										sheetLayout.CardHeight));
								}
							});
						}
					});
				});
			}
		}).GeneratePdf(stream);

		return stream.ToArray();
	}

	private static void ComposeCard(
		IContainer container,
		EmployeeICardTemplate template,
		Employee employee,
		string? organization,
		IReadOnlyDictionary<string, bool> fields,
		EmployeeICardLayout layout,
		byte[]? logoBytes,
		byte[]? employeePhotoBytes,
		string themeColor,
		string accentColor,
		float cardWidth,
		float cardHeight)
	{
		var svg = BuildCardSvg(template, employee, organization, fields, layout, logoBytes, employeePhotoBytes, themeColor, accentColor);

		container
			.Width(cardWidth)
			.Height(cardHeight)
			.Svg(svg);
	}

	private static string BuildCardSvg(
		EmployeeICardTemplate template,
		Employee employee,
		string? organization,
		IReadOnlyDictionary<string, bool> fields,
		EmployeeICardLayout layout,
		byte[]? logoBytes,
		byte[]? employeePhotoBytes,
		string themeColor,
		string accentColor)
	{
		var builder = new StringBuilder();
		var background = EmployeeICardSvgFormatting.NormalizeColor(layout.BackgroundColor ?? template.BackgroundColor ?? "#FFFFFF");
		builder.Append($"""<svg xmlns="http://www.w3.org/2000/svg" width="{layout.Width}" height="{layout.Height}" viewBox="0 0 {layout.Width} {layout.Height}">""");
		builder.Append($"""<rect x="0" y="0" width="{layout.Width}" height="{layout.Height}" rx="8" fill="{EmployeeICardSvgFormatting.Escape(background)}" stroke="#CBD5E1" stroke-width="1"/>""");

		foreach (var element in layout.Front.Elements.Where(x => x.Visible).OrderBy(x => x.Z))
		{
			if (!ShouldRenderElement(template, element, fields))
			{
				continue;
			}

			builder.Append(RenderElement(template, employee, organization, element, logoBytes, employeePhotoBytes, themeColor, accentColor));
		}

		builder.Append("</svg>");
		return builder.ToString();
	}

	private static bool ShouldRenderElement(EmployeeICardTemplate template, EmployeeICardLayoutElement element, IReadOnlyDictionary<string, bool> fields)
	{
		if (string.Equals(element.Type, "qr", StringComparison.OrdinalIgnoreCase) && !template.QrCodeEnabled)
		{
			return false;
		}

		if (string.Equals(element.Type, "barcode", StringComparison.OrdinalIgnoreCase) && !template.BarcodeEnabled)
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(element.FieldKey) ||
			string.Equals(element.FieldKey, "schoolName", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(element.FieldKey, "schoolAddress", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(element.FieldKey, "static", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return string.Equals(element.Type, "photo", StringComparison.OrdinalIgnoreCase)
			? EmployeeICardPdfFieldResolver.IsFieldEnabled(fields, "photo")
			: EmployeeICardPdfFieldResolver.IsFieldEnabled(fields, element.FieldKey);
	}

	private static string RenderElement(
		EmployeeICardTemplate template,
		Employee employee,
		string? organization,
		EmployeeICardLayoutElement element,
		byte[]? logoBytes,
		byte[]? employeePhotoBytes,
		string themeColor,
		string accentColor)
	{
		var style = element.Style ?? new EmployeeICardElementStyle();
		var fill = EmployeeICardSvgFormatting.NormalizeColor(style.BackgroundColor ?? "FFFFFF");
		var stroke = EmployeeICardSvgFormatting.NormalizeColor(style.BorderColor ?? "#CBD5E1");
		var strokeWidth = Math.Max(0, style.BorderWidth ?? 0);
		var radius = Math.Max(0, style.BorderRadius ?? 0);

		return element.Type.ToLowerInvariant() switch
		{
			"shape" => $"""<rect x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" rx="{radius}" fill="{EmployeeICardSvgFormatting.Escape(fill)}" stroke="{EmployeeICardSvgFormatting.Escape(stroke)}" stroke-width="{strokeWidth}"/>""",
			"line" => $"""<line x1="{element.X}" y1="{element.Y}" x2="{element.X + element.W}" y2="{element.Y}" stroke="{EmployeeICardSvgFormatting.Escape(EmployeeICardSvgFormatting.NormalizeColor(style.BackgroundColor ?? style.BorderColor ?? accentColor))}" stroke-width="{Math.Max(1, element.H)}"/>""",
			"logo" => EmployeeICardMediaRenderer.RenderImageOrPlaceholder(element, logoBytes, themeColor, "LOGO"),
			"photo" => EmployeeICardMediaRenderer.RenderImageOrPlaceholder(element, employeePhotoBytes, themeColor, "PHOTO", style.PhotoShape),
			"qr" => EmployeeICardMediaRenderer.RenderEmbeddedSvg(element, EmployeeICardMediaRenderer.BuildQrSvg(EmployeeICardPdfFieldResolver.BuildIdentityPayload(employee))),
			"barcode" => EmployeeICardBarcodeRenderer.Render(element, employee.EmployeeCode ?? employee.EmployeeName ?? employee.Id.ToString("N")),
			"text" => EmployeeICardTextRenderer.Render(template, employee, organization, element),
			_ => string.Empty
		};
	}
}
