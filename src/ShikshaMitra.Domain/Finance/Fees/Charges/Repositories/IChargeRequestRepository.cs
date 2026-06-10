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
	int ApprovedCount,
	int RejectedCount);

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
	/// </summary>
	Task<(List<ChargeRequestJoinResult> Items, int TotalCount)> GetChargeRequestsPagedAsync(
		Guid academicYearId,
		int status,
		int page,
		int pageSize,
		Guid? boardId = null,
		Guid? educationMediumId = null,
		string? search = null,
		CancellationToken cancellationToken = default);

	Task<IQueryable<ChargeLineJoinResult>> GetChargeLinesQueryAsync(
		Guid academicYearId,
		int? status = null,
		Guid? boardId = null,
		Guid? educationMediumId = null);

	Task<ChargeRequestStatusCounts> GetStatusCountsAsync(
		Guid academicYearId,
		CancellationToken cancellationToken = default);
}

public record ChargeRequestStatusCounts(int Pending, int Approved, int Rejected);
