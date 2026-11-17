using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	public async Task<PagedResultDto<StudentIdCardListItemDto>> GetIssuedHistoryAsync(StudentIdCardHistoryQueryDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View);
		EnsureTenant();
		var sorting = ValidateHistoryQuery(input);
		var options = await LazyServiceProvider.LazyGetRequiredService<StudentDirectoryOptionsService>().GetAsync(input.AcademicYearId);
		if (!options.AcademicYears.Any(y => y.Id == input.AcademicYearId) ||
			!options.Boards.Any(b => b.Id == input.BoardId && b.ParentId == input.AcademicYearId) ||
			!options.Mediums.Any(m => m.Id == input.MediumId && m.ParentId == input.BoardId) ||
			input.StandardId.HasValue && !options.Standards.Any(s => s.Id == input.StandardId && s.ParentId == input.MediumId) ||
			input.DivisionId.HasValue && !options.Divisions.Any(d => d.Id == input.DivisionId && d.ParentId == input.StandardId))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		var roles = await access.GetDatabaseRoleNamesAsync();
		var currentPolicy = (await policies.ResolveAsync(input.AcademicYearId)).Policy;
		if (currentPolicy.IsMasked("Identity", roles)) return new(0, []);
		var scope = LazyServiceProvider.LazyGetRequiredService<StudentRecordsEnrollmentScopeService>();
		var students = LazyServiceProvider.LazyGetRequiredService<IStudentRepository>();
		var studentQuery = await scope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		studentQuery = await access.FilterFullAccessStudentsAsync(studentQuery, currentPolicy);
		studentQuery = studentQuery.Where(s => s.AcademicYearId == input.AcademicYearId && s.AcademicBoardId == input.BoardId &&
			s.AcademicMediumId == input.MediumId && (!input.StandardId.HasValue || s.StandardId == input.StandardId) &&
			(!input.DivisionId.HasValue || s.DivisionId == input.DivisionId));
		var cardQuery = (await cards.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id);
		var population = from card in cardQuery join student in studentQuery on card.StudentId equals student.Id select new { Card = card, Student = student };
		// Resolve each issued policy year once, before count/paging. Never hydrate the frozen JSON or Student aggregates.
		var issuedYears = await AsyncExecuter.ToListAsync(population.Select(row => row.Card.AcademicYearId).Distinct());
		var visibleYears = new List<Guid>(); var visibleNullYear = false;
		foreach (var year in issuedYears)
		{
			var policy = year == input.AcademicYearId ? currentPolicy : (await policies.ResolveAsync(year)).Policy;
			if (policy.IsMasked("Identity", roles)) continue;
			if (year.HasValue) visibleYears.Add(year.Value); else visibleNullYear = true;
		}
		var allowedYears = visibleYears.ToArray();
		population = population.Where(row => row.Card.AcademicYearId.HasValue ? Enumerable.Contains(allowedYears, row.Card.AcademicYearId.Value) : visibleNullYear);
		var query = population.Select(row => new StudentIdCardListItemDto
		{
			Id = row.Card.Id, StudentId = row.Student.Id,
			StudentName = ((row.Student.FirstName ?? "") + (row.Student.MiddleName != null && row.Student.MiddleName != "" ? " " + row.Student.MiddleName : "") +
				(row.Student.LastName != null && row.Student.LastName != "" ? " " + row.Student.LastName : "")).Trim(),
			GrNumber = row.Student.GrNumber ?? (row.Student.GrNo.HasValue ? row.Student.GrNo.Value.ToString() : null),
			StandardName = row.Student.Standard != null ? row.Student.Standard.Name : null,
			DivisionName = row.Student.Division != null ? row.Student.Division.Name : null,
			CardNumber = row.Card.CardNumber, Status = row.Card.Status, IssuedAt = row.Card.IssuedAt
		});
		var search = input.Search?.Trim().ToLowerInvariant();
		if (!string.IsNullOrEmpty(search)) query = query.Where(row => row.StudentName.ToLower().Contains(search) ||
			row.GrNumber != null && row.GrNumber.ToLower().Contains(search) || row.CardNumber.ToLower().Contains(search));
		var total = await AsyncExecuter.CountAsync(query);
		var ordered = SortHistory(query, sorting);
		return new(total, await AsyncExecuter.ToListAsync(ordered.ThenBy(row => row.Id).Skip(input.SkipCount).Take(input.MaxResultCount)));
	}

	private static string ValidateHistoryQuery(StudentIdCardHistoryQueryDto input)
	{
		if (input.AcademicYearId == Guid.Empty || input.BoardId == Guid.Empty || input.MediumId == Guid.Empty ||
			input.StandardId == Guid.Empty || input.DivisionId == Guid.Empty || input.DivisionId.HasValue && !input.StandardId.HasValue ||
			input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.Search?.Trim().Length > 150 || input.Sorting?.Length > 100)
			throw new BusinessException(StudentErrorCodes.InvalidIdHistoryQuery);
		var parts = input.Sorting?.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
		var sorting = parts.Length == 0 ? "issuedat desc" : parts.Length == 1 ? parts[0] + " asc" : string.Join(" ", parts);
		if (parts.Length > 2 || sorting is not ("studentname asc" or "studentname desc" or "grnumber asc" or "grnumber desc" or
			"standardname asc" or "standardname desc" or "divisionname asc" or "divisionname desc" or "cardnumber asc" or "cardnumber desc" or
			"issuedat asc" or "issuedat desc" or "status asc" or "status desc"))
			throw new BusinessException(StudentErrorCodes.InvalidIdHistorySort);
		return sorting;
	}

	private static IOrderedQueryable<StudentIdCardListItemDto> SortHistory(IQueryable<StudentIdCardListItemDto> query, string sorting) => sorting switch
	{
		"studentname asc" => query.OrderBy(row => row.StudentName), "studentname desc" => query.OrderByDescending(row => row.StudentName),
		"grnumber asc" => query.OrderBy(row => row.GrNumber), "grnumber desc" => query.OrderByDescending(row => row.GrNumber),
		"standardname asc" => query.OrderBy(row => row.StandardName), "standardname desc" => query.OrderByDescending(row => row.StandardName),
		"divisionname asc" => query.OrderBy(row => row.DivisionName), "divisionname desc" => query.OrderByDescending(row => row.DivisionName),
		"cardnumber asc" => query.OrderBy(row => row.CardNumber), "cardnumber desc" => query.OrderByDescending(row => row.CardNumber),
		"issuedat asc" => query.OrderBy(row => row.IssuedAt), "issuedat desc" => query.OrderByDescending(row => row.IssuedAt),
		"status asc" => query.OrderBy(row => row.Status), "status desc" => query.OrderByDescending(row => row.Status),
		_ => throw new BusinessException(StudentErrorCodes.InvalidIdHistorySort)
	};
}
