using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

/// <summary>
/// Abstract base for all student-registration section app services.
/// Centralises the shared dependencies, lifecycle-status constants, and the
/// query search-filter helper so each section service stays lean.
/// </summary>
[RemoteService(false)]
public abstract class StudentRegistrationBaseAppService(
	IStudentRepository studentRepository,
	EntityValidationService validationService,
	StudentScopeAccessService studentScopeAccessService) : ApplicationService
{
	/// <summary>
	/// Statuses that permit all registration-form section reads and writes.
	/// </summary>
	protected static readonly StudentLifeCycleStatusEnum[] RegistrationStatuses =
	[
		StudentLifeCycleStatusEnum.RegistrationFormIssued,
		StudentLifeCycleStatusEnum.Registration,
		StudentLifeCycleStatusEnum.EntranceExam,
		StudentLifeCycleStatusEnum.Admission
	];

	// These section DTOs also back Student Records. A completed student must
	// remain readable there without reopening registration writes.
	protected static readonly StudentLifeCycleStatusEnum[] ProfileReadStatuses =
	[
		..RegistrationStatuses,
		StudentLifeCycleStatusEnum.Enrolled,
		StudentLifeCycleStatusEnum.InActive,
		StudentLifeCycleStatusEnum.Left
	];
	protected static readonly StudentLifeCycleStatusEnum[] ProfileWriteStatuses =
	[
		..RegistrationStatuses,
		StudentLifeCycleStatusEnum.Enrolled,
		StudentLifeCycleStatusEnum.InActive,
		StudentLifeCycleStatusEnum.Left
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

	protected async Task<Student> GetScopedRegistrationStudentAsync(
		Guid studentId,
		string operation,
		string invalidStatusErrorCode,
		bool includeStudentRecords = false,
		bool forProfileWrite = false)
	{
		var student = await studentRepository.GetRegistrationAggregateAsync(studentId)
			?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Student), studentId);
		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, operation);
		if (Array.Exists(RegistrationStatuses, status => status == student.Status))
			await AuthorizationService.CheckAsync(forProfileWrite
				? ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Add
				: ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View);

		validationService.ValidateStudentStatus(
			student,
			forProfileWrite ? ProfileWriteStatuses :
				includeStudentRecords ? ProfileReadStatuses : RegistrationStatuses,
			invalidStatusErrorCode);
		if (!forProfileWrite && student.Status is StudentLifeCycleStatusEnum.Enrolled or StudentLifeCycleStatusEnum.InActive or StudentLifeCycleStatusEnum.Left)
			await LazyServiceProvider.LazyGetRequiredService<StudentRecordReadGuard>()
				.EnsureAsync(studentId, StudentRecordReadGuard.Section(operation));
		if (forProfileWrite && student.Status is StudentLifeCycleStatusEnum.Enrolled or StudentLifeCycleStatusEnum.InActive or StudentLifeCycleStatusEnum.Left)
		{
			var recordsGuard = LazyServiceProvider.LazyGetRequiredService<StudentRecordWriteGuard>();
			if (!recordsGuard.IsApplying(studentId, operation))
				await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add);
			await recordsGuard.EnsureAsync(studentId, operation);
		}

		return student;
	}
}
