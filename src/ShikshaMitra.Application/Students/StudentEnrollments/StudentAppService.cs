using ShikshaMitra.Students.StudentDetails;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments;
public class StudentAppService(StudentQueryService studentQueryService,
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	IRepository<StudentContactDetail, Guid> studentContactDetailRepository) : ApplicationService, IStudentAppService
{
	public Task<StudentGeneralInformationDto> GetStudentGeneralInformationAsync(Guid studentId)
	{
		// Implementation to retrieve student general information
		throw new NotImplementedException();
	}
	public async Task<StudentGeneralInformationDto> UpdateStudentGeneralInformationAsync(Guid studentId, CreateUpdateStudentGeneralInformationDto updateStudentGeneralInfoDto)
	{
		try
		{
			var studentQuery = await studentQueryService.GetStudentWithDetailsQueryAsync(studentFilter: s =>
				s.Id == studentId && s.TenantId == CurrentTenant.Id);

			var studentResult = await AsyncExecuter.FirstOrDefaultAsync(studentQuery) ?? throw new BusinessException("Student not found");

			var student = studentResult.Student;
			var studentAdditionalDetail = studentResult.AdditionalDetail;
			var studentContactDetail = studentResult.ContactDetail;

			student.UpdateGeneralInformation(
				updateStudentGeneralInfoDto.FirstName,
				updateStudentGeneralInfoDto.MiddleName,
				updateStudentGeneralInfoDto.LastName
				);

			// TO DO: upload profile picture and get the URL
			var profilePictureUrl = string.Empty;

			studentAdditionalDetail.UpdateGeneralInformation(
				updateStudentGeneralInfoDto.DateOfBirth,
				updateStudentGeneralInfoDto.Gender,
				profilePictureUrl,
				updateStudentGeneralInfoDto.AadharNo,
				updateStudentGeneralInfoDto.ApparId,
				updateStudentGeneralInfoDto.PanNo,
				updateStudentGeneralInfoDto.Religion,
				updateStudentGeneralInfoDto.FamilyCast,
				updateStudentGeneralInfoDto.CastCategory,
				updateStudentGeneralInfoDto.Nationality,
				updateStudentGeneralInfoDto.BirthPlace,
				updateStudentGeneralInfoDto.MotherTongue,
				updateStudentGeneralInfoDto.PreviousSchool,
				updateStudentGeneralInfoDto.LastClassPassed);

			studentContactDetail.UpdateGeneralInformation(
				updateStudentGeneralInfoDto.PrimaryContactNo,
				updateStudentGeneralInfoDto.SecondaryContactNo,
				updateStudentGeneralInfoDto.Email,
				updateStudentGeneralInfoDto.Address1,
				updateStudentGeneralInfoDto.Address2,
				updateStudentGeneralInfoDto.Address3,
				updateStudentGeneralInfoDto.City,
				updateStudentGeneralInfoDto.State,
				updateStudentGeneralInfoDto.Country,
				updateStudentGeneralInfoDto.Pincode
			);

			await studentRepository.UpdateAsync(student);
			await studentAdditionalDetailRepository.UpdateAsync(studentAdditionalDetail);
			await studentContactDetailRepository.UpdateAsync(studentContactDetail);

			return ObjectMapper.Map<Student, StudentGeneralInformationDto>(student);
		}
		catch (Exception ex)
		{
			// Handle exceptions appropriately, possibly logging them
			throw new ApplicationException("An error occurred while updating student general information.", ex);
		}
	}
}
