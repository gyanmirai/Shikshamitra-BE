using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using System.Threading.Tasks;


namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollmentAppService(
	IStudentEnrollmentRepository studentEnrollmentRepository,
	StudentEnrollmentManager studentEnrollmentManager,
	IBlobContainer blobContainer) : CrudAppService<StudentEnrollment, StudentEnrollmentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentEnrollmentDto>(studentEnrollmentRepository), IStudentEnrollmentAppService
{
	#region Get Student Enrollment by Enrollment Id

	// public async Task<StudentEnrollmentDto?> GetStudentEnrollmentByUserId(Guid studentEnrollmentId)
	// {
	// 	StudentEnrollment? studentEnrollment = await studentEnrollmentRepository.GetStudentEnrollmentByUserIdAsync(studentEnrollmentId);
	//
	// 	return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	// }

	#endregion

	#region Create Basic Information

	public async Task<StudentEnrollmentDto> CreateBasicInfoAsync(CreateStudentEnrollmentBasicInfoDto input)
	{
		Console.WriteLine("Creating Student Enrollment called");
		
		Console.WriteLine($"input++++++++++++++++++++++++++++++++ {input}");
		// use studentEnrollmentManager to create the student enrollment with domain logic
		StudentEnrollment studentEnrollment = await studentEnrollmentManager.CreateStudentEnrollmentAsync(
			// userId: input.UserId,
			// roleId:input.RoleId,      
			studentEnrollmentState: input.StudentEnrollmentState,

			// Inquiry
			formNumber: input.FormNumber,
			firstName: input.FirstName,
			middleName: input.MiddleName,
			lastName: input.LastName,
			previousSchoolName: input.PreviousSchoolName,
			dateOfBirth: input.DateOfBirth,
			dateOfInquiry : input.DateOfInquiry,
			genderType : input.GenderType,
			parentContactNumber: input.ParentContactNumber,
			alternativeContactNumber: input.AlternativeContactNumber,
			city: input.City,
			state: input.State,
			pinCode: input.PinCode,
			address: input.Address,
			inquiryStatusType: InquiryStatusType.Pending,
			academicYearId: input.AcademicYearId,
			educationBoardId: input.EducationBoardId,
			educationMediumId: input.EducationMediumId,
			standardId: input.StandardId,
			inquiryPreferenceId: input.InquiryPreferenceId,

			// For Registration
			profilePicture: input.ProfilePicture,
			aadhaarNo: input.AadhaarNo,
			religion: input.Religion,
			familyCast: input.FamilyCast,
			castType: input.CastType,
			motherTongue: input.MotherTongue,
			nationality: input.Nationality,
			birthPlace: input.BirthPlace,
			temporaryGr: input.TemporaryGr,
			remarks: input.Remarks
		);

		// Save the student enrollment
		studentEnrollment = await studentEnrollmentRepository.InsertAsync(studentEnrollment);

		// return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}

	#endregion

	#region Update basic Information

	public async Task<StudentEnrollmentDto> UpdateBasicInfoAsync(Guid studentEnrollmentId, UpdateStudentEnrollmentBasicInfoDto input)
	{
		// Update student enrollment using StudentEnrollment manager 
		StudentEnrollment studentEnrollment = await studentEnrollmentManager.UpdateStudentEnrollmentAsync(
			studentEnrollmentId,
			roleId: input.RoleId,
			studentEnrollmentState: input.StudentEnrollmentState,
			formNumber: input.FormNumber,
			firstName: input.FirstName,
			middleName: input.MiddleName,
			lastName: input.LastName,
			previousSchoolName: input.PreviousSchoolName,
			dateOfBirth: input.DateOfBirth,
			dateOfInquiry: input.DateOfInquiry,
			genderType: input.GenderType,
			parentContactNumber: input.ParentContactNumber,
			alternativeContactNumber: input.AlternativeContactNumber,
			city: input.City,
			state: input.State,
			pinCode: input.PinCode,
			address: input.Address,
			inquiryStatusType: input.InquiryStatusType,
			academicYearId: input.AcademicYearId,
			educationBoardId: input.EducationBoardId,
			educationMediumId: input.EducationMediumId,
			standardId: input.StandardId,
			inquiryPreferenceId: input.InquiryPreferenceId,

			// registration
			profilePicture: input.ProfilePicture,
			aadhaarNo: input.AadhaarNo,
			religion: input.Religion,
			familyCast: input.FamilyCast,
			castType: input.CastType,
			motherTongue: input.MotherTongue,
			nationality: input.Nationality,
			birthPlace: input.BirthPlace,
			temporaryGr: input.TemporaryGr,
			remarks: input.Remarks
		);
		
		studentEnrollment  = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);
		
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}

	#endregion

	#region Update Family Information

	public async Task<StudentEnrollmentDto> UpdateFamilyInfo(Guid studentEnrollmentId, UpdateStudentFamilyDetailsDto input)
	{
		// convert the input to domain model and update the student enrollment
		StudentContactDetails? fatherDetails =
			ObjectMapper.Map<StudentContactDetailsDto, StudentContactDetails>(input.FatherDetails);
		StudentContactDetails? motherDetails =
			ObjectMapper.Map<StudentContactDetailsDto, StudentContactDetails>(input.MotherDetails);
		StudentContactDetails? guardianDetails =
			ObjectMapper.Map<StudentContactDetailsDto, StudentContactDetails>(input.GuardianDetails);

		StudentEnrollment? studentEnrollment = await studentEnrollmentManager.UpdateFamilyDetailsByStudentEnrollmentIdAsync(studentEnrollmentId, fatherDetails, motherDetails, guardianDetails);

		// Save the student enrollment
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);
		
		// return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}
	#endregion

	#region Update Parent bank details

	public async Task<StudentEnrollmentDto> UpdateParentBankDetails(Guid studentEnrollmentId, ParentBankDetailsDto input)
	{
		ParentBankDetails parentBankDetails = ObjectMapper.Map<ParentBankDetailsDto, ParentBankDetails>(input);

		StudentEnrollment studentEnrollment =
			await studentEnrollmentManager.UpdateParentBankDetailsByStudentEnrollmentIdAsync(studentEnrollmentId, parentBankDetails);

		// Save the employee
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

		// Return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}


	#endregion

	#region Update Previous School details

	public async Task<StudentEnrollmentDto> UpdatePreviousSchoolDetails(Guid studentEnrollmentId, PreviousSchoolDetailsDto input)
	{
		PreviousSchoolDetails previousSchoolDetails = ObjectMapper.Map<PreviousSchoolDetailsDto, PreviousSchoolDetails>(input);

		StudentEnrollment studentEnrollment =
			await studentEnrollmentManager.UpdatePreviousSchoolDetailsByStudentEnrollmentIdAsync(studentEnrollmentId, previousSchoolDetails);

		// Save the employee
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

		// Return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}
	#endregion

	#region Update stuent additional details

	public async Task<StudentEnrollmentDto> UpdateStudentAdditionalDetails(Guid studentEnrollmentId, StudentAdditionalDetailsDto input)
	{
		StudentAdditionalDetails studentAdditionalDetails = ObjectMapper.Map<StudentAdditionalDetailsDto, StudentAdditionalDetails>(input);

		StudentEnrollment studentEnrollment =
			await studentEnrollmentManager.UpdateAdditionalDetailsByStudentEnrollmentIdAsync(studentEnrollmentId, studentAdditionalDetails);

		// Save the employee
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

		// Return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}
	#endregion

	#region Update stuednt communication details

	public async Task<StudentEnrollmentDto> UpdateStudentCommunicationDetails(Guid studentEnrollmentId, StudentCommunicationDetailsDto input)
	{
		StudentCommunicationDetails studentCommunicationDetails = ObjectMapper.Map<StudentCommunicationDetailsDto, StudentCommunicationDetails>(input);

		StudentEnrollment studentEnrollment =
			await studentEnrollmentManager.UpdateCommunicationDetailsByStudentEnrollmentIdAsync(studentEnrollmentId, studentCommunicationDetails);

		// Save the employee
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

		// Return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}
	#endregion

	#region Update stuednt medical details

	public async Task<StudentEnrollmentDto> UpdateStudentMedicalRecords(Guid studentEnrollmentId, StudentMedicalRecordsDto input)
	{
		StudentMedicalRecords studentMedicalRecords = ObjectMapper.Map<StudentMedicalRecordsDto, StudentMedicalRecords>(input);

		StudentEnrollment studentEnrollment =
			await studentEnrollmentManager.UpdateMedicalDetailsByStudentEnrollmentIdAsync(studentEnrollmentId, studentMedicalRecords);

		// Save the employee
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

		// Return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}
	#endregion

	#region  Student Dociments APIs

	public async Task UploadDocumentsAsync(Guid studentEnrollmentId, List<DocumentDto> documents)
	{
		// Map the DTOs to domain entities or value objects
		List<StudentDocumentData> studentDocuments = documents.ConvertAll(doc => new StudentDocumentData
		{
			FileName = doc.FileName, ContentType = doc.ContentType, Content = doc.Content, Size = doc.Size
		});

		// Call the domain service
		await studentEnrollmentManager.AddDocumentsAsync(studentEnrollmentId, studentDocuments);
	}
	
	
	public async Task<DocumentDto> GetDocumentAsync(Guid studentEnrollmentId, Guid documentId)
	{
		StudentDocument document = await studentEnrollmentManager.GetDocumentAsync(studentEnrollmentId, documentId);

		await using var stream = await blobContainer.GetAsync(document.BlobName);
		await using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);
		return new DocumentDto
		{
			FileName = document.DocumentName,
			ContentType = document.ContentType,
			Content = memoryStream.ToArray(),
			Size = document.Size
		};
	}
	
	
	public async Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid studentEnrollmentId)
	{
		var documents = await studentEnrollmentManager.GetDocumentsAsync(studentEnrollmentId);
		return documents.Select(doc => new DocumentMetadataDto
		{
			Id = doc.Id, FileName = doc.DocumentName, Size = doc.Size, ContentType = doc.ContentType
		}).ToList();
	}
	
	public async Task<DocumentDto> UpdateDocumentAsync(Guid studentEnrollmentId, Guid documentId, DocumentDto documentDto)
	{
		var updatedDocument = await studentEnrollmentManager.UpdateDocumentAsync(
			studentEnrollmentId,
			documentId,
			documentDto.FileName,
			documentDto.ContentType,
			documentDto.Size,
			documentDto.Content
		);

		return new DocumentDto
		{
			FileName = updatedDocument.DocumentName,
			ContentType = updatedDocument.ContentType,
			Content = documentDto.Content,
			Size = updatedDocument.Size,
			Id = updatedDocument.Id
		};
	}

	#endregion	
}