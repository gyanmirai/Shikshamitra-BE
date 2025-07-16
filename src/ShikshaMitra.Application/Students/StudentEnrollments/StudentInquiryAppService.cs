using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
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
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	IRepository<StudentContactDetail, Guid> studentContactDetailRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	IRepository<Standard, Guid> standardRepository,
	IGuidGenerator guidGenerator,
	StudentEnrollmentManagerNew studentEnrollmentManager
) : ApplicationService, IStudentInquiryAppService
{
	public async Task<StudentInquiryDto> CreateStudentInquiryAsync(CreateUpdateInquiryDto createStudentInquiryDto)
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

	public Task<PagedResultDto<StudentInquiryDto>> GetInquiriesByMediumAsync(
		Guid academicMediumId,
		int skipCount = 0,
		int maxResultCount = 10
	) => throw new NotImplementedException();

	public Task<StudentInquiryDto> UpdateInquiryAsync(
		Guid studentId,
		CreateUpdateInquiryDto updateInquiryDto
	) => throw new NotImplementedException();

	public Task AssignRegistrationFormAsync(
		Guid studentId,
		CreateUpdateInquiryDto createUpdateInquiryDto
	) => throw new NotImplementedException();

	private async Task ValidateReferencesAsync(CreateUpdateInquiryDto inquiryDto)
	{
		if (!await academicMediumRepository.AnyAsync(x => x.Id == inquiryDto.AcademicMediumId))
		{
			throw new UserFriendlyException("", L["Academic Medium Not Found"]);
		}

		if (!await standardRepository.AnyAsync(x => x.Id == inquiryDto.StandardId))
		{
			throw new UserFriendlyException("", L["Standard Not Found"]);
		}

		if (inquiryDto.InquiryPreferenceId.HasValue) { }
	}
}
