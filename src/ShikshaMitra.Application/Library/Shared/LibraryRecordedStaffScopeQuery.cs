using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Shared;

public class LibraryRecordedStaffScopeQuery(IRepository<LibraryBorrowerScopeCapture, Guid> captures,
	IRepository<LibraryBorrowerAssignmentSnapshot, Guid> assignments, ICurrentTenant tenant) : ITransientDependency
{
	public async Task<IQueryable<LibraryBorrowerScopeCapture>> GetReadableCapturesAsync(UserScopeContext scope)
	{
		var query = (await captures.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id && row.IsComplete && row.Version > 0 &&
			(row.Provenance == LibraryBorrowerScopeProvenance.CapturedAtOperation || row.Provenance == LibraryBorrowerScopeProvenance.ReviewedLegacy));
		if (!tenant.Id.HasValue || scope.TenantId != tenant.Id || !scope.HasAnyAccess) return query.Where(_ => false);
		var stageIds = scope.MediumStageIds.Concat(scope.WildcardStandardMediumStageIds).Distinct().ToArray();
		var mediumIds = scope.WildcardMediumStageMediumIds.ToArray();
		var boardIds = scope.WildcardMediumBoardIds.ToArray();
		var yearIds = scope.WildcardBoardAcademicYearIds.ToArray();
		var tuples = (await assignments.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		return query.Where(capture => tuples.Count(tuple => tuple.CaptureId == capture.Id && tuple.TenantId == capture.TenantId) == capture.ExpectedAssignmentCount &&
			tuples.Any(tuple => tuple.CaptureId == capture.Id && tuple.TenantId == capture.TenantId &&
			tuple.AcademicYearId == capture.AcademicYearId && tuple.BoardId.HasValue && tuple.MediumId.HasValue &&
			(Enumerable.Contains(stageIds, tuple.StageId) || Enumerable.Contains(mediumIds, tuple.MediumId.Value) ||
			 Enumerable.Contains(boardIds, tuple.BoardId.Value) || Enumerable.Contains(yearIds, capture.AcademicYearId))));
	}
}
