using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments.Settings;
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
		IRepository<InquiryPreference, Guid> inquiryPreferenceRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<IQueryable<Student>> ApplyCurrentUserScopeAsync(IQueryable<Student> query)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		return ApplyScope(query, scope);
	}

	public static IQueryable<Student> ApplyScope(
		IQueryable<Student> query,
		UserScopeContext scope)
	{

		if (scope.HasTenantWideScope())
		{
			return query;
		}

		if (!scope.HasAnyAccess)
		{
			return query.Where(_ => false);
		}

		return query.Where(s =>
			s.AcademicYearId.HasValue &&
			scope.AcademicYearIds.Contains(s.AcademicYearId.Value) &&
			(scope.WildcardBoardAcademicYearIds.Contains(s.AcademicYearId.Value) ||
			 (s.AcademicBoardId.HasValue && scope.WildcardMediumBoardIds.Contains(s.AcademicBoardId.Value)) ||
			 scope.WildcardMediumStageMediumIds.Contains(s.AcademicMediumId) ||
			 (s.AcademicMediumStageId.HasValue &&
			  scope.WildcardStandardMediumStageIds.Contains(s.AcademicMediumStageId.Value)) ||
			 (s.StandardId.HasValue && scope.WildcardDivisionStandardIds.Contains(s.StandardId.Value)) ||
			 (s.DivisionId.HasValue && scope.DivisionIds.Contains(s.DivisionId.Value))));
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
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		if (!StudentRecordScope.HasAccess(student, scope))
		{
			Logger.LogInformation("UserScope denied Student access. UserId: {UserId}, TenantId: {TenantId}, StudentId: {StudentId}, Operation: {Operation}",
				scope.UserId, scope.TenantId, student.Id, operation);
			throw new BusinessException(UserScopeErrorCodes.AccessDenied)
				.WithData("StudentId", student.Id).WithData("Operation", operation);
		}
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

	public async Task EnsureCanUseInquiryPreferenceAsync(
		Guid? inquiryPreferenceId,
		Guid academicYearId,
		string operation,
		Guid? existingInquiryPreferenceId = null)
	{
		if (!inquiryPreferenceId.HasValue)
		{
			return;
		}

		var inquiryPreference = await inquiryPreferenceRepository.GetAsync(inquiryPreferenceId.Value);
		if (inquiryPreference.AcademicYearId != academicYearId)
		{
			throw new BusinessException(UserScopeErrorCodes.AccessDenied)
				.WithData("InquiryPreferenceId", inquiryPreference.Id)
				.WithData("AcademicYearId", academicYearId)
				.WithData("Operation", operation);
		}

		if (!inquiryPreference.IsActive && inquiryPreference.Id != existingInquiryPreferenceId)
		{
			throw new BusinessException("InquiryPreference:Inactive")
				.WithData("InquiryPreferenceId", inquiryPreference.Id)
				.WithData("AcademicYearId", academicYearId)
				.WithData("Operation", operation);
		}

		await EnsureCanAccessAcademicYearAsync(inquiryPreference.AcademicYearId, operation);
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
		// Scope validation needs only hierarchy IDs; loading aggregate details expands the Academic Setup graph.
		var query = from division in await divisionRepository.GetQueryableAsync()
			join standard in await standardRepository.GetQueryableAsync() on division.StandardId equals standard.Id
			join stage in await educationMediumStageRepository.GetQueryableAsync() on standard.EducationMediumStageId equals stage.Id
			join medium in await educationMediumRepository.GetQueryableAsync() on stage.EducationMediumId equals medium.Id
			join board in await educationBoardRepository.GetQueryableAsync() on medium.EducationBoardId equals board.Id
			where division.Id == divisionId
			select new AcademicOrganizationScopeRow(board.AcademicYearId, board.Id, medium.Id, stage.Id, standard.Id, division.Id);
		return await AsyncExecuter.SingleOrDefaultAsync(query) ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Division), divisionId);
	}

	private sealed record AcademicOrganizationScopeRow(
		Guid? AcademicYearId,
		Guid? EducationBoardId,
		Guid? EducationMediumId,
		Guid? EducationMediumStageId,
		Guid? StandardId,
		Guid? DivisionId) : IUserScopedEntity;
}
