using ShikshaMitra.Students.Enrollments.Inquiries;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.TenantSettings.Standards;
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
	IRepository<Student, Guid> studentRepository,
	IRepository<Standard, Guid> standardRepository,
	IGuidGenerator guidGenerator,
	StudentEnrollmentManager studentEnrollmentManager,
	StudentEnrollmentDtoEnricher dtoEnricher,
	EntityValidationService validationService
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
		// Load the complete aggregate with all details
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);

		// ABP data filters enforce tenant isolation; validate the lifecycle state
		validationService.ValidateStudentStatus(
			student,
			[StudentLifeCycleStatusEnum.Inquiry],
			StudentRegistrationErrorCodes.StudentNotInInquiryStatus
		);

		return ObjectMapper.Map<Student, StudentDto>(student);
	}

	public async Task<PagedResultDto<StudentDto>> GetInquiriesByMediumAsync(
		Guid academicMediumId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null
	)
	{
		await validationService.ValidateAcademicMediumExistsAsync(academicMediumId);

		// Get queryable with related details included for proper mapping
		var queryable = await studentRepository.WithDetailsAsync(
			 s => s.AdditionalDetail,
			 s => s.ContactDetail,
			 s => s.Notes
		);

		var query = queryable
			.Where(s => s.Status == StudentLifeCycleStatusEnum.Inquiry
				&& s.AcademicMediumId == academicMediumId
				&& s.TenantId == CurrentTenant.Id);

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

		var pagedQuery = query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skipCount)
			.Take(maxResultCount);

		var results = await AsyncExecuter.ToListAsync(pagedQuery);
		var resultList = ObjectMapper.Map<List<Student>, List<StudentDto>>(results);

		return new PagedResultDto<StudentDto>(totalCount, resultList);
	}

	public async Task<StudentDto> UpdateInquiryAsync(
		Guid studentId,
		CreateUpdateInquiryDto updateInquiryDto
	)
	{
		await validationService.ValidateAcademicMediumExistsAsync(updateInquiryDto.AcademicMediumId);
		await validationService.ValidateStandardExistsAsync(updateInquiryDto.StandardId);

		// Load the complete aggregate (with all parts due to AutoInclude)
		var student = await studentRepository.GetAsync(studentId);

		// Guard against updating a student that is no longer in inquiry state
		validationService.ValidateStudentStatus(
			student,
			[StudentLifeCycleStatusEnum.Inquiry],
			StudentRegistrationErrorCodes.StudentNotInInquiryStatus
		);

		var standard = await standardRepository.GetAsync(updateInquiryDto.StandardId, includeDetails: true) ?? throw new BusinessException(StudentRegistrationErrorCodes.StandardNotFound, "Standard not found");

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
		return ObjectMapper.Map<Student, StudentDto>(student);
	}

	public async Task AssignRegistrationFormAsync(
		Guid studentId,
		IssueRegistrationFormDto issueFormDto
	)
	{
		// Load the complete aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Use aggregate method - it encapsulates business rules
		student.IssueRegistrationForm(issueFormDto.FormNumber, issueFormDto.Amount);

		// Single update call
		await studentRepository.UpdateAsync(student, autoSave: true);
	}
}
