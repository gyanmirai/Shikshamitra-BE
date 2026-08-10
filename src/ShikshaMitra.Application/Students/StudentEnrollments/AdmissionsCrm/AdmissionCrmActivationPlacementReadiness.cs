using System;
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

public class AdmissionCrmActivationPlacementReadiness(
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IStudentRepository studentRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<(bool Valid, string Detail, int Strength, int Occupancy)> ValidateAsync(
		AdmissionInquiryLead lead,
		PreviewAdmissionCrmActivationDto input,
		string operation)
	{
		await scopeAccess.EnsureCanAccessStandardAsync(input.StandardId, operation);
		await scopeAccess.EnsureCanAccessDivisionAsync(input.DivisionId, operation);
		if (lead.TargetStandardId.HasValue && lead.TargetStandardId.Value != input.StandardId)
			return (false, "The activation placement must match the applicant's target standard.", 0, 0);
		var board = await boardRepository.FindAsync(input.EducationBoardId);
		var medium = await mediumRepository.FindAsync(input.AcademicMediumId);
		var stage = await stageRepository.FindAsync(input.AcademicMediumStageId);
		var standard = await standardRepository.FindAsync(input.StandardId);
		var division = await divisionRepository.FindAsync(input.DivisionId);
		var valid = board?.AcademicYearId == lead.AcademicYearId && medium?.EducationBoardId == board?.Id &&
			stage?.EducationMediumId == medium?.Id && standard?.EducationMediumStageId == stage?.Id &&
			division?.StandardId == standard?.Id;
		if (!valid) return (false, "Board, medium, stage, class, and division are not a valid active hierarchy.", division?.Strength ?? 0, 0);
		var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var divisionStudents = studentQuery.Where(x => x.TenantId == CurrentTenant.Id && x.DivisionId == division!.Id &&
			x.AcademicYearId == lead.AcademicYearId);
		var occupancy = await AsyncExecuter.CountAsync(divisionStudents.Where(x => x.Status == StudentLifeCycleStatusEnum.Enrolled));
		if (occupancy >= division.Strength)
			return (false, $"Division capacity is full ({occupancy}/{division.Strength}).", division.Strength, occupancy);
		if (input.RollNumber.HasValue && await AsyncExecuter.AnyAsync(divisionStudents.Where(x => x.RollNo == input.RollNumber)))
			return (false, "Roll number is already assigned in this division.", division.Strength, occupancy);
		return (true, $"Placement is valid with {division.Strength - occupancy} seat(s) remaining.", division.Strength, occupancy);
	}
}
