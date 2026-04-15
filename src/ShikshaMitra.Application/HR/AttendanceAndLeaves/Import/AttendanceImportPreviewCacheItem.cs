using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceImportPreviewCacheItem
{
	public string Token { get; set; } = string.Empty;
	public Guid? TenantId { get; set; }
	public Guid UserId { get; set; }
	public Guid ImportBatchId { get; set; }
	public List<AttendanceImportPreviewCacheRow> Rows { get; set; } = [];
}

public class AttendanceImportPreviewCacheRow
{
	public Guid EmployeeId { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public DateOnly AttendanceDate { get; set; }
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }
	public EmployeeAttendanceStatusType StatusType { get; set; }
}
