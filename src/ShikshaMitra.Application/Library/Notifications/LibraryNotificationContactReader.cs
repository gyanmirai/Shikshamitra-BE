using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationContactReader(IRepository<Student, Guid> students, IRepository<Employee, Guid> employees,
	ICurrentTenant tenant, IAsyncQueryableExecuter queries) : ITransientDependency
{
	public async Task<LibraryNotificationStudentFacts?> StudentAsync(Guid id) => await queries.FirstOrDefaultAsync(
		(await students.GetQueryableAsync()).Where(student => student.TenantId == tenant.Id && student.Id == id)
		.Select(student => new LibraryNotificationStudentFacts(student.Id, student.UserId, student.FirstName,
			student.MiddleName, student.LastName, student.Status,
			student.AdditionalDetail != null && student.AdditionalDetail.IsLibraryMember == true,
			student.ContactDetail == null ? null : student.ContactDetail.Email,
			student.ContactDetail == null ? null : student.ContactDetail.PrimaryContactNo, student.ExtraProperties,
			student.FamilyDetail == null || student.FamilyDetail.FatherDetails == null ? null :
				new LibraryNotificationContactFacts(student.FamilyDetail.FatherDetails.FullName, student.FamilyDetail.FatherDetails.Email, student.FamilyDetail.FatherDetails.ContactNo),
			student.FamilyDetail == null || student.FamilyDetail.MotherDetails == null ? null :
				new LibraryNotificationContactFacts(student.FamilyDetail.MotherDetails.FullName, student.FamilyDetail.MotherDetails.Email, student.FamilyDetail.MotherDetails.ContactNo),
			student.FamilyDetail == null || student.FamilyDetail.GuardianDetails == null ? null :
				new LibraryNotificationContactFacts(student.FamilyDetail.GuardianDetails.FullName, student.FamilyDetail.GuardianDetails.Email, student.FamilyDetail.GuardianDetails.ContactNo))));

	public async Task<LibraryNotificationEmployeeFacts?> EmployeeAsync(Guid id) => await queries.FirstOrDefaultAsync(
		(await employees.GetQueryableAsync()).Where(employee => employee.TenantId == tenant.Id && employee.Id == id)
		.Select(employee => new LibraryNotificationEmployeeFacts(employee.Id, employee.UserId, employee.FirstName,
			employee.LastName, employee.Email, employee.MobileNo, employee.AdditionalDetails != null && employee.AdditionalDetails.LibraryMember == true)));
}
