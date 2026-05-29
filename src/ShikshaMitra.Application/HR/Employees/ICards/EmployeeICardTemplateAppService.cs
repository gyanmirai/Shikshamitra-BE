using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
			employees = employees
				.Where(x => !string.IsNullOrWhiteSpace(x.ProfilePictureBlobName) &&
							!string.IsNullOrWhiteSpace(x.MobileNo) &&
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
		var cardsPerPage = Math.Clamp(input.CardsPerPage, 1, 10);
		var themeColor = NormalizeColor(template.ThemeColor ?? template.BackgroundColor ?? "#1D4ED8");
		var accentColor = NormalizeColor(template.BackgroundColor ?? "#F8FAFC");
		var cardSize = ResolveCardSize(template);
		var sheetLayout = ResolveSheetLayout(cardsPerPage, cardSize.Width, cardSize.Height);

		using var stream = new MemoryStream();
		Document.Create(container =>
		{
			foreach (var pageEmployees in employees.Chunk(cardsPerPage))
			{
				container.Page(page =>
				{
					page.Size(PageSizes.A4);
					page.Margin(18);
					page.DefaultTextStyle(x => x.FontSize(8).FontColor("#111827"));
					page.Content().Grid(grid =>
					{
						grid.Columns(sheetLayout.Columns);
						grid.Spacing(10);

						foreach (var employee in pageEmployees)
						{
							grid.Item().Element(card => ComposeCard(
								card,
								template,
								employee,
								ResolveOrganizationName(employee, organizationsByUserId),
								fields,
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
		byte[]? logoBytes,
		byte[]? employeePhotoBytes,
		string themeColor,
		string accentColor,
		float cardWidth,
		float cardHeight)
	{
		var address = string.Join(", ", new[] { employee.Address1, employee.Address2, employee.Address3 }.Where(x => !string.IsNullOrWhiteSpace(x)));
		var employeeName = string.IsNullOrWhiteSpace(employee.EmployeeName) ? employee.ShortName ?? "Employee" : employee.EmployeeName;
		var schoolName = string.IsNullOrWhiteSpace(template.SchoolName) ? template.Name : template.SchoolName;
		var schoolAddress = string.IsNullOrWhiteSpace(template.SchoolAddress) ? organization ?? "School" : template.SchoolAddress;

		container
			.Width(cardWidth)
			.Height(cardHeight)
			.Border(1)
			.BorderColor("#CBD5E1")
			.Background(NormalizeColor(template.BackgroundColor ?? "#FFFFFF"))
			.Padding(8)
			.Column(column =>
			{
				column.Spacing(6);
				column.Item().Row(row =>
				{
					if (logoBytes is { Length: > 0 })
					{
						row.ConstantItem(34).Height(34).Image(logoBytes).FitArea();
						row.ConstantItem(8);
					}

					row.RelativeItem().Column(header =>
					{
						header.Item().Text(schoolName).Bold().FontSize(11).FontColor(themeColor);
						header.Item().Text(schoolAddress).FontSize(7).FontColor(Colors.Grey.Darken1);
					});
				});

				column.Item().LineHorizontal(1).LineColor(accentColor);
				column.Item().Row(row =>
				{
					if (IsFieldEnabled(fields, "photo"))
					{
						row.ConstantItem(58).Height(68).Element(photo =>
						{
							var frame = photo.Border(1).BorderColor("#CBD5E1").Background("#F8FAFC");
							if (employeePhotoBytes is { Length: > 0 })
							{
								frame.Image(employeePhotoBytes).FitArea();
							}
							else
							{
								frame.AlignCenter().AlignMiddle().Text("PHOTO").FontSize(8).FontColor(Colors.Grey.Darken1);
							}
						});
						row.ConstantItem(10);
					}
					row.RelativeItem().Column(details =>
					{
						details.Spacing(3);
						if (IsFieldEnabled(fields, "name"))
						{
							details.Item().Text(employeeName).Bold().FontSize(12).FontColor("#0F172A");
						}
						AddLineIfEnabled(details, fields, "employeeCode", "Code", employee.EmployeeCode);
						AddLineIfEnabled(details, fields, "designation", "Designation", employee.Designation);
						AddLineIfEnabled(details, fields, "department", "Department", employee.Department);
						AddLineIfEnabled(details, fields, "organization", "Organization", organization);
						AddLineIfEnabled(details, fields, "contactNo", "Mobile", employee.MobileNo);
						AddLineIfEnabled(details, fields, "bloodGroup", "Blood Group", employee.BloodGroup);
						AddLineIfEnabled(details, fields, "validityYear", "Valid", template.Name);
					});
				});

				if (IsFieldEnabled(fields, "address") && !string.IsNullOrWhiteSpace(address))
				{
					column.Item().Text(address).FontSize(6).FontColor(Colors.Grey.Darken1);
				}

				if (template.QrCodeEnabled || template.BarcodeEnabled)
				{
					column.Item().AlignRight().Text(template.QrCodeEnabled ? $"QR: {employee.EmployeeCode}" : $"BAR: {employee.EmployeeCode}").FontSize(6).FontColor(Colors.Grey.Darken1);
				}
			});
	}

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

	private static (float Width, float Height) ResolveCardSize(EmployeeICardTemplate template)
	{
		var isVertical = string.Equals(template.Orientation, VerticalOrientation, StringComparison.OrdinalIgnoreCase);
		var defaultWidth = isVertical ? 160f : 252f;
		var defaultHeight = isVertical ? 252f : 160f;
		if (template.Width <= 0 || template.Height <= 0)
		{
			return (defaultWidth, defaultHeight);
		}

		var width = (float)(template.Width * PreviewPixelToPointScale);
		var height = (float)(template.Height * PreviewPixelToPointScale);
		if ((isVertical && width > height) || (!isVertical && height > width))
		{
			(width, height) = (height, width);
		}

		return (Math.Clamp(width, 120f, 280f), Math.Clamp(height, 120f, 390f));
	}

	private static (int Columns, float CardWidth, float CardHeight) ResolveSheetLayout(int cardsPerPage, float cardWidth, float cardHeight)
	{
		const float a4Width = 595f;
		const float a4Height = 842f;
		const float margin = 18f;
		const float spacing = 10f;
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
			FieldsJson = template.FieldsJson,
			IsActive = template.IsActive,
			CreationTime = template.CreationTime,
			LastModificationTime = template.LastModificationTime
		};
	}
}
