using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlacementOptionsWorkflow(
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IStudentRepository studentRepository,
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<IReadOnlyList<AdmissionCrmPlacementOptionDto>> GetAsync(Guid academicYearId)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, "GetPlacementOptionsAsync");
		var boardQuery = await boardRepository.GetQueryableAsync();
		var boards = await AsyncExecuter.ToListAsync(boardQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId).Select(x => new { x.Id, x.Type }));
		var mediumQuery = await mediumRepository.GetQueryableAsync();
		var mediums = await AsyncExecuter.ToListAsync(mediumQuery.Where(x => x.TenantId == CurrentTenant.Id)
			.Select(x => new { x.Id, x.EducationBoardId, x.Name }));
		var stageQuery = await stageRepository.GetQueryableAsync();
		var stages = await AsyncExecuter.ToListAsync(stageQuery.Where(x => x.TenantId == CurrentTenant.Id)
			.Select(x => new { x.Id, x.EducationMediumId, x.Name }));
		var standardQuery = await standardRepository.GetQueryableAsync();
		var standards = await AsyncExecuter.ToListAsync(standardQuery.Where(x => x.TenantId == CurrentTenant.Id)
			.Select(x => new { x.Id, x.EducationMediumStageId, x.Name }));
		var divisionQuery = await divisionRepository.GetQueryableAsync();
		divisionQuery = await scopeAccess.ApplyCurrentUserDivisionScopeAsync(
			divisionQuery.Where(x => x.TenantId == CurrentTenant.Id), academicYearId);
		var divisions = await AsyncExecuter.ToListAsync(divisionQuery.Select(x => new { x.Id, x.StandardId, x.Name, x.Strength }));
		var divisionIds = divisions.Select(x => x.Id).ToList();
		var students = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var activeCounts = await AsyncExecuter.ToListAsync(students.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.DivisionId.HasValue && divisionIds.Contains(x.DivisionId.Value) &&
			x.Status == StudentLifeCycleStatusEnum.Enrolled).GroupBy(x => x.DivisionId!.Value)
			.Select(group => new { DivisionId = group.Key, Count = group.Count() }));
		var activations = await activationRepository.GetQueryableAsync();
		var pendingCounts = await AsyncExecuter.ToListAsync(activations.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && divisionIds.Contains(x.DivisionId) &&
			x.Status != AdmissionCrmActivationStatus.Draft && x.Status != AdmissionCrmActivationStatus.FirstDayReady &&
			x.Status != AdmissionCrmActivationStatus.Cancelled).GroupBy(x => x.DivisionId)
			.Select(group => new { DivisionId = group.Key, Count = group.Count() }));
		var active = activeCounts.ToDictionary(x => x.DivisionId, x => x.Count);
		var pending = pendingCounts.ToDictionary(x => x.DivisionId, x => x.Count);
		return (from board in boards join medium in mediums on board.Id equals medium.EducationBoardId
			join stage in stages on medium.Id equals stage.EducationMediumId
			join standard in standards on stage.Id equals standard.EducationMediumStageId
			join division in divisions on standard.Id equals division.StandardId
			let activeCount = active.GetValueOrDefault(division.Id)
			let pendingCount = pending.GetValueOrDefault(division.Id)
			let configured = division.Strength > 0
			orderby board.Type, medium.Name, stage.Name, standard.Name, division.Name
			select new AdmissionCrmPlacementOptionDto
			{
				EducationBoardId = board.Id, BoardName = board.Type.ToString(), AcademicMediumId = medium.Id,
				MediumName = medium.Name, AcademicMediumStageId = stage.Id, StageName = stage.Name,
				StandardId = standard.Id, StandardName = standard.Name, DivisionId = division.Id,
				DivisionName = division.Name, DivisionStrength = division.Strength, CapacityConfigured = configured,
				ActiveStudentCount = activeCount, PendingActivationCount = pendingCount,
				RemainingCapacity = configured ? Math.Max(0, division.Strength - activeCount - pendingCount) : null
			}).ToList();
	}
}
