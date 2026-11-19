using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

[RemoteService(false)]
public class AcademicHierarchyQueryScope(IUserScopeAccessService access) : ApplicationService
{
	public async Task<IQueryable<EducationBoard>> BoardsAsync(IQueryable<EducationBoard> query)
	{
		var tenant = CurrentTenant.Id;
		query = query.Where(x => tenant.HasValue && x.TenantId == tenant && x.AcademicYear.TenantId == tenant);
		var scope = await access.GetCurrentUserScopeAsync();
		if (scope.HasTenantWideScope()) return query;
		return query.Where(b => Enumerable.Contains(scope.WildcardBoardAcademicYearIds, b.AcademicYearId)
			|| Enumerable.Contains(scope.WildcardMediumBoardIds, b.Id)
			|| b.EducationMediums.Any(m => m.TenantId == tenant && (Enumerable.Contains(scope.WildcardMediumStageMediumIds, m.Id)
				|| m.EducationMediumStages.Any(s => s.TenantId == tenant && (Enumerable.Contains(scope.WildcardStandardMediumStageIds, s.Id)
					|| s.Standards.Any(c => c.TenantId == tenant && (Enumerable.Contains(scope.WildcardDivisionStandardIds, c.Id)
						|| Enumerable.Contains(scope.StandardIds, c.Id) || c.Divisions.Any(d => d.TenantId == tenant && Enumerable.Contains(scope.DivisionIds, d.Id)))))))));
	}
	public async Task<IQueryable<EducationMedium>> MediumsAsync(IQueryable<EducationMedium> query)
	{
		var tenant = CurrentTenant.Id;
		query = query.Where(x => tenant.HasValue && x.TenantId == tenant && x.EducationBoard.TenantId == tenant);
		var scope = await access.GetCurrentUserScopeAsync();
		if (scope.HasTenantWideScope()) return query;
		return query.Where(m => Enumerable.Contains(scope.WildcardBoardAcademicYearIds, m.EducationBoard.AcademicYearId)
			|| Enumerable.Contains(scope.WildcardMediumBoardIds, m.EducationBoardId)
			|| Enumerable.Contains(scope.WildcardMediumStageMediumIds, m.Id)
			|| m.EducationMediumStages.Any(s => s.TenantId == tenant && (Enumerable.Contains(scope.WildcardStandardMediumStageIds, s.Id)
				|| s.Standards.Any(c => c.TenantId == tenant && (Enumerable.Contains(scope.WildcardDivisionStandardIds, c.Id)
					|| Enumerable.Contains(scope.StandardIds, c.Id) || c.Divisions.Any(d => d.TenantId == tenant && Enumerable.Contains(scope.DivisionIds, d.Id)))))));
	}
	public async Task<IQueryable<EducationMediumStage>> StagesAsync(IQueryable<EducationMediumStage> query)
	{
		var tenant = CurrentTenant.Id;
		query = query.Where(x => tenant.HasValue && x.TenantId == tenant && x.EducationMedium.TenantId == tenant && x.EducationMedium.EducationBoard.TenantId == tenant);
		var scope = await access.GetCurrentUserScopeAsync();
		if (scope.HasTenantWideScope()) return query;
		return query.Where(s => Enumerable.Contains(scope.WildcardBoardAcademicYearIds, s.EducationMedium.EducationBoard.AcademicYearId)
			|| Enumerable.Contains(scope.WildcardMediumBoardIds, s.EducationMedium.EducationBoardId)
			|| Enumerable.Contains(scope.WildcardMediumStageMediumIds, s.EducationMediumId)
			|| Enumerable.Contains(scope.WildcardStandardMediumStageIds, s.Id)
			|| s.Standards.Any(c => c.TenantId == tenant && (Enumerable.Contains(scope.WildcardDivisionStandardIds, c.Id)
				|| Enumerable.Contains(scope.StandardIds, c.Id) || c.Divisions.Any(d => d.TenantId == tenant && Enumerable.Contains(scope.DivisionIds, d.Id)))));
	}
	public async Task<IQueryable<Standard>> StandardsAsync(IQueryable<Standard> query)
	{
		var tenant = CurrentTenant.Id;
		query = query.Where(x => tenant.HasValue && x.TenantId == tenant && x.EducationMediumStage.TenantId == tenant
			&& x.EducationMediumStage.EducationMedium.TenantId == tenant && x.EducationMediumStage.EducationMedium.EducationBoard.TenantId == tenant);
		var scope = await access.GetCurrentUserScopeAsync();
		if (scope.HasTenantWideScope()) return query;
		return query.Where(c => Enumerable.Contains(scope.WildcardBoardAcademicYearIds, c.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId)
			|| Enumerable.Contains(scope.WildcardMediumBoardIds, c.EducationMediumStage.EducationMedium.EducationBoardId)
			|| Enumerable.Contains(scope.WildcardMediumStageMediumIds, c.EducationMediumStage.EducationMediumId)
			|| Enumerable.Contains(scope.WildcardStandardMediumStageIds, c.EducationMediumStageId)
			|| Enumerable.Contains(scope.WildcardDivisionStandardIds, c.Id) || Enumerable.Contains(scope.StandardIds, c.Id)
			|| c.Divisions.Any(d => d.TenantId == tenant && Enumerable.Contains(scope.DivisionIds, d.Id)));
	}
	public async Task<IQueryable<Division>> DivisionsAsync(IQueryable<Division> query)
	{
		var tenant = CurrentTenant.Id;
		query = query.Where(x => tenant.HasValue && x.TenantId == tenant && x.Standard.TenantId == tenant
			&& x.Standard.EducationMediumStage.TenantId == tenant && x.Standard.EducationMediumStage.EducationMedium.TenantId == tenant
			&& x.Standard.EducationMediumStage.EducationMedium.EducationBoard.TenantId == tenant);
		var scope = await access.GetCurrentUserScopeAsync();
		if (scope.HasTenantWideScope()) return query;
		return query.Where(d => Enumerable.Contains(scope.WildcardBoardAcademicYearIds, d.Standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId)
			|| Enumerable.Contains(scope.WildcardMediumBoardIds, d.Standard.EducationMediumStage.EducationMedium.EducationBoardId)
			|| Enumerable.Contains(scope.WildcardMediumStageMediumIds, d.Standard.EducationMediumStage.EducationMediumId)
			|| Enumerable.Contains(scope.WildcardStandardMediumStageIds, d.Standard.EducationMediumStageId)
			|| Enumerable.Contains(scope.WildcardDivisionStandardIds, d.StandardId) || Enumerable.Contains(scope.DivisionIds, d.Id));
	}
}
