using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

public interface IAttendanceImportAppService
{
	Task<AttendanceImportPreviewResponseDto> PreviewAsync(string fileName, string? contentType, byte[] fileContent);
	Task<AttendanceImportConfirmResponseDto> ConfirmAsync(AttendanceImportConfirmRequestDto input);
	Task<AttendanceImportTemplateDto> GetTemplateAsync();
}

public class AttendanceImportConfirmRequestDto
{
	public string PreviewToken { get; set; } = string.Empty;
}

public class AttendanceImportPreviewResponseDto
{
	public string? PreviewToken { get; set; }
	public bool CanConfirm { get; set; }
	public AttendanceImportSummaryDto Summary { get; set; } = new();
	public List<AttendanceImportRowResultDto> Rows { get; set; } = [];
}

public class AttendanceImportConfirmResponseDto
{
	public int ProcessedRows { get; set; }
	public int CreatedCount { get; set; }
	public int ReplacedCount { get; set; }
}

public class AttendanceImportTemplateDto
{
	public string FileName { get; set; } = "employee-attendance-import-template.xlsx";
	public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
	public byte[] Content { get; set; } = [];
}

public class AttendanceImportSummaryDto
{
	public int TotalRows { get; set; }
	public int ValidRows { get; set; }
	public int InvalidRows { get; set; }
	public int CreateCount { get; set; }
	public int ReplaceCount { get; set; }
}

public class AttendanceImportRowResultDto
{
	public int RowNumber { get; set; }
	public string Action { get; set; } = "invalid";
	public string AttendanceDate { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string? EmployeeName { get; set; }
	public string? ContactNo { get; set; }
	public string? TimeIn { get; set; }
	public string? TimeOut { get; set; }
	public string? Status { get; set; }
	public string? Remarks { get; set; }
	public List<string> Errors { get; set; } = [];
	public List<string> Warnings { get; set; } = [];
}
