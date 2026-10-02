using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
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
public class StudentRecordsEnrollmentScopeService(IUserScopeAccessService scopes,
	IRepository<StudentEnrollment, Guid> enrollments, IRepository<Standard, Guid> standards,
	IRepository<EducationMediumStage, Guid> stages, IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationBoard, Guid> boards, IRepository<Division, Guid> divisions, StudentScopeAccessService studentScope) : ApplicationService, ITransientDependency
{
	public async Task<IQueryable<StudentEnrollment>> GetQueryableAsync()
	{
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
		var query = (await enrollments.GetQueryableAsync()).Where(e => e.TenantId == CurrentTenant.Id);
		if (!CurrentTenant.Id.HasValue) return query.Where(_ => false);
		var scope = await scopes.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasTenantWideScope()) return query;
		if (!scope.HasAnyAccess) return query.Where(_ => false);
		var standardQuery = await standards.GetQueryableAsync();
		var stageQuery = await stages.GetQueryableAsync();
		var mediumQuery = await mediums.GetQueryableAsync();
		var boardQuery = await boards.GetQueryableAsync();
		return from e in query
			join s in standardQuery.Where(s => s.TenantId == CurrentTenant.Id) on e.StandardId equals (Guid?)s.Id
			join stage in stageQuery.Where(s => s.TenantId == CurrentTenant.Id) on s.EducationMediumStageId equals stage.Id
			join medium in mediumQuery.Where(m => m.TenantId == CurrentTenant.Id) on stage.EducationMediumId equals medium.Id
			join board in boardQuery.Where(b => b.TenantId == CurrentTenant.Id) on medium.EducationBoardId equals board.Id
			where scope.AcademicYearIds.Contains(e.AcademicYearId) &&
				(scope.WildcardBoardAcademicYearIds.Contains(e.AcademicYearId) || scope.WildcardMediumBoardIds.Contains(board.Id) ||
				 scope.WildcardMediumStageMediumIds.Contains(medium.Id) || scope.WildcardStandardMediumStageIds.Contains(stage.Id) ||
				 scope.WildcardDivisionStandardIds.Contains(s.Id) || (e.DivisionId.HasValue && scope.DivisionIds.Contains(e.DivisionId.Value)))
			select e;
	}

	public async Task<IQueryable<Student>> ApplyStudentScopeAsync(IQueryable<Student> query)
	{
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
		query = query.Where(s => s.TenantId == CurrentTenant.Id);
		if (!CurrentTenant.Id.HasValue) return query.Where(_ => false);
		var scope = await scopes.GetCurrentUserScopeAsync();
		return scope.IsAdmin || scope.HasTenantWideScope() ? query : StudentScopeAccessService.ApplyScope(query, scope);
	}

	public async Task<IQueryable<StudentEnrollment>> FilterHierarchyAsync(IQueryable<StudentEnrollment> query, Guid? boardId, Guid? mediumId)
	{
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
		if (!boardId.HasValue && !mediumId.HasValue) return query;
		var standardQuery = await standards.GetQueryableAsync();
		var stageQuery = await stages.GetQueryableAsync();
		var mediumQuery = await mediums.GetQueryableAsync();
		return query.Where(e => standardQuery.Any(s => s.TenantId == CurrentTenant.Id && s.Id == e.StandardId &&
			stageQuery.Any(stage => stage.TenantId == CurrentTenant.Id && stage.Id == s.EducationMediumStageId &&
				mediumQuery.Any(medium => medium.TenantId == CurrentTenant.Id && medium.Id == stage.EducationMediumId &&
					(!mediumId.HasValue || medium.Id == mediumId) && (!boardId.HasValue || medium.EducationBoardId == boardId)))));
	}

	public async Task EnsureTargetPlacementAsync(Guid yearId, Guid standardId, Guid divisionId)
	{
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
		var targetStandards = (await standards.GetQueryableAsync()).Where(s => s.Id == standardId && s.TenantId == CurrentTenant.Id &&
			s.EducationMediumStage.TenantId == CurrentTenant.Id && s.EducationMediumStage.EducationMedium.TenantId == CurrentTenant.Id &&
			s.EducationMediumStage.EducationMedium.EducationBoard.TenantId == CurrentTenant.Id &&
			s.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == yearId);
		var targetDivisions = (await divisions.GetQueryableAsync()).Where(d => d.Id == divisionId && d.TenantId == CurrentTenant.Id &&
			d.StandardId == standardId && targetStandards.Any(s => s.Id == d.StandardId));
		// Ancestors of a narrow class assignment do not grant access to other classes in that board.
		var authorized = await studentScope.ApplyCurrentUserDivisionScopeAsync(targetDivisions, yearId);
		if (!await AsyncExecuter.AnyAsync(authorized)) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	public async Task<IQueryable<StudentDirectoryEnrollmentProjection>> GetDirectoryProjectionAsync()
	{
		var query = await GetQueryableAsync();
		var standardQuery = await standards.GetQueryableAsync();
		var stageQuery = await stages.GetQueryableAsync();
		var mediumQuery = await mediums.GetQueryableAsync();
		return from e in query
			join s in standardQuery.Where(s => s.TenantId == CurrentTenant.Id) on e.StandardId equals (Guid?)s.Id
			join stage in stageQuery.Where(s => s.TenantId == CurrentTenant.Id) on s.EducationMediumStageId equals stage.Id
			join medium in mediumQuery.Where(m => m.TenantId == CurrentTenant.Id) on stage.EducationMediumId equals medium.Id
			select new StudentDirectoryEnrollmentProjection { Enrollment = e, StandardName = s.Name, MediumId = medium.Id, MediumName = medium.Name };
	}
}

public class StudentDirectoryEnrollmentProjection
{
	public StudentEnrollment Enrollment { get; set; } = null!;
	public string StandardName { get; set; } = "";
	public Guid MediumId { get; set; }
	public string MediumName { get; set; } = "";
}
