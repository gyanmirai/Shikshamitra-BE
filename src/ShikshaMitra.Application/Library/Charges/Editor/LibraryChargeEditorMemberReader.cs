using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Charges.Editor;

[RemoteService(false)]
public class LibraryChargeEditorMemberReader(ILibraryAccessService access, LibraryStoredLoanScope storedLoans,
	IRepository<LibraryMember, Guid> memberships, IRepository<Student, Guid> students,
	IRepository<Employee, Guid> employees) : ApplicationService, ITransientDependency
{
	public async Task<PaginatedResponse<LibraryChargeEditorMemberDto>> SearchAsync(LibraryAcademicContext context,
		string? search, int page, int pageSize)
	{
		var (studentQuery, employeeQuery) = await CandidatesAsync(context);
		var term = Term(search);
		if (term.Length > 0)
		{
			var guid = Guid.TryParse(term, out var id);
			var aliases = (await memberships.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
				(row.MemberIdentifier.ToLower().Contains(term) || guid && row.Id == id)).Select(row => row.StudentId);
			studentQuery = studentQuery.Where(row => guid && row.Id == id || aliases.Contains(row.Id) || row.MemberName.ToLower().Contains(term));
			employeeQuery = employeeQuery.Where(row => guid && row.Id == id || row.MemberIdentifier != null && row.MemberIdentifier.ToLower().Contains(term) || row.MemberName.ToLower().Contains(term));
		}
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
		var studentCount = await AsyncExecuter.CountAsync(studentQuery);
		var total = studentCount + (long)await AsyncExecuter.CountAsync(employeeQuery);
		var skip = ((long)page - 1) * pageSize;
		List<LibraryChargeEditorMemberDto> found = [];
		if (skip < total)
		{
			if (skip < studentCount)
				found.AddRange(await AsyncExecuter.ToListAsync(studentQuery.OrderBy(row => row.MemberName).ThenBy(row => row.Id)
					.Skip((int)skip).Take((int)Math.Min(pageSize, studentCount - skip))));
			if (found.Count < pageSize)
				found.AddRange(await AsyncExecuter.ToListAsync(employeeQuery.OrderBy(row => row.MemberName).ThenBy(row => row.Id)
					.Skip((int)Math.Max(0, skip - studentCount)).Take(pageSize - found.Count)));
		}
		return new() { Items = found, TotalCount = total, CurrentPage = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(total / (double)pageSize) };
	}

	public async Task<LibraryChargeEditorMemberDto> ResolveAsync(LibraryAcademicContext context, string identifier)
	{
		var term = Term(identifier);
		if (term.Length == 0) throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		var (studentQuery, employeeQuery) = await CandidatesAsync(context);
		var guid = Guid.TryParse(term, out var id);
		var aliases = (await memberships.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			(row.MemberIdentifier.ToLower() == term || guid && row.Id == id)).Select(row => row.StudentId);
		var found = await AsyncExecuter.ToListAsync(studentQuery.Where(row => guid && row.Id == id || aliases.Contains(row.Id)).OrderBy(row => row.Id).Take(2));
		found.AddRange(await AsyncExecuter.ToListAsync(employeeQuery.Where(row => guid && row.Id == id || row.MemberIdentifier != null && row.MemberIdentifier.ToLower() == term).OrderBy(row => row.Id).Take(2)));
		if (found.Count == 0) throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		if (found.Count != 1) throw new BusinessException(LibraryErrorCodes.MemberIdentifierAmbiguous,
			"More than one borrower uses this identifier. Search and select the correct borrower.");
		return found.Single();
	}

	private async Task<(IQueryable<LibraryChargeEditorMemberDto> Students, IQueryable<LibraryChargeEditorMemberDto> Employees)> CandidatesAsync(LibraryAcademicContext context)
	{
		var aliases = (await memberships.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id);
		var currentStudents = (await access.GetStudentsAsync(context.UserScope)).Select(row => row.Id);
		var currentEmployees = (await access.GetEmployeesAsync(context.UserScope)).Select(row => row.Id);
		var loans = await storedLoans.GetReadableLoansAsync(context.UserScope);
		var studentQuery = (await students.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			(currentStudents.Contains(row.Id) && row.AdditionalDetail != null && row.AdditionalDetail.IsLibraryMember == true &&
			 aliases.Where(alias => alias.StudentId == row.Id).OrderByDescending(alias => alias.CreationTime).ThenBy(alias => alias.Id).Select(alias => (bool?)alias.IsActive).FirstOrDefault() != false ||
			 loans.Any(loan => loan.MemberRole == 0 && (loan.MemberId == row.Id || aliases.Any(alias => alias.Id == loan.MemberId && alias.StudentId == row.Id)))))
			.Select(row => new LibraryChargeEditorMemberDto
			{
				Id = row.Id, Role = 0,
				MemberName = ((row.FirstName ?? "") + (row.MiddleName == null || row.MiddleName == "" ? "" : " " + row.MiddleName) + (row.LastName == null || row.LastName == "" ? "" : " " + row.LastName)).Trim(),
				MemberIdentifier = aliases.Where(alias => alias.StudentId == row.Id).OrderByDescending(alias => alias.CreationTime).ThenBy(alias => alias.Id).Select(alias => alias.MemberIdentifier).FirstOrDefault(),
				HasActiveLibraryMembership = row.AdditionalDetail != null && row.AdditionalDetail.IsLibraryMember == true &&
					aliases.Where(alias => alias.StudentId == row.Id).OrderByDescending(alias => alias.CreationTime).ThenBy(alias => alias.Id).Select(alias => (bool?)alias.IsActive).FirstOrDefault() != false,
				CanAssignDirect = currentStudents.Contains(row.Id) && row.AdditionalDetail != null && row.AdditionalDetail.IsLibraryMember == true &&
					aliases.Where(alias => alias.StudentId == row.Id).OrderByDescending(alias => alias.CreationTime).ThenBy(alias => alias.Id).Select(alias => (bool?)alias.IsActive).FirstOrDefault() != false,
				RequiresLoanSource = !(currentStudents.Contains(row.Id) && row.AdditionalDetail != null && row.AdditionalDetail.IsLibraryMember == true &&
					aliases.Where(alias => alias.StudentId == row.Id).OrderByDescending(alias => alias.CreationTime).ThenBy(alias => alias.Id).Select(alias => (bool?)alias.IsActive).FirstOrDefault() != false)
			});
		var employeeQuery = (await employees.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			(currentEmployees.Contains(row.Id) && row.AdditionalDetails != null && row.AdditionalDetails.LibraryMember || loans.Any(loan => loan.MemberRole == 1 && loan.MemberId == row.Id)))
			.Select(row => new LibraryChargeEditorMemberDto
			{
				Id = row.Id, Role = 1, MemberIdentifier = row.EmployeeCode,
				MemberName = ((row.FirstName ?? "") + (row.MiddleName == null || row.MiddleName == "" ? "" : " " + row.MiddleName) + (row.LastName == null || row.LastName == "" ? "" : " " + row.LastName)).Trim(),
				HasActiveLibraryMembership = row.AdditionalDetails != null && row.AdditionalDetails.LibraryMember,
				CanAssignDirect = currentEmployees.Contains(row.Id) && row.AdditionalDetails != null && row.AdditionalDetails.LibraryMember,
				RequiresLoanSource = !(currentEmployees.Contains(row.Id) && row.AdditionalDetails != null && row.AdditionalDetails.LibraryMember)
			});
		return (studentQuery, employeeQuery);
	}

	private static string Term(string? value)
	{
		var term = (value ?? string.Empty).Trim().ToLowerInvariant();
		if (term.Length > 100) throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		return term;
	}
}
