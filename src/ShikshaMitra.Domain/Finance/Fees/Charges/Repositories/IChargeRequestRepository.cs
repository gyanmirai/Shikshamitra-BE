using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Finance.Fees.Charges;

public record ChargeRequestJoinResult(
	ChargeRequest Request,
	ChargeCategory Category,
	IdentityUser Requestor,
	AcademicYear AcademicYear,
	int StudentCount,
	int PendingCount,
	int ProcessedCount,
	ChargeWorkflowStatus WorkflowStatus = ChargeWorkflowStatus.Pending);

public record ChargeRequestDetailResult(
	ChargeRequest Request,
	ChargeCategory Category,
	IdentityUser Requestor,
	AcademicYear AcademicYear,
	IReadOnlyList<ChargeLineJoinResult> Lines);

public record ChargeLineJoinResult(
	Charge Charge,
	Student Student,
	Standard Standard,
	Division Division);

public interface IChargeRequestRepository : IRepository<ChargeRequest, Guid>
{
	Task<ChargeRequestDetailResult?> GetWithLinesAsync(
		Guid chargeRequestId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a paged list of charge requests with per-request student counts,
	/// along with the total filtered count. Handles ordering and pagination
	/// inside the repository to avoid EF Core correlated-subquery translation errors.
	/// status: 0 = Pending, 1 = In Process, 2 = Completed, 3 = Rejected.
	/// </summary>
	Task<(List<ChargeRequestJoinResult> Items, int TotalCount)> GetChargeRequestsPagedAsync(
		Guid academicYearId,
		int status,
		int page,
		int pageSize,
		Guid? boardId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		string? search = null,
		string? sortBy = null,
		string? sortOrder = null,
		IReadOnlyCollection<Guid>? scopedStudentIds = null,
		CancellationToken cancellationToken = default,
		IReadOnlyCollection<ChargeColumnFilter>? columnFilters = null);

	Task<IQueryable<ChargeLineJoinResult>> GetChargeLinesQueryAsync(
		Guid academicYearId,
		int? status = null,
		Guid? boardId = null,
		Guid? educationMediumId = null);

	Task<ChargeRequestStatusCounts> GetStatusCountsAsync(
		Guid academicYearId,
		Guid? boardId = null,
		Guid? educationMediumId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		IReadOnlyCollection<Guid>? scopedStudentIds = null,
		CancellationToken cancellationToken = default,
		string? search = null,
		IReadOnlyCollection<ChargeColumnFilter>? columnFilters = null);
}

public record ChargeRequestStatusCounts(int Pending, int Processed, int Completed = 0, int Rejected = 0);
