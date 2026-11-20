using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Circulation;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Reservations;

[RemoteService(false)]
public class LibraryReservationMemberReader(ILibraryAccessService access, IRepository<LibraryMember, Guid> memberships,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions, LibraryReservationEligibilityReader eligibility)
	: ApplicationService, ITransientDependency
{
	public async Task<PaginatedResponse<LibraryReservationMemberDto>> SearchAsync(Guid yearId, string? search, int page, int pageSize)
	{
		var context = await access.GetContextAsync(yearId);
		var students = (await access.GetStudentsAsync(context.UserScope)).Where(row => row.AcademicYearId == yearId);
		var employees = await access.GetEmployeesAsync(context.UserScope);
		var term = (search ?? string.Empty).Trim().ToLowerInvariant();
		if (term.Length > 100) throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest);
		if (term.Length > 0)
		{
			var guid = Guid.TryParse(term, out var id);
			var linked = (await memberships.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
				(row.MemberIdentifier.ToLower().Contains(term) || guid && row.Id == id)).Select(row => row.StudentId);
			students = students.Where(row => guid && row.Id == id || linked.Contains(row.Id) ||
				row.FirstName != null && row.FirstName.ToLower().Contains(term) || row.MiddleName != null && row.MiddleName.ToLower().Contains(term) || row.LastName != null && row.LastName.ToLower().Contains(term));
			employees = employees.Where(row => guid && row.Id == id || row.EmployeeCode != null && row.EmployeeCode.ToLower().Contains(term) ||
				row.FirstName != null && row.FirstName.ToLower().Contains(term) || row.MiddleName != null && row.MiddleName.ToLower().Contains(term) || row.LastName != null && row.LastName.ToLower().Contains(term));
		}
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
		var studentCount = await AsyncExecuter.CountAsync(students);
		var count = studentCount + await AsyncExecuter.CountAsync(employees);
		var skip = (page - 1) * pageSize;
		var studentRows = skip < studentCount ? await AsyncExecuter.ToListAsync(students.OrderBy(row => row.FirstName).ThenBy(row => row.LastName).ThenBy(row => row.Id)
			.Skip(skip).Take(Math.Min(pageSize, studentCount - skip)).Select(LibraryCirculationBorrowerProjection.Student)) : new List<CirculationStudentRow>();
		var employeeRows = studentRows.Count < pageSize ? await AsyncExecuter.ToListAsync(employees.OrderBy(row => row.FirstName).ThenBy(row => row.LastName).ThenBy(row => row.Id)
			.Skip(Math.Max(0, skip - studentCount)).Take(pageSize - studentRows.Count).Select(LibraryCirculationBorrowerProjection.Employee)) : new List<CirculationEmployeeRow>();
		return new() { TotalCount = count, CurrentPage = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(count / (double)pageSize),
			Items = await MapAsync(studentRows, employeeRows) };
	}

	public async Task<LibraryReservationMemberDto> ResolveAsync(Guid yearId, string identifier)
	{
		var context = await access.GetContextAsync(yearId);
		var term = (identifier ?? string.Empty).Trim().ToLowerInvariant();
		if (term.Length is 0 or > 100) throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		var guid = Guid.TryParse(term, out var id);
		var linked = (await memberships.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			(row.MemberIdentifier.ToLower() == term || guid && row.Id == id)).Select(row => row.StudentId);
		var students = await AsyncExecuter.ToListAsync((await access.GetStudentsAsync(context.UserScope)).Where(row => row.AcademicYearId == yearId &&
			(guid && row.Id == id || linked.Contains(row.Id))).OrderBy(row => row.Id).Select(LibraryCirculationBorrowerProjection.Student).Take(2));
		var employees = await AsyncExecuter.ToListAsync((await access.GetEmployeesAsync(context.UserScope)).Where(row => guid && row.Id == id ||
			row.EmployeeCode != null && row.EmployeeCode.ToLower() == term).OrderBy(row => row.Id).Select(LibraryCirculationBorrowerProjection.Employee).Take(2));
		if (students.Count + employees.Count == 0) throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		if (students.Count + employees.Count != 1) throw new BusinessException(LibraryErrorCodes.MemberIdentifierAmbiguous, "More than one borrower uses this identifier. Search and select the correct borrower.");
		return (await MapAsync(students, employees)).Single();
	}

	public async Task<Dictionary<Guid, LibraryReservationMemberDto>> ReadLabelsAsync(IEnumerable<Guid> ids)
	{
		var wanted = ids.Distinct().ToArray();
		var aliases = await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(wanted, row.Id))
			.Select(row => new { row.Id, row.StudentId }));
		var people = wanted.Concat(aliases.Select(row => row.StudentId)).Distinct().ToArray();
		var students = await AsyncExecuter.ToListAsync((await access.GetStudentsAsync()).Where(row => Enumerable.Contains(people, row.Id)).Select(LibraryCirculationBorrowerProjection.Student));
		var employees = await AsyncExecuter.ToListAsync((await access.GetEmployeesAsync()).Where(row => Enumerable.Contains(wanted, row.Id)).Select(LibraryCirculationBorrowerProjection.Employee));
		var result = (await MapAsync(students, employees)).GroupBy(row => row.Id).Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single());
		foreach (var alias in aliases)
		{
			if (!result.TryGetValue(alias.StudentId, out var label)) continue;
			if (result.TryGetValue(alias.Id, out var existing) && existing.Id != label.Id) result.Remove(alias.Id);
			else result[alias.Id] = label;
		}
		return result;
	}

	private async Task<List<LibraryReservationMemberDto>> MapAsync(List<CirculationStudentRow> students, List<CirculationEmployeeRow> employees)
	{
		var ids = students.Select(row => row.Id).ToArray();
		var linked = await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, row.StudentId))
			.Select(row => new { row.Id, row.StudentId, row.MemberIdentifier, row.CreationTime }));
		var standardIds = students.Where(row => row.StandardId.HasValue).Select(row => row.StandardId!.Value).ToArray();
		var divisionIds = students.Where(row => row.DivisionId.HasValue).Select(row => row.DivisionId!.Value).ToArray();
		var standardLabels = (await AsyncExecuter.ToListAsync((await standards.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(standardIds, row.Id))
			.Select(row => new { row.Id, row.Name }))).ToDictionary(row => row.Id, row => row.Name);
		var divisionLabels = (await AsyncExecuter.ToListAsync((await divisions.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(divisionIds, row.Id))
			.Select(row => new { row.Id, row.Name }))).ToDictionary(row => row.Id, row => row.Name);
		var facts = await eligibility.ReadAsync(ids.Concat(employees.Select(row => row.Id)));
		var result = students.Select(row =>
		{
			var current = linked.Where(member => member.StudentId == row.Id).OrderByDescending(member => member.CreationTime).ThenBy(member => member.Id).ToList();
			var latest = current.FirstOrDefault();
			var card = latest == null || current.Where(member => member.CreationTime == latest.CreationTime).Select(member => member.MemberIdentifier).Distinct().Count() > 1 ? null : latest.MemberIdentifier;
			var standard = row.StandardId.HasValue ? standardLabels.GetValueOrDefault(row.StandardId.Value) : null;
			var division = row.DivisionId.HasValue ? divisionLabels.GetValueOrDefault(row.DivisionId.Value) : null;
			var person = facts.GetValueOrDefault(row.Id);
			return new LibraryReservationMemberDto { Id = row.Id, MemberName = Name(row.FirstName, row.MiddleName, row.LastName), MemberIdentifier = card,
				Role = 0, AcademicYearId = row.AcademicYearId, ClassDivision = standard != null && division != null ? $"{standard}-{division}" : standard ?? division,
				IsEligibleForReservation = person?.Eligibility == LibraryReservationEligibility.Eligible, IneligibilityReason = person?.Reason };
		}).ToList();
		result.AddRange(employees.Select(row => new LibraryReservationMemberDto { Id = row.Id, MemberName = Name(row.FirstName, row.MiddleName, row.LastName),
			MemberIdentifier = row.EmployeeCode, Role = 1, IsEligibleForReservation = facts.GetValueOrDefault(row.Id)?.Eligibility == LibraryReservationEligibility.Eligible,
			IneligibilityReason = facts.GetValueOrDefault(row.Id)?.Reason }));
		return result;
	}
	private static string? Name(params string?[] parts)
	{
		var value = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
		return value.Length == 0 ? null : value;
	}
}
