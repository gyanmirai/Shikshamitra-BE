using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.ICards;

public class EmployeeICardTemplateDto
{
	public ShikshaMitra.TenantSettings.SchoolIdentity.SchoolIdentityAssetMode? LogoMode { get; set; }
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

public class EmployeeICardTemplateRequest
{
	public ShikshaMitra.TenantSettings.SchoolIdentity.SchoolIdentityAssetMode? LogoMode { get; set; }
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

	public bool IsActive { get; set; } = true;
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

public class EmployeeICardPdfRequest
{
	public List<Guid> EmployeeIds { get; set; } = new();

	public int? CardsPerPage { get; set; }

	public bool ExcludeIncompleteEmployees { get; set; }
}
