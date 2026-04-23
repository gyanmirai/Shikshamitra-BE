using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public interface IAttendanceIntegrationRunAppService : IApplicationService
{
	Task<AttendanceIntegrationRunResponseDto> RecordIntegrationRunAsync(AttendanceIntegrationRunRequestDto input);
}

public class AttendanceIntegrationRunRequestDto
{
	public string FileName { get; set; } = string.Empty;
	public AttendanceImportRunSource RunSource { get; set; } = AttendanceImportRunSource.Integration;
	public string? ExternalCorrelationId { get; set; }
	public string? IntegrationProvider { get; set; }
	public List<AttendanceIntegrationRunRowDto> Rows { get; set; } = [];
}

public class AttendanceIntegrationRunRowDto
{
	public Guid EmployeeId { get; set; }
	public string AttendanceDate { get; set; } = string.Empty;
	public string? TimeIn { get; set; }
	public string? TimeOut { get; set; }
	public EmployeeAttendanceStatusType Status { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public string? Remarks { get; set; }
}

public class AttendanceIntegrationRunResponseDto
{
	public Guid BatchId { get; set; }
	public int CreatedCount { get; set; }
	public int ReplacedCount { get; set; }
}