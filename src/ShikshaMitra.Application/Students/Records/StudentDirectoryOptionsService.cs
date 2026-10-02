using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentDirectoryOptionsService(IUserScopeAccessService scopes, IRepository<AcademicYear, Guid> years,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions, IRepository<EducationMediumStage, Guid> stages,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards) : ApplicationService, ITransientDependency
{
	public async Task<StudentDirectoryOptionsDto> GetAsync(Guid? academicYearId)
	{
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
		if (!CurrentTenant.Id.HasValue) throw new BusinessException("Students:Records:TenantRequired");
		var scope = await scopes.GetCurrentUserScopeAsync(); var result = new StudentDirectoryOptionsDto();
		if (!scope.HasAnyAccess && !scope.IsAdmin) return result;
		var wide = scope.IsAdmin || scope.HasTenantWideScope();
		var yearQuery = (await years.GetQueryableAsync()).Where(y => y.TenantId == CurrentTenant.Id);
		if (!wide) yearQuery = yearQuery.Where(y => scope.AcademicYearIds.Contains(y.Id));
		result.AcademicYears = await AsyncExecuter.ToListAsync(yearQuery.OrderByDescending(y => y.StartDate).Select(y => new StudentDirectoryOptionDto { Id = y.Id, Name = y.Name }));
		var permittedYears = result.AcademicYears.Select(y => y.Id).ToArray();
		var boardQuery = (await boards.GetQueryableAsync()).Where(b => b.TenantId == CurrentTenant.Id && Enumerable.Contains(permittedYears, b.AcademicYearId) && (!academicYearId.HasValue || b.AcademicYearId == academicYearId));
		var mediumQuery = (await mediums.GetQueryableAsync()).Where(m => m.TenantId == CurrentTenant.Id);
		var stageQuery = (await stages.GetQueryableAsync()).Where(s => s.TenantId == CurrentTenant.Id);
		var standardQuery = (await standards.GetQueryableAsync()).Where(s => s.TenantId == CurrentTenant.Id);
		var divisionQuery = (await divisions.GetQueryableAsync()).Where(d => d.TenantId == CurrentTenant.Id);
		var hierarchy = from standard in standardQuery join stage in stageQuery on standard.EducationMediumStageId equals stage.Id
			join medium in mediumQuery on stage.EducationMediumId equals medium.Id join board in boardQuery on medium.EducationBoardId equals board.Id
			where wide || scope.WildcardBoardAcademicYearIds.Contains(board.AcademicYearId) || scope.WildcardMediumBoardIds.Contains(board.Id) ||
				scope.WildcardMediumStageMediumIds.Contains(medium.Id) || scope.WildcardStandardMediumStageIds.Contains(stage.Id) ||
				scope.WildcardDivisionStandardIds.Contains(standard.Id) || divisionQuery.Any(d => d.StandardId == standard.Id && scope.DivisionIds.Contains(d.Id))
			select new { Standard = standard, Stage = stage, Medium = medium, Board = board };
		var boardOptions = await AsyncExecuter.ToListAsync(hierarchy.Select(h => new { h.Board.Id, h.Board.Type, h.Board.AcademicYearId }).Distinct());
		result.Boards = boardOptions.Select(b => new StudentDirectoryOptionDto { Id = b.Id, Name = b.Type.ToString(), ParentId = b.AcademicYearId }).OrderBy(b => b.Name).ToList();
		result.Mediums = await AsyncExecuter.ToListAsync(hierarchy.Select(h => new StudentDirectoryOptionDto { Id = h.Medium.Id, Name = h.Medium.Name, ParentId = h.Board.Id }).Distinct().OrderBy(o => o.Name));
		result.Standards = await AsyncExecuter.ToListAsync(hierarchy.Select(h => new StudentDirectoryOptionDto { Id = h.Standard.Id, Name = h.Standard.Name, ParentId = h.Medium.Id }).Distinct().OrderBy(o => o.Name));
		result.Divisions = await AsyncExecuter.ToListAsync(from h in hierarchy join d in divisionQuery on h.Standard.Id equals d.StandardId
			where wide || scope.WildcardBoardAcademicYearIds.Contains(h.Board.AcademicYearId) || scope.WildcardMediumBoardIds.Contains(h.Board.Id) || scope.WildcardMediumStageMediumIds.Contains(h.Medium.Id) ||
				scope.WildcardStandardMediumStageIds.Contains(h.Stage.Id) || scope.WildcardDivisionStandardIds.Contains(h.Standard.Id) || scope.DivisionIds.Contains(d.Id)
			orderby d.Name select new StudentDirectoryOptionDto { Id = d.Id, Name = d.Name, ParentId = h.Standard.Id });
		return result;
	}
}
