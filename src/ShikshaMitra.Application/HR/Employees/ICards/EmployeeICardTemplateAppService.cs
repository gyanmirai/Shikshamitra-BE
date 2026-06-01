using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Employees.ICards;

[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.View)]
public class EmployeeICardTemplateAppService : ApplicationService, IEmployeeICardTemplateAppService
{
	private const int MaxPageSize = 100;
	private const long MaxAssetSize = 2 * 1024 * 1024;
	private const decimal PreviewPixelToPointScale = 0.7m;
	private const string HorizontalOrientation = "Horizontal";
	private const string VerticalOrientation = "Vertical";
	private static readonly HashSet<string> AllowedAssetTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"image/png",
		"image/jpeg",
		"image/jpg"
	};
	private static readonly HashSet<string> AllowedAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".png",
		".jpg",
		".jpeg"
	};
	private static readonly HashSet<string> SupportedFieldNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"photo",
		"name",
		"employeeCode",
		"designation",
		"department",
		"organization",
		"contactNo",
		"bloodGroup",
		"validityYear",
		"address"
	};
	private static readonly HashSet<string> MandatoryFieldNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"photo",
		"name",
		"employeeCode"
	};

	private readonly IRepository<EmployeeICardTemplate, Guid> _repository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly EmployeeOrganizationScopeResolver _organizationScopeResolver;
	private readonly IBlobContainer _blobContainer;
	private readonly IGuidGenerator _guidGenerator;

	public EmployeeICardTemplateAppService(
		IRepository<EmployeeICardTemplate, Guid> repository,
		IEmployeeRepository employeeRepository,
		EmployeeOrganizationScopeResolver organizationScopeResolver,
		IBlobContainer blobContainer,
		IGuidGenerator guidGenerator)
	{
		_repository = repository;
		_employeeRepository = employeeRepository;
		_organizationScopeResolver = organizationScopeResolver;
		_blobContainer = blobContainer;
		_guidGenerator = guidGenerator;
	}

	public async Task<EmployeeICardTemplateListResponse> GetListAsync(
		Guid? academicYearId = null,
		Guid? mediumStageId = null,
		int page = 1,
		int pageSize = 20,
		string? search = null)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

		var query = await _repository.GetQueryableAsync();
		query = query
			.WhereIf(academicYearId.HasValue, x => x.AcademicYearId == academicYearId)
			.WhereIf(mediumStageId.HasValue, x => x.MediumStageId == mediumStageId)
			.WhereIf(!string.IsNullOrWhiteSpace(search), x => x.Name.Contains(search!));

		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip((page - 1) * pageSize)
				.Take(pageSize));

		return new EmployeeICardTemplateListResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			Items = items.Select(Map).ToList()
		};
	}

	public async Task<EmployeeICardTemplateDto> GetAsync(Guid id)
	{
		return Map(await _repository.GetAsync(id));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.Add)]
	public async Task<EmployeeICardTemplateDto> CreateAsync(EmployeeICardTemplateRequest input)
	{
		ValidateFieldsJson(input.FieldsJson);
		ValidateTemplateInput(input);

		var template = new EmployeeICardTemplate(
			_guidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			input.MediumStageId,
			input.Name,
			input.Width,
			input.Height,
			input.BackgroundColor,
			input.BackgroundImageBlobName,
			input.LogoBlobName,
			input.SchoolName,
			input.SchoolAddress,
			input.ThemeColor,
			input.Orientation,
			input.PresetKey,
			input.QrCodeEnabled,
			input.BarcodeEnabled,
			input.BackSideEnabled,
			input.SchemaVersion,
			input.LayoutJson,
			input.PrintSettingsJson,
			input.FieldsJson,
			input.IsActive);

		await _repository.InsertAsync(template, autoSave: true);
		return Map(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.Add)]
	public async Task<EmployeeICardTemplateDto> UpdateAsync(Guid id, EmployeeICardTemplateRequest input)
	{
		ValidateFieldsJson(input.FieldsJson);
		ValidateTemplateInput(input);

		var template = await _repository.GetAsync(id);
		template.Update(
			input.AcademicYearId,
			input.MediumStageId,
			input.Name,
			input.Width,
			input.Height,
			input.BackgroundColor,
			input.BackgroundImageBlobName,
			input.LogoBlobName,
			input.SchoolName,
			input.SchoolAddress,
			input.ThemeColor,
			input.Orientation,
			input.PresetKey,
			input.QrCodeEnabled,
			input.BarcodeEnabled,
			input.BackSideEnabled,
			input.SchemaVersion,
			input.LayoutJson,
			input.PrintSettingsJson,
			input.FieldsJson,
			input.IsActive);

		await _repository.UpdateAsync(template, autoSave: true);
		return Map(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.Add)]
	public async Task<EmployeeICardAssetUploadResult> UploadAssetAsync(string fileName, string contentType, byte[] content)
	{
		ValidateAsset(fileName, contentType, content);

		var sanitizedFileName = Path.GetFileName(fileName);
		var blobName = $"employee-icards/assets/{CurrentTenant.Id ?? Guid.Empty}/{_guidGenerator.Create()}_{sanitizedFileName}";
		await _blobContainer.SaveAsync(blobName, content, overrideExisting: true);

		return new EmployeeICardAssetUploadResult
		{
			BlobName = blobName,
			FileName = sanitizedFileName,
			ContentType = contentType,
			Size = content.LongLength
		};
	}

	public async Task<EmployeeICardAssetDownloadResult> DownloadAssetAsync(string blobName)
	{
		ValidateAssetBlobName(blobName);
		var bytes = await GetBlobBytesAsync(blobName);

		return new EmployeeICardAssetDownloadResult
		{
			Content = bytes,
			FileName = Path.GetFileName(blobName),
			ContentType = GetImageContentType(blobName)
		};
	}

	public async Task<byte[]> GenerateEmployeeICardsPdfAsync(Guid templateId, EmployeeICardPdfRequest input)
	{
		if (input.EmployeeIds.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
		}

		var template = await _repository.GetAsync(templateId);
		var employeeIds = input.EmployeeIds.Distinct().ToList();
		var fields = ParseFieldsJson(template.FieldsJson, allowLegacyFallback: true);
		var layout = EmployeeICardLayoutSchema.ResolveLayout(template);
		var printSettings = EmployeeICardLayoutSchema.ResolvePrintSettings(template.PrintSettingsJson, input.CardsPerPage);
		ValidateTemplateAssetReferences(template.LogoBlobName, template.BackgroundImageBlobName);
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		var employees = await AsyncExecuter.ToListAsync(
			employeeQuery
				.Where(x => employeeIds.Contains(x.Id))
				.OrderBy(x => x.FirstName)
				.ThenBy(x => x.LastName));

		if (employees.Count != employeeIds.Count)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeMissing);
		}
		var organizationsByUserId = await _organizationScopeResolver.GetOrganizationsByUserIdsAsync(
			employees.Select(x => x.UserId),
			template.AcademicYearId);
		if (template.MediumStageId.HasValue &&
			employees.Any(employee => !employee.UserId.HasValue ||
				!organizationsByUserId.TryGetValue(employee.UserId.Value, out var organizations) ||
				organizations.All(x => x.Id != template.MediumStageId.Value)))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeMissing);
		}

		if (input.ExcludeIncompleteEmployees)
		{
			if (string.IsNullOrWhiteSpace(template.LogoBlobName))
			{
				throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
			}

			employees = employees
				.Where(x => !string.IsNullOrWhiteSpace(x.ProfilePictureBlobName) &&
							!string.IsNullOrWhiteSpace(x.MobileNo) &&
							!string.IsNullOrWhiteSpace(x.EmployeeCode) &&
							!string.IsNullOrWhiteSpace(x.Designation))
				.ToList();
		}

		if (employees.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardPdfEmployeeRequired);
		}

		var logoBytes = await TryGetBlobBytesAsync(template.LogoBlobName);
		var logoIsRaster = IsRasterImage(template.LogoBlobName);
		var photoBytesByEmployeeId = new Dictionary<Guid, byte[]?>();
		foreach (var employee in employees)
		{
			photoBytesByEmployeeId[employee.Id] = IsRasterImage(employee.ProfilePictureBlobName)
				? await TryGetBlobBytesAsync(employee.ProfilePictureBlobName)
				: null;
		}
		var cardsPerPage = Math.Clamp(input.CardsPerPage ?? printSettings.CardsPerPage, 1, 10);
		var themeColor = NormalizeColor(template.ThemeColor ?? template.BackgroundColor ?? "#1D4ED8");
		var accentColor = NormalizeColor(template.BackgroundColor ?? "#F8FAFC");
		var cardSize = ResolveCardSize(layout);
		var sheetLayout = ResolveSheetLayout(cardsPerPage, cardSize.Width, cardSize.Height, printSettings);

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
					page.Content().Grid(grid =>
					{
						grid.Columns(sheetLayout.Columns);
						grid.Spacing(printSettings.Gap);

						foreach (var employee in pageEmployees)
						{
							grid.Item().Element(card => ComposeCard(
								card,
								template,
								employee,
								ResolveOrganizationName(employee, organizationsByUserId),
								fields,
								layout,
								logoIsRaster ? logoBytes : null,
								photoBytesByEmployeeId.GetValueOrDefault(employee.Id),
								themeColor,
								accentColor,
								sheetLayout.CardWidth,
								sheetLayout.CardHeight));
						}
					});
				});
			}
		}).GeneratePdf(stream);

		return stream.ToArray();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ICard.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		await _repository.DeleteAsync(id, autoSave: true);
	}

	private static void ValidateFieldsJson(string fieldsJson)
	{
		ParseFieldsJson(fieldsJson, allowLegacyFallback: false);
	}

	private void ValidateTemplateInput(EmployeeICardTemplateRequest input)
	{
		ValidateTemplateAssetReferences(input.LogoBlobName, input.BackgroundImageBlobName);
		ValidateColor(input.ThemeColor);
		ValidateColor(input.BackgroundColor);
		EmployeeICardLayoutSchema.Validate(input.LayoutJson, input.PrintSettingsJson);
	}

	private void ValidateTemplateAssetReferences(params string?[] blobNames)
	{
		foreach (var blobName in blobNames)
		{
			if (!string.IsNullOrWhiteSpace(blobName))
			{
				ValidateAssetBlobName(blobName);
			}
		}
	}

	private static Dictionary<string, bool> ParseFieldsJson(string fieldsJson, bool allowLegacyFallback)
	{
		try
		{
			using var document = JsonDocument.Parse(fieldsJson);
			if (document.RootElement.ValueKind is not JsonValueKind.Array)
			{
				if (allowLegacyFallback)
				{
					return BuildDefaultFields();
				}

				throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
			}

			var fields = BuildDisabledFields();
			foreach (var field in document.RootElement.EnumerateArray())
			{
				if (!field.TryGetProperty("name", out var name) || string.IsNullOrWhiteSpace(name.GetString()))
				{
					throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
				}

				var fieldName = name.GetString()!;
				if (!SupportedFieldNames.Contains(fieldName))
				{
					throw new BusinessException(HRErrorCodes.EmployeeICardFieldSchemaUnsupported);
				}

				var enabled = field.TryGetProperty("enabled", out var enabledValue) && enabledValue.ValueKind == JsonValueKind.False
					? false
					: true;
				fields[fieldName] = enabled;
			}

			foreach (var mandatoryField in MandatoryFieldNames)
			{
				fields[mandatoryField] = true;
			}

			return fields;
		}
		catch (JsonException)
		{
			if (allowLegacyFallback)
			{
				return BuildDefaultFields();
			}

			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}
	}

	private static void ValidateAsset(string fileName, string contentType, byte[] content)
	{
		var extension = Path.GetExtension(fileName);
		if (string.IsNullOrWhiteSpace(fileName) ||
			content.Length == 0 ||
			content.Length > MaxAssetSize ||
			!AllowedAssetTypes.Contains(contentType) ||
			!AllowedAssetExtensions.Contains(extension) ||
			!HasValidImageSignature(extension, content))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardAssetInvalid);
		}
	}

	private static bool HasValidImageSignature(string extension, byte[] content)
	{
		if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
		{
			return content.Length >= 8 &&
				content[0] == 0x89 &&
				content[1] == 0x50 &&
				content[2] == 0x4E &&
				content[3] == 0x47 &&
				content[4] == 0x0D &&
				content[5] == 0x0A &&
				content[6] == 0x1A &&
				content[7] == 0x0A;
		}

		return content.Length >= 3 &&
			content[0] == 0xFF &&
			content[1] == 0xD8 &&
			content[2] == 0xFF;
	}

	private static void ValidateColor(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}

		var color = value.Trim();
		if (!color.StartsWith("#", StringComparison.Ordinal))
		{
			color = $"#{color}";
		}

		var hex = color[1..];
		if (hex.Length is not 6 || hex.Any(character => !Uri.IsHexDigit(character)))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson);
		}
	}

	private void ValidateAssetBlobName(string blobName)
	{
		var expectedPrefix = $"employee-icards/assets/{CurrentTenant.Id ?? Guid.Empty}/";
		var extension = Path.GetExtension(blobName);
		if (string.IsNullOrWhiteSpace(blobName) ||
			!blobName.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
			!AllowedAssetExtensions.Contains(extension))
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardAssetBlobInvalid);
		}
	}

	private async Task<byte[]> GetBlobBytesAsync(string blobName)
	{
		try
		{
			await using var blobStream = await _blobContainer.GetAsync(blobName);
			await using var memoryStream = new MemoryStream();
			await blobStream.CopyToAsync(memoryStream);
			return memoryStream.ToArray();
		}
		catch
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardAssetBlobInvalid);
		}
	}

	private async Task<byte[]?> TryGetBlobBytesAsync(string? blobName)
	{
		if (string.IsNullOrWhiteSpace(blobName))
		{
			return null;
		}

		try
		{
			await using var blobStream = await _blobContainer.GetAsync(blobName);
			await using var memoryStream = new MemoryStream();
			await blobStream.CopyToAsync(memoryStream);
			return memoryStream.ToArray();
		}
		catch
		{
			return null;
		}
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
		var background = NormalizeColor(layout.BackgroundColor ?? template.BackgroundColor ?? "#FFFFFF");
		builder.Append($"""<svg xmlns="http://www.w3.org/2000/svg" width="{layout.Width}" height="{layout.Height}" viewBox="0 0 {layout.Width} {layout.Height}">""");
		builder.Append($"""<rect x="0" y="0" width="{layout.Width}" height="{layout.Height}" rx="8" fill="{Escape(background)}" stroke="#CBD5E1" stroke-width="1"/>""");

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
			? IsFieldEnabled(fields, "photo")
			: IsFieldEnabled(fields, element.FieldKey);
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
		var fill = NormalizeColor(style.BackgroundColor ?? "FFFFFF");
		var stroke = NormalizeColor(style.BorderColor ?? "#CBD5E1");
		var strokeWidth = Math.Max(0, style.BorderWidth ?? 0);
		var radius = Math.Max(0, style.BorderRadius ?? 0);

		return element.Type.ToLowerInvariant() switch
		{
			"shape" => $"""<rect x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" rx="{radius}" fill="{Escape(fill)}" stroke="{Escape(stroke)}" stroke-width="{strokeWidth}"/>""",
			"line" => $"""<line x1="{element.X}" y1="{element.Y}" x2="{element.X + element.W}" y2="{element.Y}" stroke="{Escape(NormalizeColor(style.BackgroundColor ?? style.BorderColor ?? accentColor))}" stroke-width="{Math.Max(1, element.H)}"/>""",
			"logo" => RenderImageOrPlaceholder(element, logoBytes, themeColor, "LOGO"),
			"photo" => RenderImageOrPlaceholder(element, employeePhotoBytes, themeColor, "PHOTO", style.PhotoShape),
			"qr" => RenderEmbeddedSvg(element, BuildQrSvg(BuildIdentityPayload(employee))),
			"barcode" => RenderBarcode(element, employee.EmployeeCode ?? employee.EmployeeName ?? employee.Id.ToString("N")),
			"text" => RenderText(template, employee, organization, element),
			_ => string.Empty
		};
	}

	private static string RenderText(EmployeeICardTemplate template, Employee employee, string? organization, EmployeeICardLayoutElement element)
	{
		var style = element.Style ?? new EmployeeICardElementStyle();
		var value = ResolveFieldValue(template, employee, organization, element.FieldKey, element.Value);
		if (string.IsNullOrWhiteSpace(value))
		{
			value = "-";
		}

		if (style.ShowLabel == true && !string.IsNullOrWhiteSpace(element.Label))
		{
			value = $"{element.Label}{style.Separator ?? ": "}{value}";
		}

		var fontSize = Math.Clamp(style.FontSize ?? 9, 5, 48);
		var color = NormalizeColor(style.Color ?? "#111827");
		var fontWeightValue = style.FontWeight?.ToString();
		var fontWeight = fontWeightValue is "700" or "800" or "900" or "bold" ? "700" : "400";
		var anchor = string.Equals(style.TextAlign, "center", StringComparison.OrdinalIgnoreCase)
			? "middle"
			: string.Equals(style.TextAlign, "right", StringComparison.OrdinalIgnoreCase) ? "end" : "start";
		var x = anchor == "middle" ? element.X + (element.W / 2) : anchor == "end" ? element.X + element.W : element.X;
		var y = element.Y + fontSize;
		var text = Truncate(value, Math.Max(4, (int)(element.W / Math.Max(4, fontSize * 0.55f))));

		return $"""<text x="{x}" y="{y}" font-family="Arial, sans-serif" font-size="{fontSize}" font-weight="{fontWeight}" fill="{Escape(color)}" text-anchor="{anchor}">{Escape(text)}</text>""";
	}

	private static string RenderImageOrPlaceholder(EmployeeICardLayoutElement element, byte[]? bytes, string themeColor, string label, string? shape = null)
	{
		var clipId = $"clip-{element.Id}";
		var radius = string.Equals(shape, "circle", StringComparison.OrdinalIgnoreCase)
			? Math.Min(element.W, element.H) / 2
			: Math.Max(0, element.Style?.BorderRadius ?? 8);
		var border = NormalizeColor(element.Style?.BorderColor ?? themeColor);
		var strokeWidth = Math.Max(0, element.Style?.BorderWidth ?? 1);
		var frame = $"""<rect x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" rx="{radius}" fill="#FFFFFF" stroke="{Escape(border)}" stroke-width="{strokeWidth}"/>""";
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

	private static string RenderEmbeddedSvg(EmployeeICardLayoutElement element, string svg)
	{
		var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
		return $"""<image href="data:image/svg+xml;base64,{encoded}" x="{element.X}" y="{element.Y}" width="{element.W}" height="{element.H}" preserveAspectRatio="xMidYMid meet"/>""";
	}

	private static string BuildQrSvg(string payload)
	{
		using var generator = new QRCodeGenerator();
		using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
		var qr = new SvgQRCode(data);
		return qr.GetGraphic(4);
	}

	private static string RenderBarcode(EmployeeICardLayoutElement element, string value)
	{
		var encoded = $"*{new string(value.ToUpperInvariant().Where(IsCode39Character).ToArray())}*";
		if (encoded.Length <= 2)
		{
			encoded = "*ID*";
		}

		var narrow = Math.Max(1f, element.W / Math.Max(40, encoded.Length * 14));
		var x = element.X;
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
					bars.Append($"""<rect x="{x:0.##}" y="{element.Y}" width="{width:0.##}" height="{Math.Max(8, element.H - 8):0.##}" fill="#111827"/>""");
				}
				x += width;
			}
			x += narrow;
		}

		var text = Escape(Truncate(encoded.Trim('*'), 24));
		return bars + $"""<text x="{element.X + (element.W / 2)}" y="{element.Y + element.H}" font-family="Arial, sans-serif" font-size="6" text-anchor="middle" fill="#111827">{text}</text>""";
	}

	private static string ResolveFieldValue(EmployeeICardTemplate template, Employee employee, string? organization, string? fieldKey, string? fallback)
	{
		var address = string.Join(", ", new[] { employee.Address1, employee.Address2, employee.Address3 }.Where(x => !string.IsNullOrWhiteSpace(x)));
		var employeeName = string.IsNullOrWhiteSpace(employee.EmployeeName) ? employee.ShortName ?? "Employee" : employee.EmployeeName;
		return fieldKey?.ToLowerInvariant() switch
		{
			"name" => employeeName,
			"employeecode" => employee.EmployeeCode,
			"designation" => employee.Designation,
			"department" => employee.Department,
			"organization" => organization,
			"contactno" => employee.MobileNo,
			"bloodgroup" => employee.BloodGroup,
			"validityyear" => template.Name,
			"address" => address,
			"schoolname" => string.IsNullOrWhiteSpace(template.SchoolName) ? template.Name : template.SchoolName,
			"schooladdress" => string.IsNullOrWhiteSpace(template.SchoolAddress) ? organization ?? "School" : template.SchoolAddress,
			"static" => fallback,
			_ => fallback
		};
	}

	private static string BuildIdentityPayload(Employee employee) =>
		$"employee:{employee.Id:N}:{employee.EmployeeCode ?? employee.EmployeeName ?? "unknown"}";

	private static bool IsPng(byte[] bytes) =>
		bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

	private static bool IsCode39Character(char value) =>
		char.IsLetterOrDigit(value) || value is '-' or '.' or ' ' or '$' or '/' or '+' or '%';

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

	private static string Truncate(string value, int maxLength) =>
		value.Length <= maxLength ? value : value[..Math.Max(1, maxLength - 3)] + "...";

	private static string Escape(string value) =>
		WebUtility.HtmlEncode(value);

	private static string? ResolveOrganizationName(
		Employee employee,
		IReadOnlyDictionary<Guid, List<EmployeeOrganizationDto>> organizationsByUserId)
	{
		return employee.UserId.HasValue &&
			organizationsByUserId.TryGetValue(employee.UserId.Value, out var organizations)
				? organizations.FirstOrDefault()?.Name
				: null;
	}

	private static void AddLine(ColumnDescriptor column, string label, string? value)
	{
		column.Item().Text(text =>
		{
			text.Span($"{label}: ").FontSize(7).FontColor(Colors.Grey.Darken1);
			text.Span(string.IsNullOrWhiteSpace(value) ? "-" : value).FontSize(7).SemiBold();
		});
	}

	private static void AddLineIfEnabled(ColumnDescriptor column, IReadOnlyDictionary<string, bool> fields, string fieldName, string label, string? value)
	{
		if (IsFieldEnabled(fields, fieldName))
		{
			AddLine(column, label, value);
		}
	}

	private static bool IsFieldEnabled(IReadOnlyDictionary<string, bool> fields, string fieldName) =>
		fields.TryGetValue(fieldName, out var enabled) && enabled;

	private static Dictionary<string, bool> BuildDefaultFields()
	{
		return SupportedFieldNames.ToDictionary(fieldName => fieldName, fieldName => MandatoryFieldNames.Contains(fieldName), StringComparer.OrdinalIgnoreCase);
	}

	private static Dictionary<string, bool> BuildDisabledFields()
	{
		return SupportedFieldNames.ToDictionary(fieldName => fieldName, _ => false, StringComparer.OrdinalIgnoreCase);
	}

	private static (float Width, float Height) ResolveCardSize(EmployeeICardLayout layout)
	{
		var isVertical = string.Equals(layout.Orientation, VerticalOrientation, StringComparison.OrdinalIgnoreCase);
		var defaultWidth = isVertical ? 160f : 252f;
		var defaultHeight = isVertical ? 252f : 160f;
		if (layout.Width <= 0 || layout.Height <= 0)
		{
			return (defaultWidth, defaultHeight);
		}

		var width = (float)(layout.Width * PreviewPixelToPointScale);
		var height = (float)(layout.Height * PreviewPixelToPointScale);
		if ((isVertical && width > height) || (!isVertical && height > width))
		{
			(width, height) = (height, width);
		}

		return (Math.Clamp(width, 120f, 280f), Math.Clamp(height, 120f, 390f));
	}

	private static (int Columns, float CardWidth, float CardHeight) ResolveSheetLayout(int cardsPerPage, float cardWidth, float cardHeight, EmployeeICardPrintSettings printSettings)
	{
		const float a4Width = 595f;
		const float a4Height = 842f;
		var margin = printSettings.Margin;
		var spacing = printSettings.Gap;
		var availableWidth = a4Width - (margin * 2);
		var availableHeight = a4Height - (margin * 2);
		var columns = Math.Clamp((int) Math.Floor((availableWidth + spacing) / (cardWidth + spacing)), 1, cardsPerPage);
		var rows = (int) Math.Ceiling(cardsPerPage / (double) columns);
		var totalWidth = (columns * cardWidth) + ((columns - 1) * spacing);
		var totalHeight = (rows * cardHeight) + ((rows - 1) * spacing);
		var scale = Math.Min(1f, Math.Min(availableWidth / totalWidth, availableHeight / totalHeight));

		return (columns, cardWidth * scale, cardHeight * scale);
	}

	private static string NormalizeColor(string value) =>
		value.StartsWith("#", StringComparison.Ordinal) ? value : $"#{value}";

	private static string GetImageContentType(string blobName)
	{
		return Path.GetExtension(blobName).ToLowerInvariant() switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".svg" => "image/svg+xml",
			_ => "application/octet-stream"
		};
	}

	private static bool IsRasterImage(string? blobName)
	{
		var extension = Path.GetExtension(blobName ?? string.Empty).ToLowerInvariant();
		return extension is ".png" or ".jpg" or ".jpeg";
	}

	private static EmployeeICardTemplateDto Map(EmployeeICardTemplate template)
	{
		return new EmployeeICardTemplateDto
		{
			Id = template.Id,
			AcademicYearId = template.AcademicYearId,
			MediumStageId = template.MediumStageId,
			Name = template.Name,
			Width = template.Width,
			Height = template.Height,
			BackgroundColor = template.BackgroundColor,
			BackgroundImageBlobName = template.BackgroundImageBlobName,
			LogoBlobName = template.LogoBlobName,
			SchoolName = template.SchoolName,
			SchoolAddress = template.SchoolAddress,
			ThemeColor = template.ThemeColor,
			Orientation = template.Orientation,
			PresetKey = template.PresetKey,
			QrCodeEnabled = template.QrCodeEnabled,
			BarcodeEnabled = template.BarcodeEnabled,
			BackSideEnabled = template.BackSideEnabled,
			SchemaVersion = template.SchemaVersion,
			LayoutJson = template.LayoutJson,
			PrintSettingsJson = template.PrintSettingsJson,
			FieldsJson = template.FieldsJson,
			IsActive = template.IsActive,
			CreationTime = template.CreationTime,
			LastModificationTime = template.LastModificationTime
		};
	}
}
