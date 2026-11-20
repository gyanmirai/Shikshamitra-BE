using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryCirculationReadService
{
	private async Task<List<IssueReturnItemDto>> MapCanonicalAsync(List<BookIssue> rows, ReadAsOfEvidence asOf)
	{
		if (rows.Count == 0) return [];
		var memberIds = rows.Where(loan => loan.MemberRole == 0).Select(loan => loan.MemberId).Distinct().ToArray();
		var aliases = await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(memberIds, row.Id)).Select(row => new { row.Id, row.StudentId }));
		var canonicalIds = aliases.ToDictionary(row => row.Id, row => row.StudentId);
		Guid PersonId(BookIssue loan) => loan.MemberRole == 0 ? canonicalIds.GetValueOrDefault(loan.MemberId, loan.MemberId) : loan.MemberId;
		var snapshots = rows.ToDictionary(loan => loan.Id, loan => LibraryLoanBorrowerSnapshot.Read(loan, PersonId(loan)));
		var fallbackIds = rows.Where(loan => snapshots[loan.Id] == null).Select(PersonId).Distinct().ToArray();
		var studentRows = await AsyncExecuter.ToListAsync((await access.GetStudentsAsync()).Where(row => Enumerable.Contains(fallbackIds, row.Id))
			.Select(row => new { row.Id, row.FirstName, row.MiddleName, row.LastName, row.StandardId, row.DivisionId, row.RollNo, row.GrNo }));
		var employeeRows = await AsyncExecuter.ToListAsync((await access.GetEmployeesAsync()).Where(row => Enumerable.Contains(fallbackIds, row.Id))
			.Select(row => new { row.Id, row.FirstName, row.MiddleName, row.LastName, row.EmployeeCode }));
		var students = studentRows.ToDictionary(row => row.Id);
		var employees = employeeRows.ToDictionary(row => row.Id);
		var studentIds = studentRows.Select(row => row.Id).ToArray();
		var cards = (await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(studentIds, row.StudentId))
			.Select(row => new { row.StudentId, row.MemberIdentifier, row.CreationTime })))
			.GroupBy(row => row.StudentId).ToDictionary(group => group.Key, group =>
			{
				var latest = group.Max(row => row.CreationTime);
				var values = group.Where(row => row.CreationTime == latest).Select(row => row.MemberIdentifier).Distinct().ToArray();
				return values.Length == 1 ? values[0] : null;
			});
		var standardIds = studentRows.Where(row => row.StandardId.HasValue).Select(row => row.StandardId!.Value).Distinct().ToArray();
		var divisionIds = studentRows.Where(row => row.DivisionId.HasValue).Select(row => row.DivisionId!.Value).Distinct().ToArray();
		var standardNames = (await AsyncExecuter.ToListAsync((await standards.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(standardIds, row.Id)).Select(row => new { row.Id, row.Name }))).ToDictionary(row => row.Id, row => row.Name);
		var divisionNames = (await AsyncExecuter.ToListAsync((await divisions.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(divisionIds, row.Id)).Select(row => new { row.Id, row.Name }))).ToDictionary(row => row.Id, row => row.Name);
		var copyIds = rows.Select(loan => loan.BookCopyId).Distinct().ToArray();
		var copyRows = await AsyncExecuter.ToListAsync((await copies.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(copyIds, row.Id))
			.Select(row => new { row.Id, row.BookId }));
		var copyBooks = copyRows.ToDictionary(row => row.Id, row => row.BookId);
		var bookIds = copyRows.Select(row => row.BookId).Distinct().ToArray();
		var bookRows = await AsyncExecuter.ToListAsync((await books.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(bookIds, row.Id))
			.Select(row => new { row.Id, row.Title, row.CategoryId }));
		var bookLookup = bookRows.ToDictionary(row => row.Id);
		var categoryIds = bookRows.Select(row => row.CategoryId).Distinct().ToArray();
		var categoryNames = (await AsyncExecuter.ToListAsync((await categories.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(categoryIds, row.Id)).Select(row => new { row.Id, row.Name }))).ToDictionary(row => row.Id, row => row.Name);
		return rows.Select(loan =>
		{
			var personId = PersonId(loan);
			var dto = new IssueReturnItemDto
			{
				TransactionId = loan.Id, MemberId = personId, Role = loan.MemberRole,
				CopyId = loan.BookCopyId, AccessionNo = loan.AccessionNumber
			};
			if (copyBooks.TryGetValue(loan.BookCopyId, out var bookId) && bookLookup.TryGetValue(bookId, out var book))
			{
				dto.BookId = book.Id; dto.BookTitle = book.Title; dto.BookCategory = categoryNames.GetValueOrDefault(book.CategoryId);
			}
			if (snapshots[loan.Id] is { } snapshot)
			{
				dto.MemberName = snapshot.MemberName; dto.MemberIdentifier = snapshot.MemberIdentifier;
				dto.ClassDivision = snapshot.ClassDivision; dto.MemberDisplaySource = snapshot.DisplaySource;
			}
			else if (loan.MemberRole == 0 && students.TryGetValue(personId, out var student))
			{
				dto.MemberName = Name(student.FirstName, student.MiddleName, student.LastName);
				dto.MemberIdentifier = cards.GetValueOrDefault(personId);
				dto.RollNo = student.RollNo?.ToString(); dto.GrNo = student.GrNo?.ToString();
				dto.StandardName = student.StandardId.HasValue ? standardNames.GetValueOrDefault(student.StandardId.Value) : null;
				dto.DivisionName = student.DivisionId.HasValue ? divisionNames.GetValueOrDefault(student.DivisionId.Value) : null;
				dto.ClassDivision = dto.StandardName != null && dto.DivisionName != null ? $"{dto.StandardName}-{dto.DivisionName}" : dto.StandardName ?? dto.DivisionName;
				dto.MemberDisplaySource = "CurrentAuthorizedRecord";
			}
			else if (loan.MemberRole == 1 && employees.TryGetValue(personId, out var employee))
			{
				dto.MemberName = Name(employee.FirstName, employee.MiddleName, employee.LastName);
				dto.MemberIdentifier = employee.EmployeeCode; dto.MemberDisplaySource = "CurrentAuthorizedRecord";
			}
			ApplyDates(dto, loan, asOf);
			return dto;
		}).ToList();
	}

	private static string? Name(params string?[] values)
	{
		var name = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
		return name.Length == 0 ? null : name;
	}
}
