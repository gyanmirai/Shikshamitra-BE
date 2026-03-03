using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public interface IStudentListAppService : IApplicationService
{
	/// <summary>
	/// Changes the operational status of an enrolled student and records an
	/// audited history entry with the provided remarks.
	/// </summary>
	Task ChangeStudentStatusAsync(Guid studentId, ChangeStudentStatusDto input);

	/// <summary>
	/// Returns the full status-change history for a student, newest first.
	/// </summary>
	Task<List<StudentStatusHistoryDto>> GetStatusHistoryAsync(Guid studentId);
}
