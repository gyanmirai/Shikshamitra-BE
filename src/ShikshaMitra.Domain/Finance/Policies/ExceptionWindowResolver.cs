using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>The active exception window's effect, resolved for a specific scope + date — the
/// shape LateFeeCalculationInput expects (Phase 4j).</summary>
public sealed record ActiveExceptionWindow(ExceptionWindowEffect Effect, int? ExtensionDays, string Label);

/// <summary>
/// Resolves the single active CollectionPolicyExceptionWindow (if any) covering a given
/// academic year / fee structure as of a date — the DB-touching counterpart to
/// LateFeeCalculationService staying a pure function (implementation plan §4j). Callers resolve
/// once per collect-form/simulate/waiver call and pass the result into LateFeeCalculationInput,
/// mirroring how CollectionPolicyResolver already works for the policy itself.
/// </summary>
public class ExceptionWindowResolver(
	IRepository<CollectionPolicyExceptionWindow, Guid> windowRepository
) : ITransientDependency
{
	public async Task<ActiveExceptionWindow?> ResolveActiveWindowAsync(
		Guid academicYearId, Guid? feeStructureId, DateTime asOfDate)
	{
		var query = await windowRepository.GetQueryableAsync();
		var date = asOfDate.Date;

		// ClassSection-scoped windows are modeled but not yet resolvable here — no class/section
		// context is threaded through the late-fee calculation call chain today (Fee Collection,
		// /simulate, waiver approval all operate per-student/per-due-line, not per-section). A
		// ClassSection window is simply never matched rather than silently misapplied to the
		// wrong students; extending the call chain to carry section context is a follow-up.
		var candidates = query
			.Where(w => w.StartDate <= date && w.EndDate >= date)
			.Where(w =>
				w.Scope == ExceptionWindowScope.SchoolWide ||
				(w.Scope == ExceptionWindowScope.AcademicYear && w.ScopeId == academicYearId) ||
				(w.Scope == ExceptionWindowScope.FeeStructure && feeStructureId != null && w.ScopeId == feeStructureId))
			.ToList();

		if (candidates.Count == 0)
		{
			return null;
		}

		// Most specific scope wins when more than one window is active at once — FeeStructure >
		// AcademicYear > SchoolWide, matching the enum's declared ordering.
		var best = candidates.OrderByDescending(w => (int)w.Scope).First();

		return new ActiveExceptionWindow(best.Effect, best.ExtensionDays, DescribeScope(best));
	}

	private static string DescribeScope(CollectionPolicyExceptionWindow window) => window.Scope switch
	{
		ExceptionWindowScope.SchoolWide => $"school-wide exception window ({window.ReasonCode})",
		ExceptionWindowScope.AcademicYear => $"academic-year exception window ({window.ReasonCode})",
		ExceptionWindowScope.FeeStructure => $"fee-structure exception window ({window.ReasonCode})",
		_ => $"exception window ({window.ReasonCode})"
	};
}
