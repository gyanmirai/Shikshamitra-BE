using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

/// <summary>
/// Abstract base for all student-registration section app services.
/// Centralises the shared dependencies, lifecycle-status constants, and the
/// query search-filter helper so each section service stays lean.
/// </summary>
[RemoteService(false)]
public abstract class StudentRegistrationBaseAppService(
    IRepository<Student, Guid> studentRepository,
    EntityValidationService validationService) : ApplicationService
{
    /// <summary>
    /// Statuses that permit all registration-form section reads and writes.
    /// </summary>
    protected static readonly StudentLifeCycleStatusEnum[] RegistrationStatuses =
    [
        StudentLifeCycleStatusEnum.RegistrationFormIssued,
        StudentLifeCycleStatusEnum.Registration,
		StudentLifeCycleStatusEnum.EntranceExam
    ];

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
}
