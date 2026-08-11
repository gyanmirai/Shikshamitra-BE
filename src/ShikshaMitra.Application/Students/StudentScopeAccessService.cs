using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students;

[RemoteService(false)]
public class StudentScopeAccessService(
	IUserScopeAccessService userScopeAccessService,
	IStudentRepository studentRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<AdmissionCrmInquirySource, Guid> admissionCrmInquirySourceRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<IQueryable<Student>> ApplyCurrentUserScopeAsync(IQueryable<Student> query)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();

		if (scope.IsAdmin)
		{
			return query;
		}

		if (!scope.HasAnyAccess)
		{
			return query.Where(_ => false);
		}

		return query.Where(s =>
			scope.HasWildcardAcademicYear ||
			(s.AcademicYearId.HasValue &&
			 (scope.AcademicYearIds.Contains(s.AcademicYearId.Value) ||
			  scope.WildcardBoardAcademicYearIds.Contains(s.AcademicYearId.Value))) ||
			(s.DivisionId.HasValue &&
			 (scope.DivisionIds.Contains(s.DivisionId.Value) ||
			  (scope.HasWildcardDivision &&
			   s.StandardId.HasValue &&
			   scope.WildcardDivisionStandardIds.Contains(s.StandardId.Value)))) ||
			(s.StandardId.HasValue &&
			 (scope.StandardIds.Contains(s.StandardId.Value) ||
			  (scope.HasWildcardStandard &&
			   s.AcademicMediumStageId.HasValue &&
			   scope.WildcardStandardMediumStageIds.Contains(s.AcademicMediumStageId.Value)))) ||
			(s.AcademicMediumStageId.HasValue &&
			 (scope.MediumStageIds.Contains(s.AcademicMediumStageId.Value) ||
			  (scope.HasWildcardMediumStage &&
			   scope.WildcardMediumStageMediumIds.Contains(s.AcademicMediumId)))) ||
			scope.MediumIds.Contains(s.AcademicMediumId) ||
			(scope.HasWildcardMedium &&
			 s.AcademicBoardId.HasValue &&
			 scope.WildcardMediumBoardIds.Contains(s.AcademicBoardId.Value)) ||
			(s.AcademicBoardId.HasValue && scope.BoardIds.Contains(s.AcademicBoardId.Value)));
	}

	public async Task<IQueryable<AdmissionInquiryLead>> ApplyCurrentUserAdmissionLeadScopeAsync(
		IQueryable<AdmissionInquiryLead> query)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();

		if (scope.IsAdmin || scope.HasWildcardAcademicYear)
		{
			return query;
		}

		if (!scope.HasAnyAccess)
		{
			return query.Where(_ => false);
		}

		var academicYearWideIds = scope.WildcardBoardAcademicYearIds
			.Distinct()
			.ToList();
		var scopedAcademicYearIds = scope.AcademicYearIds
			.Concat(academicYearWideIds)
			.Distinct()
			.ToList();
		var accessibleStandardIds = await GetAccessibleAdmissionLeadStandardIdsAsync(scope);

		return query.Where(lead =>
			academicYearWideIds.Contains(lead.AcademicYearId) ||
			(scopedAcademicYearIds.Contains(lead.AcademicYearId) &&
			 lead.TargetStandardId.HasValue &&
			 accessibleStandardIds.Contains(lead.TargetStandardId.Value)));
	}

	public async Task EnsureCanAccessAdmissionLeadAsync(
		AdmissionInquiryLead lead,
		string operation)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (lead.TargetStandardId.HasValue)
		{
			var leadScope = await BuildStandardScopeAsync(lead.TargetStandardId.Value);
			if (leadScope.AcademicYearId != lead.AcademicYearId)
			{
				throw CreateAdmissionLeadAccessDeniedException(lead, operation);
			}

			if (scope.IsAdmin ||
			    scope.HasWildcardAcademicYear ||
			    scope.WildcardBoardAcademicYearIds.Contains(lead.AcademicYearId))
			{
				return;
			}

			var accessibleStandardIds = await GetAccessibleAdmissionLeadStandardIdsAsync(scope);
			if (accessibleStandardIds.Contains(lead.TargetStandardId.Value))
			{
				return;
			}
		}
		else
		{
			// A lead without a target standard cannot be safely attributed below
			// academic-year scope, so only academic-year-wide or tenant-wide users pass.
			if (scope.IsAdmin ||
			    scope.HasWildcardAcademicYear ||
			    scope.WildcardBoardAcademicYearIds.Contains(lead.AcademicYearId))
			{
				return;
			}
		}

		throw CreateAdmissionLeadAccessDeniedException(lead, operation);
	}

	public async Task<bool> CurrentUserHasAcademicYearWideScopeAsync(Guid academicYearId)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		return scope.IsAdmin ||
		       scope.HasWildcardAcademicYear ||
		       scope.WildcardBoardAcademicYearIds.Contains(academicYearId);
	}

	public async Task<IQueryable<AttendanceDay>> ApplyCurrentUserAttendanceDayScopeAsync(IQueryable<AttendanceDay> query)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();

		if (scope.IsAdmin)
		{
			return query;
		}

		if (!scope.HasAnyAccess)
		{
			return query.Where(_ => false);
		}

		var standards = await standardRepository.GetQueryableAsync();
		var mediumStages = await educationMediumStageRepository.GetQueryableAsync();
		var mediums = await educationMediumRepository.GetQueryableAsync();
		var boards = await educationBoardRepository.GetQueryableAsync();

		var scopedQuery =
			from day in query
			join standard in standards on day.StandardId equals standard.Id
			join stage in mediumStages on standard.EducationMediumStageId equals stage.Id
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where scope.HasWildcardAcademicYear ||
			      scope.WildcardBoardAcademicYearIds.Contains(day.AcademicYearId) ||
			      scope.DivisionIds.Contains(day.DivisionId) ||
			      (scope.HasWildcardDivision && scope.WildcardDivisionStandardIds.Contains(day.StandardId)) ||
			      scope.StandardIds.Contains(day.StandardId) ||
			      (scope.HasWildcardStandard && scope.WildcardStandardMediumStageIds.Contains(stage.Id)) ||
			      scope.MediumStageIds.Contains(stage.Id) ||
			      (scope.HasWildcardMediumStage && scope.WildcardMediumStageMediumIds.Contains(medium.Id)) ||
			      scope.MediumIds.Contains(medium.Id) ||
			      (scope.HasWildcardMedium && scope.WildcardMediumBoardIds.Contains(board.Id)) ||
			      scope.BoardIds.Contains(board.Id)
			select day;

		if (scope.HasWildcardAcademicYear)
		{
			return scopedQuery;
		}

		var academicYearIds = scope.AcademicYearIds
			.Concat(scope.WildcardBoardAcademicYearIds)
			.Distinct()
			.ToList();

		return academicYearIds.Count > 0
			? scopedQuery.Where(day => academicYearIds.Contains(day.AcademicYearId))
			: scopedQuery.Where(_ => false);
	}

	public async Task EnsureCanAccessStudentAsync(Student student, string operation)
	{
		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(student, operation: operation);
	}

	public async Task<Student> GetStudentAsync(Guid studentId, string operation, bool includeDetails = false)
	{
		Student? student;
		if (includeDetails)
		{
			student = await studentRepository.GetAsync(studentId, includeDetails: true);
		}
		else
		{
			var query = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
			student = await AsyncExecuter.FirstOrDefaultAsync(query.Where(s => s.Id == studentId));
		}

		if (student == null)
		{
			throw new EntityNotFoundException(typeof(Student), studentId);
		}

		await EnsureCanAccessStudentAsync(student, operation);
		return student;
	}

	public async Task EnsureCanAccessStudentsAsync(IEnumerable<Guid> studentIds, string operation)
	{
		foreach (var studentId in studentIds.Distinct())
		{
			await GetStudentAsync(studentId, operation);
		}
	}

	public async Task EnsureCanAccessLeaveAsync(StudentLeave leave, string operation)
	{
		if (leave.Student == null)
		{
			await GetStudentAsync(leave.StudentId, operation);
			return;
		}

		await EnsureCanAccessStudentAsync(leave.Student, operation);
	}

	public async Task EnsureCanAccessStandardAsync(Guid standardId, string operation)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var standardScope = await BuildStandardScopeAsync(standardId);
		userScopeAccessService.EnsureCanAccess(standardScope, scope, operation: operation);
	}

	public async Task EnsureCanAccessMediumAsync(Guid mediumId, string operation)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var mediumScope = await BuildMediumScopeAsync(mediumId);
		userScopeAccessService.EnsureCanAccess(mediumScope, scope, operation: operation);
	}

	public async Task EnsureCanAccessAcademicYearAsync(Guid academicYearId, string operation)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (CanAccessAcademicYear(scope, academicYearId))
		{
			return;
		}

		throw new BusinessException(UserScopeErrorCodes.AccessDenied)
			.WithData("AcademicYearId", academicYearId)
			.WithData("Operation", operation);
	}

	public async Task EnsureCanUseInquirySourceAsync(
		Guid? inquirySourceId,
		Guid academicYearId,
		AdmissionInquiryChannel? channel,
		string operation,
		Guid? existingInquirySourceId = null)
	{
		if (!inquirySourceId.HasValue)
		{
			return;
		}

		var source = await admissionCrmInquirySourceRepository.GetAsync(inquirySourceId.Value);
		if (source.TenantId != CurrentTenant.Id || source.AcademicYearId != academicYearId)
		{
			throw new BusinessException(UserScopeErrorCodes.AccessDenied)
				.WithData("InquirySourceId", source.Id)
				.WithData("AcademicYearId", academicYearId)
				.WithData("Operation", operation);
		}

		if (!source.IsActive && source.Id != existingInquirySourceId)
		{
			throw new BusinessException("AdmissionCrm:InquirySourceInactive")
				.WithData("InquirySourceId", source.Id);
		}

		if (channel.HasValue && !source.Supports(channel.Value))
		{
			throw new BusinessException("AdmissionCrm:InquirySourceChannelNotAllowed")
				.WithData("InquirySourceId", source.Id)
				.WithData("Channel", channel.Value);
		}

		await EnsureCanAccessAcademicYearAsync(source.AcademicYearId, operation);
	}

	public async Task<(bool HasAllAcademicYears, IReadOnlyCollection<Guid> AcademicYearIds)> GetCurrentUserAcademicYearScopeAsync()
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasWildcardAcademicYear)
		{
			return (true, Array.Empty<Guid>());
		}

		return (
			false,
			scope.AcademicYearIds
				.Concat(scope.WildcardBoardAcademicYearIds)
				.Distinct()
				.ToList());
	}

	public async Task EnsureCanAccessDivisionAsync(Guid divisionId, string operation)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var divisionScope = await BuildDivisionScopeAsync(divisionId);
		userScopeAccessService.EnsureCanAccess(divisionScope, scope, operation: operation);
	}

	public async Task<IQueryable<Division>> ApplyCurrentUserDivisionScopeAsync(
		IQueryable<Division> query,
		Guid academicYearId)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var standards = await standardRepository.GetQueryableAsync();
		var mediumStages = await educationMediumStageRepository.GetQueryableAsync();
		var mediums = await educationMediumRepository.GetQueryableAsync();
		var boards = await educationBoardRepository.GetQueryableAsync();

		var hierarchyQuery =
			from division in query
			join standard in standards on division.StandardId equals standard.Id
			join stage in mediumStages on standard.EducationMediumStageId equals stage.Id
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where board.AcademicYearId == academicYearId
			select new { Division = division, Standard = standard, Stage = stage, Medium = medium, Board = board };

		if (scope.IsAdmin ||
		    scope.HasWildcardAcademicYear ||
		    scope.WildcardBoardAcademicYearIds.Contains(academicYearId))
		{
			return hierarchyQuery.Select(x => x.Division);
		}

		if (!CanAccessAcademicYear(scope, academicYearId))
		{
			return query.Where(_ => false);
		}

		return hierarchyQuery
			.Where(x =>
				scope.DivisionIds.Contains(x.Division.Id) ||
				(scope.HasWildcardDivision &&
				 scope.WildcardDivisionStandardIds.Contains(x.Standard.Id)) ||
				(scope.HasWildcardStandard &&
				 scope.WildcardStandardMediumStageIds.Contains(x.Stage.Id)) ||
				(scope.HasWildcardMediumStage &&
				 scope.WildcardMediumStageMediumIds.Contains(x.Medium.Id)) ||
				(scope.HasWildcardMedium &&
				 scope.WildcardMediumBoardIds.Contains(x.Board.Id)))
			.Select(x => x.Division);
	}

	public async Task EnsureCanAccessAttendanceScopeAsync(
		Guid academicYearId,
		Guid standardId,
		Guid divisionId,
		string operation)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var divisionScope = await BuildDivisionScopeAsync(divisionId);
		var attendanceScope = divisionScope with { AcademicYearId = academicYearId, StandardId = standardId };
		userScopeAccessService.EnsureCanAccess(attendanceScope, scope, operation: operation);
	}

	public async Task EnsureStudentsMatchAttendanceScopeAsync(
		IEnumerable<Guid> studentIds,
		Guid academicYearId,
		Guid standardId,
		Guid divisionId,
		string operation)
	{
		var requestedStudentIds = studentIds.Distinct().ToList();
		var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var students = await AsyncExecuter.ToListAsync(
			studentQuery.Where(s => requestedStudentIds.Contains(s.Id)));

		foreach (var student in students)
		{
			await EnsureCanAccessStudentAsync(student, operation);
			if (student.AcademicYearId != academicYearId ||
			    student.StandardId != standardId ||
			    student.DivisionId != divisionId)
			{
				throw new Volo.Abp.BusinessException(UserScopeErrorCodes.AccessDenied)
					.WithData("StudentId", student.Id)
					.WithData("Operation", operation);
			}
		}
	}

	private static bool CanAccessAcademicYear(UserScopeContext scope, Guid academicYearId) =>
		scope.IsAdmin ||
		scope.HasWildcardAcademicYear ||
		scope.AcademicYearIds.Contains(academicYearId) ||
		scope.WildcardBoardAcademicYearIds.Contains(academicYearId);

	private async Task<AcademicOrganizationScopeRow> BuildStandardScopeAsync(Guid standardId)
	{
		var standard = await standardRepository.GetAsync(standardId);
		var stage = await educationMediumStageRepository.GetAsync(standard.EducationMediumStageId);
		var medium = await educationMediumRepository.GetAsync(stage.EducationMediumId);
		var board = await educationBoardRepository.GetAsync(medium.EducationBoardId);

		return new AcademicOrganizationScopeRow(
			board.AcademicYearId,
			board.Id,
			medium.Id,
			stage.Id,
			standard.Id,
			null);
	}

	private async Task<List<Guid>> GetAccessibleAdmissionLeadStandardIdsAsync(UserScopeContext scope)
	{
		var standards = await standardRepository.GetQueryableAsync();
		var mediumStages = await educationMediumStageRepository.GetQueryableAsync();
		var mediums = await educationMediumRepository.GetQueryableAsync();
		var boards = await educationBoardRepository.GetQueryableAsync();
		var scopedAcademicYearIds = scope.AcademicYearIds
			.Concat(scope.WildcardBoardAcademicYearIds)
			.Distinct()
			.ToList();

		var query =
			from standard in standards
			join stage in mediumStages on standard.EducationMediumStageId equals stage.Id
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where scopedAcademicYearIds.Contains(board.AcademicYearId) &&
			      (scope.WildcardBoardAcademicYearIds.Contains(board.AcademicYearId) ||
			       scope.WildcardMediumBoardIds.Contains(board.Id) ||
			       scope.WildcardMediumStageMediumIds.Contains(medium.Id) ||
			       scope.WildcardStandardMediumStageIds.Contains(stage.Id) ||
			       scope.WildcardDivisionStandardIds.Contains(standard.Id))
			select standard.Id;

		return await AsyncExecuter.ToListAsync(query.Distinct());
	}

	private static BusinessException CreateAdmissionLeadAccessDeniedException(
		AdmissionInquiryLead lead,
		string operation)
	{
		var exception = new BusinessException(UserScopeErrorCodes.AccessDenied)
			.WithData("LeadId", lead.Id)
			.WithData("AcademicYearId", lead.AcademicYearId)
			.WithData("Operation", operation);

		if (lead.TargetStandardId.HasValue)
		{
			exception.WithData("TargetStandardId", lead.TargetStandardId.Value);
		}

		return exception;
	}

	private async Task<AcademicOrganizationScopeRow> BuildMediumScopeAsync(Guid mediumId)
	{
		var medium = await educationMediumRepository.GetAsync(mediumId);
		var board = await educationBoardRepository.GetAsync(medium.EducationBoardId);

		return new AcademicOrganizationScopeRow(
			board.AcademicYearId,
			board.Id,
			medium.Id,
			null,
			null,
			null);
	}

	private async Task<AcademicOrganizationScopeRow> BuildDivisionScopeAsync(Guid divisionId)
	{
		var division = await divisionRepository.GetAsync(divisionId);
		var standard = await standardRepository.GetAsync(division.StandardId);
		var stage = await educationMediumStageRepository.GetAsync(standard.EducationMediumStageId);
		var medium = await educationMediumRepository.GetAsync(stage.EducationMediumId);
		var board = await educationBoardRepository.GetAsync(medium.EducationBoardId);

		return new AcademicOrganizationScopeRow(
			board.AcademicYearId,
			board.Id,
			medium.Id,
			stage.Id,
			standard.Id,
			division.Id);
	}

	private sealed record AcademicOrganizationScopeRow(
		Guid? AcademicYearId,
		Guid? EducationBoardId,
		Guid? EducationMediumId,
		Guid? EducationMediumStageId,
		Guid? StandardId,
		Guid? DivisionId) : IUserScopedEntity;
}
