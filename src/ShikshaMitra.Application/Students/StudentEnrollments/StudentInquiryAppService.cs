using ShikshaMitra.Students.Enrollments.Inquiries;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Students.StudentEnrollments;

[RemoteService(false)]
public class StudentInquiryAppService(
	IStudentRepository studentRepository,
	IRepository<Standard, Guid> standardRepository,
	IGuidGenerator guidGenerator,
	StudentEnrollmentManager studentEnrollmentManager,
	EntityValidationService validationService,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, IStudentInquiryAppService
{
	public async Task CreateStudentInquiryAsync(
		CreateUpdateInquiryDto createStudentInquiryDto
	)
	{
		await validationService.ValidateAcademicMediumExistsAsync(createStudentInquiryDto.AcademicMediumId);
		await validationService.ValidateStandardExistsAsync(createStudentInquiryDto.StandardId);

		var standard = await standardRepository.GetAsync(createStudentInquiryDto.StandardId, includeDetails: true) ?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard not found");

		if (standard.EducationMediumStage.EducationMedium.Id != createStudentInquiryDto.AcademicMediumId)
		{
			throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard does not belong to the specified Academic Medium");
		}
		await studentScopeAccessService.EnsureCanAccessStandardAsync(
			createStudentInquiryDto.StandardId,
			nameof(CreateStudentInquiryAsync));
		await studentScopeAccessService.EnsureCanUseInquiryPreferenceAsync(
			createStudentInquiryDto.InquiryPreferenceId,
			standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			nameof(CreateStudentInquiryAsync));

		// Generate inquiry number before creating entities
		var inquiryNo = await studentEnrollmentManager.GenerateInquiryNumberAsync(
			createStudentInquiryDto.AcademicMediumId
		);

		// Create the complete aggregate using factory method
		// This ensures all parts are created together in a valid state
		var student = Student.CreateInquiry(
			guidGenerator.Create(),
			CurrentTenant.Id,
			createStudentInquiryDto.FirstName,
			createStudentInquiryDto.MiddleName,
			createStudentInquiryDto.LastName,
			standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			standard.EducationMediumStage.EducationMedium.EducationBoardId,
			createStudentInquiryDto.AcademicMediumId,
			standard.EducationMediumStageId,
			createStudentInquiryDto.StandardId,
			createStudentInquiryDto.DateOfBirth,
			createStudentInquiryDto.Gender,
			createStudentInquiryDto.DateOfInquiry,
			inquiryNo,
			createStudentInquiryDto.InquiryPreferenceId,
			createStudentInquiryDto.PrimaryContactNo,
			createStudentInquiryDto.SecondaryContactNo,
			createStudentInquiryDto.Email,
			createStudentInquiryDto.Address1,
			createStudentInquiryDto.Address2,
			createStudentInquiryDto.Address3,
			createStudentInquiryDto.City,
			createStudentInquiryDto.State,
			createStudentInquiryDto.Country,
			createStudentInquiryDto.PinCode,
			createStudentInquiryDto.PreviousSchool
		);

		// Single repository call - EF Core will handle all child entities
		// due to cascade configuration
		await studentRepository.InsertAsync(student, autoSave: true);
	}

	public async Task<StudentDto> GetInquiryByIdAsync(Guid studentId)
	{
		var lightweightQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var student = await AsyncExecuter.FirstOrDefaultAsync(lightweightQuery.Where(s =>
			s.Id == studentId &&
			s.TenantId == CurrentTenant.Id));

		if (student == null)
		{
			throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Student), studentId);
		}

		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, nameof(GetInquiryByIdAsync));

		// ABP data filters enforce tenant isolation; validate the lifecycle state
		validationService.ValidateStudentStatus(
			student,
			[StudentLifeCycleStatusEnum.Inquiry],
			StudentRegistrationErrorCodes.StudentNotInInquiryStatus
		);

		var dto = await AsyncExecuter.FirstOrDefaultAsync(
			ProjectToStudentDto(lightweightQuery.Where(s => s.Id == studentId)));
		PopulateComputedInquiryFields(dto);

		return dto ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Student), studentId);
	}

	public async Task<PagedResultDto<StudentDto>> GetInquiriesByMediumAsync(
		Guid academicMediumId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null
	)
	{
		await validationService.ValidateAcademicMediumExistsAsync(academicMediumId);

		var query = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
			.Where(s => s.Status == StudentLifeCycleStatusEnum.Inquiry
				&& s.AcademicMediumId == academicMediumId
				&& s.TenantId == CurrentTenant.Id);
		query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);

		// Implement search functionality
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var normalizedSearchTerm = searchTerm.Trim().ToLower();
			query = query.Where(x =>
				(x.FirstName != null && x.FirstName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.AdditionalDetail != null && x.AdditionalDetail.InquiryNo != null && x.AdditionalDetail.InquiryNo.ToLower().Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.PrimaryContactNo != null && x.ContactDetail.PrimaryContactNo.Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.Email != null && x.ContactDetail.Email.ToLower().Contains(normalizedSearchTerm))
			);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);

		if (totalCount == 0)
		{
			return new PagedResultDto<StudentDto>(0, new List<StudentDto>());
		}

		var resultList = await AsyncExecuter.ToListAsync(ProjectToStudentDto(query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skipCount)
			.Take(maxResultCount)));
		foreach (var item in resultList)
		{
			PopulateComputedInquiryFields(item);
		}

		return new PagedResultDto<StudentDto>(totalCount, resultList);
	}

	public async Task<StudentDto> UpdateInquiryAsync(
		Guid studentId,
		CreateUpdateInquiryDto updateInquiryDto
	)
	{
		await validationService.ValidateAcademicMediumExistsAsync(updateInquiryDto.AcademicMediumId);
		await validationService.ValidateStandardExistsAsync(updateInquiryDto.StandardId);

		var student = await studentRepository.GetInquiryForUpdateAsync(studentId)
			?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Student), studentId);
		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, nameof(UpdateInquiryAsync));

		// Guard against updating a student that is no longer in inquiry state
		validationService.ValidateStudentStatus(
			student,
			[StudentLifeCycleStatusEnum.Inquiry],
			StudentRegistrationErrorCodes.StudentNotInInquiryStatus
		);

		var standard = await standardRepository.GetAsync(updateInquiryDto.StandardId, includeDetails: true) ?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard not found");
		await studentScopeAccessService.EnsureCanAccessStandardAsync(
			updateInquiryDto.StandardId,
			nameof(UpdateInquiryAsync));
		await studentScopeAccessService.EnsureCanUseInquiryPreferenceAsync(
			updateInquiryDto.InquiryPreferenceId,
			standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			nameof(UpdateInquiryAsync));

		// Update the aggregate using its business method
		// This maintains aggregate consistency
		student.UpdateInquiry(
			updateInquiryDto.FirstName,
			updateInquiryDto.MiddleName,
			updateInquiryDto.LastName,
			standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,
			standard.EducationMediumStage.EducationMedium.EducationBoardId,
			updateInquiryDto.AcademicMediumId,
			standard.EducationMediumStageId,
			updateInquiryDto.StandardId,
			updateInquiryDto.DateOfBirth,
			updateInquiryDto.Gender,
			updateInquiryDto.DateOfInquiry,
			updateInquiryDto.InquiryPreferenceId,
			updateInquiryDto.PrimaryContactNo,
			updateInquiryDto.SecondaryContactNo,
			updateInquiryDto.Email,
			updateInquiryDto.Address1,
			updateInquiryDto.Address2,
			updateInquiryDto.Address3,
			updateInquiryDto.City,
			updateInquiryDto.State,
			updateInquiryDto.Country,
			updateInquiryDto.PinCode,
			updateInquiryDto.PreviousSchool
		);

		// Single repository call - EF Core tracks changes to all parts
		await studentRepository.UpdateAsync(student, autoSave: true);
		var dtoQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var dto = await AsyncExecuter.FirstOrDefaultAsync(ProjectToStudentDto(dtoQuery.Where(s => s.Id == studentId)));
		PopulateComputedInquiryFields(dto);
		return dto ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Student), studentId);
	}

	public async Task AssignRegistrationFormAsync(
		Guid studentId,
		IssueRegistrationFormDto issueFormDto
	)
	{
		// Load the complete aggregate
		var student = await studentRepository.GetAsync(studentId);
		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, nameof(AssignRegistrationFormAsync));

		await studentEnrollmentManager.ValidateFormNumberUniqueAsync(issueFormDto.FormNumber, student.AcademicYearId ?? Guid.Empty);

		// Use aggregate method - it encapsulates business rules
		student.IssueRegistrationForm(issueFormDto.FormNumber, issueFormDto.Amount);

		// Single update call
		await studentRepository.UpdateAsync(student, autoSave: true);
	}

	private static IQueryable<StudentDto> ProjectToStudentDto(IQueryable<Student> query)
	{
		return query.Select(x => new StudentDto
		{
			StudentId = x.Id,
			InquiryNo = x.AdditionalDetail != null ? x.AdditionalDetail.InquiryNo ?? string.Empty : string.Empty,
			FormNo = x.AdditionalDetail != null ? x.AdditionalDetail.RegistrationFormNo : null,
			StudentName = ((x.FirstName ?? string.Empty) + " " + (x.MiddleName ?? string.Empty) + " " + (x.LastName ?? string.Empty)).Trim(),
			FirstName = x.FirstName,
			MiddleName = x.MiddleName,
			LastName = x.LastName,
			AcademicYearId = x.AcademicYearId,
			AcademicBoardId = x.AcademicBoardId,
			AcademicMediumId = x.AcademicMediumId,
			StandardId = x.StandardId ?? Guid.Empty,
			Standard = x.Standard != null ? x.Standard.Name : string.Empty,
			DivisionId = x.DivisionId,
			Division = x.Division != null ? x.Division.Name : null,
			DateOfBirth = x.AdditionalDetail != null ? x.AdditionalDetail.DateOfBirth : null,
			DateOfInquiry = x.AdditionalDetail != null ? x.AdditionalDetail.DateOfInquiry : null,
			Gender = x.AdditionalDetail != null ? x.AdditionalDetail.Gender : null,
			Category = x.AdditionalDetail != null ? x.AdditionalDetail.Category : Category.General,
			Address1 = x.ContactDetail != null ? x.ContactDetail.Address1 : null,
			Address2 = x.ContactDetail != null ? x.ContactDetail.Address2 : null,
			Address3 = x.ContactDetail != null ? x.ContactDetail.Address3 : null,
			City = x.ContactDetail != null ? x.ContactDetail.City : null,
			State = x.ContactDetail != null ? x.ContactDetail.State : null,
			Country = x.ContactDetail != null ? x.ContactDetail.Country : null,
			PinCode = x.ContactDetail != null ? x.ContactDetail.Pincode : null,
			PrimaryContactNo = x.ContactDetail != null ? x.ContactDetail.PrimaryContactNo : null,
			SecondaryContactNo = x.ContactDetail != null ? x.ContactDetail.SecondaryContactNo : null,
			Email = x.ContactDetail != null ? x.ContactDetail.Email : null,
			PreviousSchool = x.AdditionalDetail != null ? x.AdditionalDetail.PreviousSchool : null,
			InquiryPreferenceId = x.AdditionalDetail != null ? x.AdditionalDetail.InquiryPreferenceId : null,
			InquiryPreference = x.AdditionalDetail != null && x.AdditionalDetail.InquiryPreference != null
				? x.AdditionalDetail.InquiryPreference.PreferenceName
				: null,
			NextReminderDate = x.Notes
				.OrderByDescending(note => note.CreationTime)
				.Select(note => (DateTimeOffset?)note.NextReminderDate)
				.FirstOrDefault(),
			InquiryStatus = x.Notes
				.OrderByDescending(note => note.CreationTime)
				.Select(note => (NoteStatusEnum?)note.Status)
				.FirstOrDefault() ?? NoteStatusEnum.Pending,
			IsFeeStructureAssigned = x.FeeStructureId.HasValue
		});
	}

	private static void PopulateComputedInquiryFields(StudentDto? dto)
	{
		if (dto == null)
		{
			return;
		}

		dto.Age = CalculateAge(dto.DateOfBirth);
		dto.Address = BuildAddress(dto);
	}

	private static string? BuildAddress(StudentDto dto)
	{
		var parts = new[]
		{
			dto.Address1,
			dto.Address2,
			dto.Address3,
			dto.City,
			dto.State?.Name,
			dto.Country?.Name
		}.Where(part => !string.IsNullOrWhiteSpace(part));

		var address = string.Join(", ", parts);
		return string.IsNullOrWhiteSpace(address) ? null : address;
	}

	private static string? CalculateAge(DateTimeOffset? dateOfBirth)
	{
		if (!dateOfBirth.HasValue)
		{
			return null;
		}

		var today = DateTimeOffset.UtcNow;
		var age = today.Year - dateOfBirth.Value.Year;
		if (dateOfBirth.Value.Date > today.AddYears(-age).Date)
		{
			age--;
		}

		return age >= 0 ? $"{age} years" : null;
	}
}
