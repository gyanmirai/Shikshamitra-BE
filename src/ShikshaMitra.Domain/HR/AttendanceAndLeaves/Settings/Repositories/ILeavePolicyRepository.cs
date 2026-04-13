using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface ILeavePolicyRepository : IRepository<LeavePolicy, Guid>
{
	Task<bool> IsPolicyNameExistsAsync(Guid academicYearId, string policyName, Guid? excludeId = null);

	Task<LeavePolicy?> FindByPolicyNameAsync(Guid academicYearId, string policyName);

	Task<List<LeavePolicy>> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int skipCount = 0,
		int maxResultCount = int.MaxValue,
		string? sorting = null,
		string? search = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default);

	Task<long> GetCountByAcademicYearIdAsync(
		Guid academicYearId,
		string? search = null,
		bool includeInactive = false,
		CancellationToken cancellationToken = default);

	Task<LeavePolicy?> GetWithLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default);
}
