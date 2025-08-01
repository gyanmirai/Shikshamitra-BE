using ShikshaMitra.Students.StudentDetails;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments;

[RemoteService(false)]
public class StudentAppService(StudentQueryService studentQueryService,
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	IRepository<StudentContactDetail, Guid> studentContactDetailRepository,
	EntityValidationService entityValidationService) : ApplicationService, IStudentAppService
{
	public async Task<StudentGeneralInformationDto> GetStudentGeneralInformationAsync(Guid studentId)
	{
		try
		{
			var studentQuery = await studentQueryService.GetStudentWithDetailsQueryAsync(studentFilter: s =>
				s.Id == studentId && s.TenantId == CurrentTenant.Id);

			var studentResult = await AsyncExecuter.FirstOrDefaultAsync(studentQuery) ?? throw new BusinessException("Student not found");

			return ObjectMapper.Map<StudentQueryResult, StudentGeneralInformationDto>(studentResult);
		}
		catch (Exception ex)
		{
			// Handle exceptions appropriately, possibly logging them
			throw new ApplicationException("An error occurred while retrieving student general information.", ex);
		}
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

			var updatedState = studentAdditionalDetail.StudentRegistrationState > StudentRegistrationStateEnum.General
				? studentAdditionalDetail.StudentRegistrationState
				: StudentRegistrationStateEnum.General;
			if (studentAdditionalDetail.StudentRegistrationState == StudentRegistrationStateEnum.Default)
			{
				student.ChangeStatus(StudentLifeCycleStatusEnum.Registation);
			}

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
				updateStudentGeneralInfoDto.LastClassPassed,
				updatedState);

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

			return ObjectMapper.Map<StudentQueryResult, StudentGeneralInformationDto>(studentResult);
		}
		catch (Exception ex)
		{
			// Handle exceptions appropriately, possibly logging them
			throw new ApplicationException("An error occurred while updating student general information.", ex);
		}
	}
}
