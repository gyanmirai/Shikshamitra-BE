using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;

namespace ShikshaMitra.Library.Reports;

public partial class LibraryReportGenerator
{
	private async Task<(Dictionary<Guid, LibraryMemberReportInfo> Members, Dictionary<Guid, Guid> Aliases)> LoadCurrentMembersAsync(
		LibraryReportExecutionScope scope, IReadOnlyCollection<BookIssue> issues, IReadOnlyCollection<BookReservation> requests,
		IReadOnlyCollection<LibraryCharge> charges, Guid[] currentMemberIds, bool includeRosterContacts)
	{
		var recordedStudentChargeIds = charges.Where(row => row.MemberRole == 0).Select(row => row.MemberId).Distinct().ToArray();
		var sourceIds = issues.Select(row => row.MemberId).Concat(requests.Select(row => row.MemberId))
			.Concat(recordedStudentChargeIds).Distinct().ToArray();
		var schoolMemberships = (await membershipRepository.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var chargeStudentOwners = schoolMemberships.Where(row => Enumerable.Contains(recordedStudentChargeIds, row.Id))
			.Select(row => row.StudentId);
		var memberships = await asyncExecuter.ToListAsync(schoolMemberships.Where(row =>
			Enumerable.Contains(sourceIds, row.Id) || Enumerable.Contains(currentMemberIds, row.StudentId) ||
			Enumerable.Contains(recordedStudentChargeIds, row.StudentId) || chargeStudentOwners.Contains(row.StudentId))
			.Select(row => new { row.Id, row.StudentId, row.MemberIdentifier, row.CreationTime }));
		var aliases = memberships.ToDictionary(row => row.Id, row => row.StudentId);
		var wanted = issues.Select(row => row.MemberRole == 0 ? aliases.GetValueOrDefault(row.MemberId, row.MemberId) : row.MemberId)
			.Concat(requests.Select(row => row.CanonicalMemberId ?? aliases.GetValueOrDefault(row.MemberId, row.MemberId)))
			.Concat(charges.Select(row => row.MemberRole == 0 ? aliases.GetValueOrDefault(row.MemberId, row.MemberId) : row.MemberId)).Distinct().ToArray();
		var students = await asyncExecuter.ToListAsync((await libraryAccess.GetStudentsAsync(scope.UserScope))
			.Where(row => includeRosterContacts || Enumerable.Contains(wanted, row.Id)).Select(row => new
			{
				row.Id, row.FirstName, row.MiddleName, row.LastName, row.GrNo, row.StandardId, row.DivisionId, row.CreationTime,
				Email = includeRosterContacts && row.ContactDetail != null ? row.ContactDetail.Email : null,
				Phone = includeRosterContacts && row.ContactDetail != null ? row.ContactDetail.PrimaryContactNo : null,
				IsCurrent = row.AdditionalDetail != null && row.AdditionalDetail.IsLibraryMember == true
			}));
		var employees = await asyncExecuter.ToListAsync((await libraryAccess.GetEmployeesAsync(scope.UserScope))
			.Where(row => includeRosterContacts || Enumerable.Contains(wanted, row.Id)).Select(row => new
			{
				row.Id, row.FirstName, row.MiddleName, row.LastName, row.EmployeeCode, row.EmployeeGroup, row.Department, row.CreationTime,
				Email = includeRosterContacts ? row.Email : null, Phone = includeRosterContacts ? row.MobileNo : null,
				IsCurrent = row.AdditionalDetails != null && row.AdditionalDetails.LibraryMember == true
			}));
		var standardIds = students.Where(row => row.StandardId.HasValue).Select(row => row.StandardId!.Value).Distinct().ToArray();
		var divisionIds = students.Where(row => row.DivisionId.HasValue).Select(row => row.DivisionId!.Value).Distinct().ToArray();
		var standards = (await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id &&
			Enumerable.Contains(standardIds, row.Id)).Select(row => new { row.Id, row.Name }))).ToDictionary(row => row.Id, row => row.Name);
		var divisions = (await asyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id &&
			Enumerable.Contains(divisionIds, row.Id)).Select(row => new { row.Id, row.Name }))).ToDictionary(row => row.Id, row => row.Name);
		var members = new Dictionary<Guid, LibraryMemberReportInfo>();
		foreach (var student in students)
		{
			var cards = memberships.Where(row => row.StudentId == student.Id).OrderByDescending(row => row.CreationTime).ToList();
			var latest = cards.FirstOrDefault();
			string[] identifiers = latest == null ? [] : cards.Where(row => row.CreationTime == latest.CreationTime).Select(row => row.MemberIdentifier).Distinct().ToArray();
			var identifier = identifiers.Length == 1 ? identifiers[0] : student.GrNo?.ToString(CultureInfo.InvariantCulture);
			members[student.Id] = new(student.Id, 0, "Student", Name(student.FirstName, student.MiddleName, student.LastName), identifier,
				student.Email, student.Phone, string.Join("-", new[] {
					student.StandardId.HasValue ? standards.GetValueOrDefault(student.StandardId.Value) : null,
					student.DivisionId.HasValue ? divisions.GetValueOrDefault(student.DivisionId.Value) : null
				}.Where(value => !string.IsNullOrWhiteSpace(value))), student.CreationTime, student.IsCurrent);
		}
		foreach (var employee in employees)
			members[employee.Id] = new(employee.Id, 1, employee.EmployeeGroup == EmployeeGroupType.Teaching ? "Teacher" : "Staff",
				Name(employee.FirstName, employee.MiddleName, employee.LastName), employee.EmployeeCode, employee.Email, employee.Phone,
				employee.Department, employee.CreationTime, employee.IsCurrent);
		foreach (var membership in memberships)
			if (members.TryGetValue(membership.StudentId, out var student) && student.RoleValue == 0) members.TryAdd(membership.Id, student);
		return (members, aliases);
	}

	private static string Name(params string?[] parts) => string.Join(" ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
}
