using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Admissions lookup, independent of Academic menu grants and teacher assignments.</summary>
[RemoteService(false)]
public class AdmissionAssessmentCatalogService(IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions, IRepository<Subject, Guid> subjects,
	IRepository<AssignedSubject, Guid> assignments, IUserScopeAccessService scopeAccess)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionAssessmentCatalogDto> GetAsync()
	{
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		var standardQuery = (await standards.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		var divisionQuery = await divisions.GetQueryableAsync();
		if (!scope.IsAdmin && !scope.HasTenantWideScope())
			standardQuery = standardQuery.Where(item =>
				scope.AcademicYearIds.Contains(item.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId) && (
					scope.WildcardBoardAcademicYearIds.Contains(item.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId) ||
					scope.WildcardMediumBoardIds.Contains(item.EducationMediumStage.EducationMedium.EducationBoardId) ||
					scope.WildcardMediumStageMediumIds.Contains(item.EducationMediumStage.EducationMediumId) ||
					scope.WildcardStandardMediumStageIds.Contains(item.EducationMediumStageId) ||
					scope.WildcardDivisionStandardIds.Contains(item.Id) || scope.StandardIds.Contains(item.Id) ||
					divisionQuery.Any(division => division.StandardId == item.Id && scope.DivisionIds.Contains(division.Id))));
		var rows = await AsyncExecuter.ToListAsync(standardQuery.OrderBy(item => item.Name).Select(item => new
		{
			item.Id, item.Name,
			BoardId = item.EducationMediumStage.EducationMedium.EducationBoardId,
			BoardType = item.EducationMediumStage.EducationMedium.EducationBoard.Type,
			MediumId = item.EducationMediumStage.EducationMediumId,
			MediumName = item.EducationMediumStage.EducationMedium.Name,
			AcademicYearId = item.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			AcademicYearName = item.EducationMediumStage.EducationMedium.EducationBoard.AcademicYear.Name
		}));
		var mediumIds = rows.Select(item => item.MediumId).Distinct().ToArray();
		var classIds = rows.Select(item => item.Id).ToArray();
		var subjectRows = await AsyncExecuter.ToListAsync((await subjects.GetQueryableAsync())
			.Where(item => item.TenantId == CurrentTenant.Id && item.Status == SubjectMasterStatus.Active && Enumerable.Contains(mediumIds, item.EducationMediumId))
			.OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
			.Select(item => new AdmissionAssessmentSubjectOptionDto { Id = item.Id, Name = item.DisplayName ?? item.Name, MediumId = item.EducationMediumId }));
		var subjectIds = subjectRows.Select(item => item.Id).ToArray();
		var suggestions = await AsyncExecuter.ToListAsync((await assignments.GetQueryableAsync())
			.Where(item => item.TenantId == CurrentTenant.Id && item.Status == SubjectStatus.Published && Enumerable.Contains(classIds, item.StandardId) && Enumerable.Contains(subjectIds, item.SubjectId))
			.Select(item => new { item.SubjectId, item.StandardId }).Distinct());
		var assignedClasses = suggestions.ToLookup(item => item.SubjectId, item => item.StandardId);
		foreach (var subject in subjectRows) subject.AssignedStandardIds = assignedClasses[subject.Id].ToList();
		return new AdmissionAssessmentCatalogDto
		{
			Classes = rows.Select(item => new AdmissionAssessmentClassOptionDto
			{
				Id = item.Id, Name = item.Name, BoardId = item.BoardId, BoardName = item.BoardType.ToString(),
				MediumId = item.MediumId, MediumName = item.MediumName, AcademicYearId = item.AcademicYearId, AcademicYearName = item.AcademicYearName
			}).ToList(),
			Subjects = subjectRows
		};
	}
}
