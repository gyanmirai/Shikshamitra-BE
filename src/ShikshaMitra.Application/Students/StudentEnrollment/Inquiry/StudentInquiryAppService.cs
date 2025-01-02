using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using System.Threading.Tasks;
using ShikshaMitra.Students.studentenrollment.Inquiry;
using ShikshaMitra.Students.StudentEnrollment.Inquiry;


public class StudentInquiryAppService(
	IStudentInquiryRepository studentInquiryRepository,
	StudentInquiryManager studentInquiryManager
	) : CrudAppService<StudentInquiry,StudentInquiryDto,Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentInquiryDto>(studentInquiryRepository),IStudentInquiryAppService
{
	public override async Task<StudentInquiryDto> CreateAsync(CreateUpdateStudentInquiryDto input)
	{
		StudentInquiry studentInquiry = await studentInquiryManager.CreateStudentInquiryAsync(
			firstName: input.FirstName,
			middleName: input.MiddleName,
			lastName: input.LastName,
			formNumber: input.FormNumber,
			previousSchoolName: input.PreviousSchoolName,
			dateOfBirth: input.DateOfBirth,
			age: input.Age,
			dateOfInquiry: input.DateOfInquiry,
			genderType: input.GenderType,
			parentContactNumber: input.ParentContactNumber,
			alternativeContactNumber: input.AlternativeContactNumber,
			city: input.City,
			state: input.State,
			pinCode: input.PinCode,
			address: input.Address,
			educationMediumId: input.EducationMediumId,
			academicYearId: input.AcademicYearId,
			standerdId: input.StanderdId,
			inquiryPreferenceId : input.InquiryPreferenceId
			
		);

		await studentInquiryRepository.InsertAsync(studentInquiry);
		return ObjectMapper.Map<StudentInquiry, StudentInquiryDto>(studentInquiry);
	}
}