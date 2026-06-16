using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public interface IStudentListAppService : IApplicationService
{
	Task<PagedResultDto<StudentDto>> GetStudentsByDivisionAsync(Guid divisionId, int skipCount = 0, int maxResultCount = 10, string? searchTerm = null);
	Task<StudentPhotoUploadResultDto> UploadPhotoAsync(Guid studentId, UploadStudentPhotoDto input);
	Task<StudentPhotoDownloadDto?> GetPhotoAsync(Guid studentId);
	Task<StudentDashboardSummaryDto> GetDashboardSummaryAsync();
	Task<StudentAttendanceDashboardSummaryDto> GetAttendanceDashboardSummaryAsync(GetStudentAttendanceDashboardSummaryInput input);

	/// <summary>
	/// Changes the operational status of an enrolled student and records an
	/// audited history entry with the provided remarks.
	/// </summary>
	Task ChangeStudentStatusAsync(Guid studentId, ChangeStudentStatusDto input);

	/// <summary>
	/// Returns the full status-change history for a student, newest first.
	/// </summary>
	Task<List<StudentStatusHistoryDto>> GetStatusHistoryAsync(Guid studentId);

	/// <summary>
	/// Retrieves enrolled students matching the specified academic scope.
	/// </summary>
	Task<List<StudentLookupDto>> GetStudentsByScopeAsync(GetStudentsByScopeRequest input);
}
