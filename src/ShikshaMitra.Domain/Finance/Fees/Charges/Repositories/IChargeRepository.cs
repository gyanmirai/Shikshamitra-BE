using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Holds the result of a Charge joined with all its related entities.
/// Used to avoid re-querying the same multi-table join multiple times.
/// </summary>
public record ChargeJoinResult(
	Charge Charge,
	Student Student,
	ChargeCategory Category,
	Standard Standard,
	Division Division,
	EducationBoard Board,
	EducationMedium Medium,
	AcademicYear AcademicYear,
	IdentityUser Requestor
);

/// <summary>
/// Domain repository for <see cref="Charge"/> with charge-specific query methods.
/// Follows the same pattern as <c>IEmployeeRepository</c>.
/// </summary>
public interface IChargeRepository : IRepository<Charge, Guid>
{
	/// <summary>
	/// Returns a charge with all related entities (student, category, scope, requestor)
	/// joined in a single query, or <c>null</c> if not found.
	/// </summary>
	/// <param name="chargeId">The charge ID to look up.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task<ChargeJoinResult?> GetChargeWithDetailsAsync(
		Guid chargeId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a queryable join of charges with all related entities for a given academic year.
	/// All DbSets use <c>IgnoreAutoIncludes()</c> to prevent circular/duplicate JOINs from
	/// EF Core scalar AutoIncludes configured on Standard, Student, etc.
	/// All filter predicates are applied on entity properties <b>before</b> the final projection
	/// so EF Core can translate the entire query to SQL without client-side evaluation.
	/// </summary>
	Task<IQueryable<ChargeJoinResult>> GetChargesQueryAsync(
		Guid academicYearId,
		int status,
		Guid? boardId = null,
		Guid? educationMediumId = null,
		string? search = null);
}
