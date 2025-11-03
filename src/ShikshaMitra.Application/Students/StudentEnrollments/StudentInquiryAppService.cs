using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.ObjectMapping;

namespace ShikshaMitra.Students.StudentEnrollments;

[RemoteService(false)]
public class StudentInquiryAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	IRepository<StudentContactDetail, Guid> studentContactDetailRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	IRepository<Standard, Guid> standardRepository,
	IGuidGenerator guidGenerator,
	StudentEnrollmentManagerNew studentEnrollmentManager
) : ApplicationService, IStudentInquiryAppService
{
	public async Task<StudentInquiryDto> CreateStudentInquiryAsync(
		CreateUpdateInquiryDto createStudentInquiryDto
	)
	{
		await ValidateReferencesAsync(createStudentInquiryDto);

		Guid studentId = guidGenerator.Create();
		var inquiryNo = await studentEnrollmentManager.GenerateInquiryNumberAsync(
			createStudentInquiryDto.AcademicMediumId
		);
		var student = new Student(
			studentId,
			CurrentTenant.Id,
			createStudentInquiryDto.FirstName,
			createStudentInquiryDto.MiddleName,
			createStudentInquiryDto.LastName,
			createStudentInquiryDto.AcademicMediumId,
			createStudentInquiryDto.StandardId,
			status: StudentLifeCycleStatusEnum.Inquiry
		);

		var studentAdditionalDetail = new StudentAdditionalDetail(
			studentId,
			CurrentTenant.Id,
			createStudentInquiryDto.DateOfBirth,
			createStudentInquiryDto.Gender,
			createStudentInquiryDto.DateOfInquiry,
			inquiryNo,
			createStudentInquiryDto.InquiryPreferenceId
		);

		var studentContactDetails = new StudentContactDetail(
			studentId,
			CurrentTenant.Id,
			createStudentInquiryDto.PrimaryContactNo,
			createStudentInquiryDto.SecondaryContactNo,
			createStudentInquiryDto.Email,
			createStudentInquiryDto.Address1,
			createStudentInquiryDto.Address2,
			createStudentInquiryDto.Address3,
			createStudentInquiryDto.City,
			createStudentInquiryDto.State,
			createStudentInquiryDto.Country,
			createStudentInquiryDto.PinCode
		);

		await studentRepository.InsertAsync(student);
		await studentAdditionalDetailRepository.InsertAsync(studentAdditionalDetail);
		await studentContactDetailRepository.InsertAsync(studentContactDetails);

		return ObjectMapper.Map<Student, StudentInquiryDto>(student);
	}

	public async Task<PagedResultDto<StudentInquiryDto>> GetInquiriesByMediumAsync(
		Guid academicMediumId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null
	)
	{
		try
		{
			await CheckAcademicMediumExistsAsync(academicMediumId);

			var query = await GetStudentWithDetailsQueryAsync(studentFilter: s =>
				s.Status == StudentLifeCycleStatusEnum.Inquiry
				&& s.EducationMediumId == academicMediumId
				&& s.TenantId == CurrentTenant.Id
			);

			var totalCount = query.Count();
			var pagedQuery = query.Skip(skipCount).Take(maxResultCount);

			var results = await AsyncExecuter.ToListAsync(pagedQuery);
			var resultList = ObjectMapper.Map<List<StudentQueryResult>, List<StudentInquiryDto>>(
				results
			);

			return new PagedResultDto<StudentInquiryDto>(totalCount, resultList.ToList());
		}
		catch (Exception ex)
		{
			// Handle exception as needed
			throw;
		}
	}

	public async Task<StudentInquiryDto> UpdateInquiryAsync(
		Guid studentId,
		CreateUpdateInquiryDto updateInquiryDto
	)
	{
		var studentQueryResult = await CheckandReturnStudentExistsAsync(studentId);
		await ValidateReferencesAsync(updateInquiryDto);

		var student = studentQueryResult.Student;
		var studentAdditionalDetail = studentQueryResult.AdditionalDetail;
		var studentContactDetails = studentQueryResult.ContactDetail;

		// Update student properties
		student.UpdateDetails(
			updateInquiryDto.FirstName,
			updateInquiryDto.MiddleName,
			updateInquiryDto.LastName,
			updateInquiryDto.AcademicMediumId,
			updateInquiryDto.StandardId,
			StudentLifeCycleStatusEnum.Inquiry
		);

		// Update additional details
		studentAdditionalDetail.UpdateDetails(
			updateInquiryDto.DateOfBirth,
			updateInquiryDto.Gender,
			updateInquiryDto.DateOfInquiry,
			updateInquiryDto.InquiryPreferenceId
		);

		// Update contact details
		studentContactDetails.UpdateContactInfo(
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

		// Save changes
		await studentRepository.UpdateAsync(student);
		await studentAdditionalDetailRepository.UpdateAsync(studentAdditionalDetail);
		await studentContactDetailRepository.UpdateAsync(studentContactDetails);

		return ObjectMapper.Map<Student, StudentInquiryDto>(student);
	}

	public async Task AssignRegistrationFormAsync(
		Guid studentId,
		IssueRegistrationFormDto issueFormDto
	)
	{
		try
		{
			var studentQueryResult = await CheckandReturnStudentExistsAsync(studentId);

			var student = studentQueryResult.Student;
			var studentAdditionalDetail = studentQueryResult.AdditionalDetail;

			studentAdditionalDetail.IssueRegistrationForm(
				issueFormDto.FormNumber,
				issueFormDto.Amount);
			student.ChangeStatus(StudentLifeCycleStatusEnum.RegistrationFormIssued);

			await studentRepository.UpdateAsync(student);
			await studentAdditionalDetailRepository.UpdateAsync(studentAdditionalDetail);
		}
		catch (Exception ex)
		{
			// Handle exception as needed
			throw;
		}
	}

	private async Task<IQueryable<StudentQueryResult>> GetStudentWithDetailsQueryAsync(
		Expression<Func<Student, bool>>? studentFilter = null,
		Expression<Func<StudentAdditionalDetail, bool>>? additionalDetailFilter = null,
		Expression<Func<StudentContactDetail, bool>>? contactDetailFilter = null
	)
	{
		var studentQueryable = await studentRepository.GetQueryableAsync();
		var additionalDetailQueryable = await studentAdditionalDetailRepository.GetQueryableAsync();
		var contactDetailQueryable = await studentContactDetailRepository.GetQueryableAsync();

		if (studentFilter != null)
			studentQueryable = studentQueryable.Where(studentFilter);

		if (additionalDetailFilter != null)
			additionalDetailQueryable = additionalDetailQueryable.Where(additionalDetailFilter);

		if (contactDetailFilter != null)
			contactDetailQueryable = contactDetailQueryable.Where(contactDetailFilter);

		var query = studentQueryable
			.Join(
				additionalDetailQueryable,
				s => s.Id,
				sad => sad.Id,
				(s, sad) => new { Student = s, AdditionalDetail = sad }
			)
			.Join(
				contactDetailQueryable,
				x => x.Student.Id,
				scd => scd.Id,
				(x, scd) =>
					new StudentQueryResult
					{
						Student = x.Student,
						AdditionalDetail = x.AdditionalDetail,
						ContactDetail = scd,
					}
			);

		return query;
	}

	private async Task ValidateReferencesAsync(CreateUpdateInquiryDto inquiryDto)
	{
		await CheckAcademicMediumExistsAsync(inquiryDto.AcademicMediumId);
		await CheckStandardExistsAsync(inquiryDto.StandardId);

		if (inquiryDto.InquiryPreferenceId.HasValue) { }
	}

	private async Task<StudentQueryResult> CheckandReturnStudentExistsAsync(Guid studentId)
	{
		var query = await GetStudentWithDetailsQueryAsync(
			studentFilter: s => s.Id == studentId
		);
		var result = await AsyncExecuter.FirstOrDefaultAsync(query);
		return result ?? throw new BusinessException("", L["Student Not Found"]);
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
