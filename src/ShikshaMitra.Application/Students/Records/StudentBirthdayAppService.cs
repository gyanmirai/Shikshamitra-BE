using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using BirthdayPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.Records_Birthdays;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize(BirthdayPermissions.View)]
public class StudentBirthdayAppService(
	StudentDirectoryQueryService directory, StudentDirectoryOptionsService directoryOptions,
	StudentRecordsPolicyResolver policies, StudentRecordsAccessService access,
	AttendanceWorkspaceAccessService assignments) : ApplicationService, IStudentBirthdayAppService
{
	public async Task<StudentBirthdayPageDto> GetListAsync(StudentBirthdayQueryDto input)
	{
		await AuthorizationService.CheckAsync(BirthdayPermissions.View);
		var schoolDate = DateOnly.FromDateTime(AdmissionCycleWindowEvaluator.GetInstitutionDate(AdmissionEventUtcTime.Normalize(Clock.Now)));
		var (start, end) = StudentBirthdayOccurrences.ResolveRange(input, schoolDate);
		var search = input.Search?.Trim().ToLowerInvariant();
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || search?.Length > 150)
			throw new BusinessException(StudentRecordsErrorCodes.InvalidBirthdayQuery);

		var result = new StudentBirthdayPageDto { SchoolDate = schoolDate, StartDate = start, EndDate = end };
		var context = await GetContextAsync(input.AcademicYearId);
		if (context == null) return result;
		ValidateHierarchy(input, context.Options);
		if (!context.OfficeScope && context.Options.Divisions.Count == 0) return result;

		var population = await directory.GetPopulationAsync(new StudentDirectoryRequest
		{
			AcademicYearId = input.AcademicYearId,
			BoardId = input.BoardId,
			MediumId = input.MediumId,
			StandardId = input.StandardId,
			DivisionId = input.DivisionId,
			IncludeActiveStudentList = true,
			Population = "Active"
		});
		if (!context.OfficeScope)
		{
			var divisionIds = context.Options.Divisions.Select(division => division.Id).ToArray();
			population = population.Where(student => student.DivisionId.HasValue && Enumerable.Contains(divisionIds, student.DivisionId.Value)
				&& student.Division != null && student.Division.StandardId == student.StandardId);
		}

		var occurrences = StudentBirthdayOccurrences.Query(population, schoolDate, start, end, search);
		result.TotalCount = await AsyncExecuter.LongCountAsync(occurrences);
		if (input.SkipCount >= result.TotalCount) return result;

		var page = await AsyncExecuter.ToListAsync(occurrences
			.OrderBy(row => row.DateKey).ThenBy(row => row.StudentName).ThenBy(row => row.StudentId)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		var readableIds = await ReadableProfileIdsAsync(population, page.Select(row => row.StudentId).Distinct().ToArray(), context.Policy);
		result.Items = page.Select(row => new StudentBirthdayOccurrenceDto
		{
			StudentId = row.StudentId,
			StudentName = row.StudentName,
			StandardName = row.StandardName,
			DivisionName = row.DivisionName,
			OccurrenceDate = new DateOnly(row.DateKey / 10000, row.DateKey / 100 % 100, row.DateKey % 100),
			CanOpenProfile = readableIds.Contains(row.StudentId)
		}).ToList();
		return result;
	}

	public async Task<StudentDirectoryOptionsDto> GetOptionsAsync(Guid academicYearId)
	{
		await AuthorizationService.CheckAsync(BirthdayPermissions.View);
		var context = await GetContextAsync(academicYearId);
		return context?.Options ?? new StudentDirectoryOptionsDto();
	}

	private async Task<BirthdayContext?> GetContextAsync(Guid yearId)
	{
		await access.EnsureActiveActorAsync();
		if (yearId == Guid.Empty) throw new BusinessException(StudentRecordsErrorCodes.InvalidBirthdayQuery);

		var options = await directoryOptions.GetAsync(yearId);
		options.AcademicYears.RemoveAll(year => year.Id != yearId);
		if (options.AcademicYears.Count == 0) return null;
		var policy = (await policies.ResolveAsync(yearId)).Policy;
		if (policy.IsMasked("Identity", await access.GetDatabaseRoleNamesAsync())) throw new AbpAuthorizationException();
		var officeScope = await AuthorizationService.IsGrantedAsync(BirthdayPermissions.OfficeScopeView);

		if (!officeScope)
		{
			var assigned = await assignments.AssignedClassesAsync(yearId);
			options.Divisions.RemoveAll(division => !assigned.Any(item => item.DivisionId == division.Id && item.StandardId == division.ParentId));
			var classIds = options.Divisions.Select(division => division.ParentId).ToHashSet();
			options.Standards.RemoveAll(standard => !classIds.Contains(standard.Id));
			var stageIds = options.Standards.Select(standard => standard.StageId).ToHashSet();
			options.Stages.RemoveAll(stage => !stageIds.Contains(stage.Id));
			var mediumIds = options.Standards.Select(standard => standard.ParentId).ToHashSet();
			options.Mediums.RemoveAll(medium => !mediumIds.Contains(medium.Id));
			var boardIds = options.Mediums.Select(medium => medium.ParentId).ToHashSet();
			options.Boards.RemoveAll(board => !boardIds.Contains(board.Id));
		}

		return new BirthdayContext(options, policy, officeScope);
	}

	private static void ValidateHierarchy(StudentBirthdayQueryDto input, StudentDirectoryOptionsDto options)
	{
		var division = options.Divisions.Find(item => item.Id == input.DivisionId);
		var standard = options.Standards.Find(item => item.Id == (input.StandardId ?? division?.ParentId));
		var medium = options.Mediums.Find(item => item.Id == (input.MediumId ?? standard?.ParentId));
		var board = options.Boards.Find(item => item.Id == (input.BoardId ?? medium?.ParentId));
		if ((input.DivisionId.HasValue && division == null) || (input.StandardId.HasValue && standard == null) ||
			(input.MediumId.HasValue && medium == null) || (input.BoardId.HasValue && board == null) ||
			(division != null && division.ParentId != standard?.Id) ||
			(standard != null && standard.ParentId != medium?.Id) || (medium != null && medium.ParentId != board?.Id))
			throw new BusinessException(StudentRecordsErrorCodes.InvalidBirthdayQuery);
	}

	private async Task<HashSet<Guid>> ReadableProfileIdsAsync(
		IQueryable<Student> population, Guid[] pageIds, StudentRecordsPolicy policy)
	{
		if (pageIds.Length == 0 || !await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View))
			return [];

		var candidates = population.Where(student => Enumerable.Contains(pageIds, student.Id));
		var readable = await access.FilterReadableProfileStudentsAsync(candidates, policy);
		return (await AsyncExecuter.ToListAsync(readable.Select(student => student.Id))).ToHashSet();
	}

	private sealed record BirthdayContext(StudentDirectoryOptionsDto Options, StudentRecordsPolicy Policy, bool OfficeScope);
}
