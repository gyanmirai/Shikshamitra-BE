using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Students;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationRecipientResolver(
	IRepository<Student, Guid> studentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<LibraryMember, Guid> libraryMemberRepository)
	: ITransientDependency
{
	public async Task<LibraryNotificationRecipient> ResolveMemberAsync(
		Guid memberId,
		int? memberRole = null,
		string? fallbackName = null,
		string? fallbackEmail = null)
	{
		if (memberRole is null or 0)
		{
			var student = await studentRepository.FindAsync(memberId);
			if (student != null)
			{
				var libraryMember = await libraryMemberRepository.FindAsync(x => x.StudentId == student.Id);
				return new LibraryNotificationRecipient(
					student.Id,
					LibraryNotificationRecipientRoles.Member,
					$"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
					student.ContactDetail?.Email,
					student.ContactDetail?.PrimaryContactNo,
					student.AdditionalDetail?.IsLibraryMember == true && libraryMember?.IsActive != false);
			}
		}

		if (memberRole is null or 1)
		{
			var employee = await employeeRepository.FindAsync(memberId);
			if (employee != null)
			{
				return new LibraryNotificationRecipient(
					employee.Id,
					LibraryNotificationRecipientRoles.Member,
					$"{employee.FirstName} {employee.LastName}".Trim(),
					employee.Email,
					employee.MobileNo,
					employee.AdditionalDetails?.LibraryMember == true);
			}
		}

		return new LibraryNotificationRecipient(
			memberId,
			LibraryNotificationRecipientRoles.Member,
			fallbackName,
			fallbackEmail,
			Phone: null,
			IsActiveLibraryMember: false);
	}

	public static LibraryNotificationRecipient BuildOperationalRecipient(string name) =>
		new(
			MemberId: null,
			RecipientRole: LibraryNotificationRecipientRoles.Librarian,
			RecipientName: name,
			Email: null,
			Phone: null,
			IsActiveLibraryMember: true);
}

public sealed record LibraryNotificationRecipient(
	Guid? MemberId,
	string RecipientRole,
	string? RecipientName,
	string? Email,
	string? Phone,
	bool IsActiveLibraryMember);
