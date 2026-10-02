using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public class StudentRecordReportInput : PagedResultRequestDto
{
	public string ReportType { get; set; } = "Directory";
	public Guid? AcademicYearId { get; set; }
	public Guid? StudentId { get; set; }
	public string? Status { get; set; }
	[StringLength(80)] public string ClientKey { get; set; } = string.Empty;
}
public class StudentRecordReportPreviewDto
{
	public List<string> Columns { get; set; } = [];
	public List<Dictionary<string, string?>> Rows { get; set; } = [];
	public long TotalCount { get; set; }
	public DateTimeOffset AsOf { get; set; }
}
public class StudentRecordReportJobDto
{
	public Guid Id { get; set; }
	public string ReportType { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string? ErrorCode { get; set; }
	public int? RowCount { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTimeOffset ExpiresAt { get; set; }
	public bool CanDownload { get; set; }
	public StudentRecordReportInput Request { get; set; } = new();
}
public interface IStudentRecordReportAppService : IApplicationService
{
	Task<StudentRecordReportPreviewDto> PreviewAsync(StudentRecordReportInput input);
	Task<StudentRecordReportJobDto> CreateAsync(StudentRecordReportInput input);
	Task<PagedResultDto<StudentRecordReportJobDto>> GetListAsync(PagedResultRequestDto input);
	Task<StudentRecordReportJobDto> CancelAsync(Guid id);
	Task<StudentPhotoDownloadDto> DownloadAsync(Guid id);
}
