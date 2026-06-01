using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.ICards;

public class EmployeeICardTemplateDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid? MediumStageId { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Width { get; set; }
	public int Height { get; set; }
	public string? BackgroundColor { get; set; }
	public string? BackgroundImageBlobName { get; set; }
	public string? LogoBlobName { get; set; }
	public string? SchoolName { get; set; }
	public string? SchoolAddress { get; set; }
	public string? ThemeColor { get; set; }
	public string Orientation { get; set; } = "Horizontal";
	public string? PresetKey { get; set; }
	public bool QrCodeEnabled { get; set; }
	public bool BarcodeEnabled { get; set; }
	public bool BackSideEnabled { get; set; }
	public int SchemaVersion { get; set; } = 1;
	public string LayoutJson { get; set; } = string.Empty;
	public string PrintSettingsJson { get; set; } = string.Empty;
	public string FieldsJson { get; set; } = "[]";
	public bool IsActive { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? LastModificationTime { get; set; }
}

public class EmployeeICardTemplateListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public List<EmployeeICardTemplateDto> Items { get; set; } = new();
}

public class EmployeeICardTemplateRequest : IValidatableObject
{
	[Required]
	public Guid AcademicYearId { get; set; }

	public Guid? MediumStageId { get; set; }

	[Required]
	[StringLength(120, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;

	[Range(100, 2000)]
	public int Width { get; set; }

	[Range(100, 2000)]
	public int Height { get; set; }

	[StringLength(32)]
	public string? BackgroundColor { get; set; }

	[StringLength(512)]
	public string? BackgroundImageBlobName { get; set; }

	[StringLength(512)]
	public string? LogoBlobName { get; set; }

	[StringLength(180)]
	public string? SchoolName { get; set; }

	[StringLength(300)]
	public string? SchoolAddress { get; set; }

	[StringLength(32)]
	public string? ThemeColor { get; set; }

	[StringLength(24)]
	public string Orientation { get; set; } = "Horizontal";

	[StringLength(64)]
	public string? PresetKey { get; set; }

	public bool QrCodeEnabled { get; set; }

	public bool BarcodeEnabled { get; set; }

	public bool BackSideEnabled { get; set; }

	[Range(1, 1)]
	public int SchemaVersion { get; set; } = 1;

	public string LayoutJson { get; set; } = string.Empty;

	public string PrintSettingsJson { get; set; } = string.Empty;

	[Required]
	public string FieldsJson { get; set; } = "[]";

	public bool IsActive { get; set; } = true;

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (AcademicYearId == Guid.Empty)
		{
			yield return new ValidationResult("AcademicYearId is required.", new[] { nameof(AcademicYearId) });
		}
		if (MediumStageId == Guid.Empty)
		{
			yield return new ValidationResult("MediumStageId cannot be empty.", new[] { nameof(MediumStageId) });
		}
		if (string.IsNullOrWhiteSpace(FieldsJson))
		{
			yield return new ValidationResult("FieldsJson is required.", new[] { nameof(FieldsJson) });
		}
		if (!string.Equals(Orientation, "Horizontal", StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(Orientation, "Vertical", StringComparison.OrdinalIgnoreCase))
		{
			yield return new ValidationResult("Orientation must be Horizontal or Vertical.", new[] { nameof(Orientation) });
		}
	}
}

public class EmployeeICardAssetUploadResult
{
	public string BlobName { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long Size { get; set; }
}

public class EmployeeICardAssetDownloadResult
{
	public byte[] Content { get; set; } = [];
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
}

public class EmployeeICardPdfRequest : IValidatableObject
{
	[Required]
	public List<Guid> EmployeeIds { get; set; } = new();

	[Range(1, 10)]
	public int? CardsPerPage { get; set; }

	public bool ExcludeIncompleteEmployees { get; set; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (EmployeeIds.Count == 0)
		{
			yield return new ValidationResult("At least one employee is required.", new[] { nameof(EmployeeIds) });
		}
		if (EmployeeIds.Any(x => x == Guid.Empty))
		{
			yield return new ValidationResult("EmployeeIds cannot contain empty values.", new[] { nameof(EmployeeIds) });
		}
	}
}

public interface IEmployeeICardTemplateAppService : IApplicationService
{
	Task<EmployeeICardTemplateListResponse> GetListAsync(
		Guid? academicYearId = null,
		Guid? mediumStageId = null,
		int page = 1,
		int pageSize = 20,
		string? search = null);

	Task<EmployeeICardTemplateDto> GetAsync(Guid id);
	Task<EmployeeICardTemplateDto> CreateAsync(EmployeeICardTemplateRequest input);
	Task<EmployeeICardTemplateDto> UpdateAsync(Guid id, EmployeeICardTemplateRequest input);
	Task<EmployeeICardAssetUploadResult> UploadAssetAsync(string fileName, string contentType, byte[] content);
	Task<EmployeeICardAssetDownloadResult> DownloadAssetAsync(string blobName);
	Task<byte[]> GenerateEmployeeICardsPdfAsync(Guid templateId, EmployeeICardPdfRequest input);
	Task DeleteAsync(Guid id);
}
