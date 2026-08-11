using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records.Profiles;

/// <summary>
/// Shared access and query helpers for student-record profile sections.
/// </summary>
[RemoteService(false)]
public abstract class StudentProfileBaseAppService(
	IStudentRepository studentRepository,
	StudentScopeAccessService studentScopeAccessService) : ApplicationService
{
	/// <summary>
	/// Applies an optional free-text search across student name, form number,
	/// inquiry number, primary contact, and e-mail fields.
	/// </summary>
	protected static IQueryable<Student> ApplySearchFilter(IQueryable<Student> query, string? searchTerm)
	{
		if (string.IsNullOrWhiteSpace(searchTerm))
		{
			return query;
		}

		var normalizedSearchTerm = searchTerm.Trim().ToLower();
		return query.Where(x =>
			(x.FirstName != null && x.FirstName.ToLower().Contains(normalizedSearchTerm)) ||
			(x.LastName != null && x.LastName.ToLower().Contains(normalizedSearchTerm)) ||
			(x.AdditionalDetail != null && x.AdditionalDetail.RegistrationFormNo != null &&
			 x.AdditionalDetail.RegistrationFormNo.ToLower().Contains(normalizedSearchTerm)) ||
			(x.AdditionalDetail != null && x.AdditionalDetail.InquiryNo != null &&
			 x.AdditionalDetail.InquiryNo.ToLower().Contains(normalizedSearchTerm)) ||
			(x.ContactDetail != null && x.ContactDetail.PrimaryContactNo != null &&
			 x.ContactDetail.PrimaryContactNo.Contains(normalizedSearchTerm)) ||
			(x.ContactDetail != null && x.ContactDetail.Email != null &&
			 x.ContactDetail.Email.ToLower().Contains(normalizedSearchTerm)));
	}

	protected async Task<Student> GetScopedStudentAsync(Guid studentId, string operation)
	{
		var student = await studentRepository.GetRegistrationAggregateAsync(studentId)
			?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Student), studentId);
		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, operation);
		return student;
	}
}
