using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments;
public interface IStudentRegistrationAppService
{
	Task<PagedResultDto<StudentRegistrationFormDto>> GetRegistrationFormsByMediumAsync(
		Guid academicMediumId, 
		int skipCount = 0, 
		int maxResultCount = 10, 
		string? searchTerm = null);

	Task<PagedResultDto<StudentRegistrationDto>> GetRegistrationsByMediumAsync(
		Guid academicMediumId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null);

	/// <summary>
	/// Updates general information for a student in RegistrationFormIssued status.
	/// Upon successful validation, transitions the student to Registration status.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">General information data</param>
	/// <returns>Updated student information with new status</returns>
	Task<UpdateGeneralInfoOutputDto> UpdateGeneralInfoAsync(Guid studentId, UpdateGeneralInfoDto input);

	/// <summary>
	/// Creates a new student directly in Registration status without going through inquiry/form issued flow.
	/// This allows direct registration of students.
	/// </summary>
	/// <param name="input">General information data including education medium</param>
	/// <returns>Newly created student in Registration status</returns>
	Task<UpdateGeneralInfoOutputDto> CreateDirectRegistrationAsync(CreateDirectRegistrationDto input);

	/// <summary>
	/// Gets family information for a student in Registration status.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Family information including father, mother, guardian, and sibling details</returns>
	Task<StudentFamilyInfoDto> GetFamilyInfoAsync(Guid studentId);

	/// <summary>
	/// Updates family information for a student in Registration status.
	/// This includes father, mother, guardian, and sibling information.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Family information data</param>
	/// <returns>Updated family information</returns>
	Task<StudentFamilyInfoDto> UpdateFamilyInfoAsync(Guid studentId, UpdateFamilyInfoDto input);

	/// <summary>
	/// Gets communication/address information for a student in Registration status.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Communication information including local, permanent, and emergency addresses</returns>
	Task<StudentCommunicationInfoDto> GetCommunicationInfoAsync(Guid studentId);

	/// <summary>
	/// Updates communication/address information for a student in Registration status.
	/// This includes local address, permanent address, and emergency address.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Communication information data</param>
	/// <returns>Updated communication information</returns>
	Task<StudentCommunicationInfoDto> UpdateCommunicationInfoAsync(Guid studentId, UpdateCommunicationInfoDto input);

	/// <summary>
	/// Gets additional details information for a student in Registration status.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Additional details information</returns>
	Task<StudentAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid studentId);

	/// <summary>
	/// Updates additional details for a student in Registration status.
	/// This includes category, BPL status, disability status, emergency contacts, etc.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Additional details data</param>
	/// <returns>Updated additional details information</returns>
	Task<StudentAdditionalDetailsDto> UpdateAdditionalDetailsAsync(Guid studentId, UpdateAdditionalDetailsDto input);

	/// <summary>
	/// Gets medical records information for a student in Registration status.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Medical records information</returns>
	Task<StudentMedicalRecordsDto> GetMedicalRecordsAsync(Guid studentId);

	/// <summary>
	/// Updates medical records for a student in Registration status.
	/// This includes weight, height, immunization, allergies, etc.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Medical records data</param>
	/// <returns>Updated medical records information</returns>
	Task<StudentMedicalRecordsDto> UpdateMedicalRecordsAsync(Guid studentId, UpdateMedicalRecordsDto input);

	/// <summary>
	/// Gets previous school details information for a student in Registration status.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Previous school details information</returns>
	Task<StudentPreviousSchoolDetailsDto> GetPreviousSchoolDetailsAsync(Guid studentId);

	/// <summary>
	/// Updates previous school details for a student in Registration status.
	/// This includes school name, board, TC number, academic performance, etc.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Previous school details data</param>
	/// <returns>Updated previous school details information</returns>
	Task<StudentPreviousSchoolDetailsDto> UpdatePreviousSchoolDetailsAsync(Guid studentId, UpdatePreviousSchoolDetailsDto input);

	/// <summary>
	/// Gets bank details information for a student in Registration status.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Bank details information</returns>
	Task<StudentBankDetailsDto> GetBankDetailsAsync(Guid studentId);

	/// <summary>
	/// Updates bank details for a student in Registration status.
	/// This includes bank name, account number, IFSC code, branch details, etc.
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Bank details data</param>
	/// <returns>Updated bank details information</returns>
	Task<StudentBankDetailsDto> UpdateBankDetailsAsync(Guid studentId, UpdateBankDetailsDto input);
}
