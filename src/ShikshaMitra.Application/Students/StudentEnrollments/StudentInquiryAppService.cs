using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
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
	IRepository<EducationMedium, Guid> academicMediumRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IGuidGenerator guidGenerator,
	StudentEnrollmentManagerNew studentEnrollmentManager,
	StudentEnrollmentDtoEnricher dtoEnricher
) : ApplicationService, IStudentInquiryAppService
{
	public async Task<StudentInquiryDto> GetInquiryByIdAsync(Guid studentId)
	{
		// Load the complete aggregate with all details
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);

		// Verify the student is an inquiry
		if (student.Status != StudentLifeCycleStatusEnum.Inquiry)
		{
			throw new BusinessException("", L["Student is not in Inquiry status"]);
		}

		// Verify tenant isolation
		if (student.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("", L["Inquiry Not Found"]);
		}

		var dto = ObjectMapper.Map<Student, StudentInquiryDto>(student);
		await dtoEnricher.EnrichSingleDtoAsync(dto);
		return dto;
	}

	public async Task<StudentInquiryDto> CreateStudentInquiryAsync(
		CreateUpdateInquiryDto createStudentInquiryDto
	)
	{
		await ValidateReferencesAsync(createStudentInquiryDto);

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
			createStudentInquiryDto.AcademicMediumId,
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

		// Re-load with details to ensure mapper has all data populated
		var saved = await studentRepository.GetAsync(student.Id, includeDetails: true);
		var dto = ObjectMapper.Map<Student, StudentInquiryDto>(saved);
		await dtoEnricher.EnrichSingleDtoAsync(dto);
		return dto;
	}

	public async Task<PagedResultDto<StudentInquiryDto>> GetInquiriesByMediumAsync(
		Guid academicMediumId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null
	)
	{
		await CheckAcademicMediumExistsAsync(academicMediumId);

		// Get queryable with related details included for proper mapping
		var queryable = await studentRepository.WithDetailsAsync(
			 s => s.AdditionalDetail,
			 s => s.ContactDetail
		);
		
		var query = queryable
			.Where(s => s.Status == StudentLifeCycleStatusEnum.Inquiry
				&& s.EducationMediumId == academicMediumId
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
			return new PagedResultDto<StudentInquiryDto>(0, new List<StudentInquiryDto>());
		}

		var pagedQuery = query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skipCount)
			.Take(maxResultCount);

		var results = await AsyncExecuter.ToListAsync(pagedQuery);
		var resultList = ObjectMapper.Map<List<Student>, List<StudentInquiryDto>>(results);
		
		// Use batch enrichment to avoid N+1 problem
		await dtoEnricher.EnrichDtoListAsync(resultList, results);

		return new PagedResultDto<StudentInquiryDto>(totalCount, resultList);
	}

	public async Task<StudentInquiryDto> UpdateInquiryAsync(
		Guid studentId,
		CreateUpdateInquiryDto updateInquiryDto
	)
	{
		await ValidateReferencesAsync(updateInquiryDto);

		// Load the complete aggregate (with all parts due to AutoInclude)
		var student = await studentRepository.GetAsync(studentId);

		// Update the aggregate using its business method
		// This maintains aggregate consistency
		student.UpdateInquiry(
			updateInquiryDto.FirstName,
			updateInquiryDto.MiddleName,
			updateInquiryDto.LastName,
			updateInquiryDto.AcademicMediumId,
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
			updateInquiryDto.PinCode
		);

		// Single repository call - EF Core tracks changes to all parts
		await studentRepository.UpdateAsync(student, autoSave: true);

		// Re-load with details to ensure mapper has all data populated
		var saved = await studentRepository.GetAsync(student.Id, includeDetails: true);
		var dto = ObjectMapper.Map<Student, StudentInquiryDto>(saved);
		await dtoEnricher.EnrichSingleDtoAsync(dto);
		return dto;
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

	private async Task ValidateReferencesAsync(CreateUpdateInquiryDto inquiryDto)
	{
		await CheckAcademicMediumExistsAsync(inquiryDto.AcademicMediumId);
		await CheckStandardExistsAsync(inquiryDto.StandardId);
	}

	private async Task CheckStandardExistsAsync(Guid standardId)
	{
		if (!await standardRepository.AnyAsync(x => x.Id == standardId))
		{
			throw new BusinessException("", L["Standard Not Found"]);
		}
	}

	private async Task CheckAcademicMediumExistsAsync(Guid academicMediumId)
	{
		if (!await academicMediumRepository.AnyAsync(x => x.Id == academicMediumId))
		{
			throw new BusinessException("", L["Academic Medium Not Found"]);
		}
	}
}
