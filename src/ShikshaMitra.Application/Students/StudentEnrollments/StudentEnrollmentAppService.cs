using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using System.Threading.Tasks;
using Volo.Abp;
using ShikshaMitra.Students.StudentRecords;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static ShikshaMitra.Students.StudentPermissions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.Students.StudentRecords.Division;


namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollmentAppService(
	IStudentEnrollmentRepository studentEnrollmentRepository,
	IEducationMediumRepository educationMediumRepository,
	IStudentRepository studentRepository,
	IStandardRepository standardRepository,
	StudentEnrollmentManager studentEnrollmentManager,
	IBlobContainer blobContainer) :
	CrudAppService<StudentEnrollment, StudentEnrollmentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentEnrollmentDto>(studentEnrollmentRepository),
	IStudentEnrollmentAppService
{
	#region Get Student Enrollment by Enrollment Id

	// public async Task<StudentEnrollmentDto?> GetStudentEnrollmentByUserId(Guid studentEnrollmentId)
	// {
	// 	StudentEnrollment? studentEnrollment = await studentEnrollmentRepository.GetStudentEnrollmentByUserIdAsync(studentEnrollmentId);
	//
	// 	return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	// }

	#endregion

	#region Move To Registration
	public async Task<StudentEnrollmentDto> MoveToRegistration(Guid studentEnrollmentId)
	{
		StudentEnrollment studentEnrollment = await studentEnrollmentManager.MoveToRegistrationAsync(studentEnrollmentId);

		// Save the student enrollment
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

		// Return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}

	#endregion

	#region Move To Addmission
	public async Task<StudentEnrollmentDto> MoveToAdmission(Guid studentEnrollmentId)
	{

		StudentEnrollment studentEnrollment = await studentEnrollmentManager.MoveToAdmissionAsync(studentEnrollmentId);

		// Save the student enrollment
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

		// Return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}

	#endregion

	#region Move To Addmission Fees
	//public async Task<StudentEnrollmentDto> MoveToAdmissionFees(Guid studentEnrollmentId)
	//{

	//	StudentEnrollment studentEnrollment = await studentEnrollmentManager.MoveToAdmissionFeesAsync(studentEnrollmentId);

	//	// Save the student enrollment
	//	studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

	//	// Return the DTO
	//	return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	//}

	#endregion

	#region Get Student Inquiries list

	public async Task<PagedResultDto<StudentEnrollmentDto>> GetStudentInquiry()
	{
		List<StudentEnrollment> studentInquiry =
			await studentEnrollmentRepository.GetListAsync(
				e => e.StudentEnrollmentState == StudentEnrollmentState.Inquiry,
				includeDetails: true
			);

		return new PagedResultDto<StudentEnrollmentDto>
		{
			TotalCount = studentInquiry.Count,
			Items = ObjectMapper.Map<List<StudentEnrollment>, List<StudentEnrollmentDto>>(studentInquiry)
		};
	}

	#endregion

	#region Get Student Registration form list
	public async Task<PagedResultDto<StudentEnrollmentDto>> GetStudentRegistrationFormAsync()
	{
		List<StudentEnrollment> studentInquiry =
			await studentEnrollmentRepository.GetListAsync(
				e => e.StudentEnrollmentState == StudentEnrollmentState.RegistrationForm,
				includeDetails: true
			);

		return new PagedResultDto<StudentEnrollmentDto>
		{
			TotalCount = studentInquiry.Count,
			Items = ObjectMapper.Map<List<StudentEnrollment>, List<StudentEnrollmentDto>>(studentInquiry)
		};
	}

	#endregion

	#region Get Student Registration list
	public async Task<PagedResultDto<StudentEnrollmentDto>> GetStudentRegistrationAsync()
	{
		List<StudentEnrollment> studentInquiry =
			await studentEnrollmentRepository.GetListAsync(
				e => e.StudentEnrollmentState == StudentEnrollmentState.Registration,
				includeDetails: true
			);

		return new PagedResultDto<StudentEnrollmentDto>
		{
			TotalCount = studentInquiry.Count,
			Items = ObjectMapper.Map<List<StudentEnrollment>, List<StudentEnrollmentDto>>(studentInquiry)
		};
	}
	#endregion

	#region Get Student Admission list
	public async Task<PagedResultDto<StudentEnrollmentDto>> GetStudentAdmissionAsync()
	{
		List<StudentEnrollment> studentInquiry =
			await studentEnrollmentRepository.GetListAsync(
				e => e.StudentEnrollmentState == StudentEnrollmentState.Admission,
				includeDetails: true
			);

		return new PagedResultDto<StudentEnrollmentDto>
		{
			TotalCount = studentInquiry.Count,
			Items = ObjectMapper.Map<List<StudentEnrollment>, List<StudentEnrollmentDto>>(studentInquiry)
		};
	}
	#endregion

	#region Get Student Admission fees list
	//public async Task<PagedResultDto<StudentEnrollmentDto>> GetStudentAdmissionFeesAsync()
	//{
	//	List<StudentEnrollment> studentInquiry =
	//		await studentEnrollmentRepository.GetListAsync(
	//			e => e.StudentEnrollmentState == StudentEnrollmentState.AdmissionFees,
	//			includeDetails: true
	//		);

	//	return new PagedResultDto<StudentEnrollmentDto>
	//	{
	//		TotalCount = studentInquiry.Count,
	//		Items = ObjectMapper.Map<List<StudentEnrollment>, List<StudentEnrollmentDto>>(studentInquiry)
	//	};
	//}
	#endregion

	#region Create student registration

	public async Task<StudentEnrollmentDto> CreateStudentRegistrationAsync(CreateUpdateStudentRegistrationDto input)
	{
		StudentEnrollment studentEnrollment = await studentEnrollmentManager.CreateStudentRegistrationAsync(
			formNumber: input.FormNumber,
			firstName: input.FirstName,
			middleName: input.MiddleName,
			lastName: input.LastName,
			previousSchoolName: input.PreviousSchoolName,
			dateOfBirth: input.DateOfBirth,
			genderType: input.GenderType,
			parentContactNumber: input.ParentContactNumber,
			alternativeContactNumber: input.AlternativeContactNumber,
			addressLine1: input.AddressLine1,
			addressLine2: input.AddressLine2,
			addressLine3: input.AddressLine3,
			country: input.Country,
			city: input.City,
			state: input.State,
			pinCode: input.PinCode,
			academicYearId: input.AcademicYearId,
			educationBoardId: input.EducationBoardId,
			educationMediumId: input.EducationMediumId,
			standardId: input.StandardId,
			inquiryPreferenceId: input.InquiryPreferenceId,
			userId: input.UserId,
			profilePicture: input.ProfilePicture,
			email: input.Email,
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

	#region Create student inquiry

	public async Task<StudentEnrollmentDto> CreateStudentInquiryAsync(CreateStudentInquiryDto input)
	{
		StudentEnrollmentState studentEnrollmentState = StudentEnrollmentState.Inquiry;
		if (input.FormNumber != null)
		{
			studentEnrollmentState = StudentEnrollmentState.RegistrationForm;
		}
		// use studentEnrollmentManager to create the student enrollment with domain logic
		StudentEnrollment studentEnrollment = await studentEnrollmentManager.CreateStudentInquiryAsync(

			studentEnrollmentState: studentEnrollmentState,
			// Inquiry
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
			addressLine1: input.AddressLine1,
			addressLine2: input.AddressLine2,
			addressLine3: input.AddressLine3,
			country: input.Country,
			city: input.City,
			state: input.State,
			pinCode: input.PinCode,
			inquiryStatusType: FollowUpStatusType.Pending,
			academicYearId: input.AcademicYearId,
			educationBoardId: input.EducationBoardId,
			educationMediumId: input.EducationMediumId,
			standardId: input.StandardId,
			inquiryPreferenceId: input.InquiryPreferenceId
		);

		// Save the student enrollment
		studentEnrollment = await studentEnrollmentRepository.InsertAsync(studentEnrollment);

		// return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}

	#endregion

	#region Update Student Inquiry

	public async Task<StudentEnrollmentDto> UpdateStudentInquiryAsync(Guid studentEnrollmentId, UpdateStudentInquiryDto input)
	{
		StudentEnrollmentState studentEnrollmentState = StudentEnrollmentState.Inquiry;
		if (input.FormNumber != null)
		{
			studentEnrollmentState = StudentEnrollmentState.RegistrationForm;
		}
		// Update student enrollment using StudentEnrollment manager 
		StudentEnrollment studentEnrollment = await studentEnrollmentManager.UpdateStudentInquiryAsync(
			studentEnrollmentId,
			studentEnrollmentState: studentEnrollmentState,
			studentEnrollmentApprovalState: StudentEnrollmentApprovalState.Pending,
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
			addressLine1: input.AddressLine1,
			addressLine2: input.AddressLine2,
			addressLine3: input.AddressLine3,
			country: input.Country,
			city: input.City,
			state: input.State,
			pinCode: input.PinCode,
			inquiryStatusType: input.InquiryStatusType,
			academicYearId: input.AcademicYearId,
			educationBoardId: input.EducationBoardId,
			educationMediumId: input.EducationMediumId,
			standardId: input.StandardId,
			inquiryPreferenceId: input.InquiryPreferenceId
		);
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}

	#endregion

	#region Update Student basic information
	public async Task<StudentEnrollmentDto> UpdateStudentBasicDetails(Guid studentEnrollmentId, CreateUpdateStudentRegistrationDto input)
	{
		StudentEnrollment studentEnrollment = await studentEnrollmentManager.UpdateStudentBasicDetailsByEnrollmentIdAsync(
			studentEnrollmentId,
			formNumber: input.FormNumber,
			firstName: input.FirstName,
			middleName: input.MiddleName,
			lastName: input.LastName,
			previousSchoolName: input.PreviousSchoolName,
			dateOfBirth: input.DateOfBirth,
			genderType: input.GenderType,
			parentContactNumber: input.ParentContactNumber,
			alternativeContactNumber: input.AlternativeContactNumber,
			addressLine1: input.AddressLine1,
			addressLine2: input.AddressLine2,
			addressLine3: input.AddressLine3,
			country: input.Country,
			city: input.City,
			state: input.State,
			pinCode: input.PinCode,
			academicYearId: input.AcademicYearId,
			educationBoardId: input.EducationBoardId,
			educationMediumId: input.EducationMediumId,
			standardId: input.StandardId,
			inquiryPreferenceId: input.InquiryPreferenceId,
			// basic details
			userId: input.UserId,
			profilePicture: input.ProfilePicture,
			email: input.Email,
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

		// Save the employee
		studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

		// Return the DTO
		return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	}

	#endregion

	#region Update Family Information

	public async Task<StudentEnrollmentDto> UpdateFamilyDetails(Guid studentEnrollmentId, UpdateStudentFamilyDetailsDto input)
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

	#region  Student Documents APIs

	public async Task UploadDocumentsAsync(Guid studentEnrollmentId, List<DocumentDto> documents)
	{
		// Map the DTOs to domain entities or value objects
		List<StudentDocumentData> studentDocuments = documents.ConvertAll(doc => new StudentDocumentData
		{
			FileName = doc.FileName,
			ContentType = doc.ContentType,
			Content = doc.Content,
			Size = doc.Size
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
			Id = doc.Id,
			FileName = doc.DocumentName,
			Size = doc.Size,
			ContentType = doc.ContentType
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

	#region Update Student Enrollment Approval State
	//public async Task<StudentEnrollmentDto> UpdateStudentEnrollmentApprovalStateAsync(
	//	Guid studentEnrollmentId,
	//	StudentEnrollmentApprovalState approvalState)
	//{
	//	// Get the student enrollment through the repository
	//	StudentEnrollment studentEnrollment = await studentEnrollmentRepository.GetAsync(studentEnrollmentId);

	//	// Update the approval state using the manager
	//	studentEnrollment = await studentEnrollmentManager.UpdateStudentEnrollmentApprovalStateAsync(
	//		studentEnrollmentId,
	//		approvalState);

	//	// Save the updated entity
	//	studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

	//	// Return the mapped DTO
	//	return ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment);
	//}

	[RemoteService(false)]
	public async Task<List<StudentEnrollmentDto>> UpdateStudentEnrollmentApprovalStateAsync(
		List<Guid> studentEnrollmentIds,
		StudentEnrollmentApprovalState approvalState,
		Guid? academicYearId,
		Guid? educationBoardId,
		Guid? educationMediumId)
	{
		var updatedStudentEnrollmentDtos = new List<StudentEnrollmentDto>();

		foreach (var studentEnrollmentId in studentEnrollmentIds)
		{
			// Get the student enrollment through the repository
			StudentEnrollment studentEnrollment = await studentEnrollmentRepository.GetAsync(studentEnrollmentId);

			if (studentEnrollment != null) // Handle case if studentEnrollment is not found
			{
				// Update the approval state using the manager
				studentEnrollment = await studentEnrollmentManager.UpdateStudentEnrollmentApprovalStateAsync(
					studentEnrollmentId,
					approvalState);

				// Save the updated entity
				studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

				// Map to DTO and add to the list
				updatedStudentEnrollmentDtos.Add(ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment));

				if (approvalState == StudentEnrollmentApprovalState.Approved)
				{
					Guid? educationMediumStageId = null;
					// find 
					if (studentEnrollment.StandardId != default)
					{
						// Standard
						educationMediumStageId = (await standardRepository.GetAsync(studentEnrollment.StandardId)).EducationMediumStageId;
					}
					Student student = await studentEnrollmentManager.ConvertToStudentAsync(studentEnrollment, educationMediumStageId, studentEnrollment.DivisionId, null);
					// update in the db 

					await studentRepository.InsertAsync(student);
				}
				else if (approvalState == StudentEnrollmentApprovalState.Rejected)
				{
					// delete the student
					await studentRepository.DeleteAsync(studentEnrollment.Id);
				}
			}
		}

		// Return the list of mapped DTOs
		return updatedStudentEnrollmentDtos;
	}

	public async Task<List<StudentEnrollmentDto>> UpdateStudentEnrollmentInquiryStateAsync(
		List<Guid> studentEnrollmentIds,
		StudentEnrollmentInquiryState inquiryState)
	{
		var updatedStudentEnrollmentDtos = new List<StudentEnrollmentDto>();

		foreach (var studentEnrollmentId in studentEnrollmentIds)
		{
			// Get the student enrollment through the repository
			StudentEnrollment studentEnrollment = await studentEnrollmentRepository.GetAsync(studentEnrollmentId);

			if (studentEnrollment != null) // Handle case if studentEnrollment is not found
			{
				// Update the approval state using the manager
				studentEnrollment = await studentEnrollmentManager.UpdateStudentEnrollmentInquiryStateAsync(
					studentEnrollmentId,
					inquiryState);

				// Save the updated entity
				studentEnrollment = await studentEnrollmentRepository.UpdateAsync(studentEnrollment);

				// Map to DTO and add to the list
				updatedStudentEnrollmentDtos.Add(ObjectMapper.Map<StudentEnrollment, StudentEnrollmentDto>(studentEnrollment));
			}
		}

		// Return the list of mapped DTOs
		return updatedStudentEnrollmentDtos;
	}
	#endregion

	[RemoteService(false)]
	public async Task<PagedResultDto<StudentEnrollmentDto>> GetStudentsFollowUp(Guid academicYearId)
	{
		List<StudentEnrollment> studentEnrollments =
			await studentEnrollmentRepository.GetListAsync(
				e => e.AcademicYearId == academicYearId,
				includeDetails: true
			);

		// filter out result which has atelast one follow up
		studentEnrollments = studentEnrollments.Where(e => e.FollowUps.Any()).ToList();

		return new PagedResultDto<StudentEnrollmentDto>
		{
			TotalCount = studentEnrollments.Count,
			Items = ObjectMapper.Map<List<StudentEnrollment>, List<StudentEnrollmentDto>>(studentEnrollments)
		};
	}

	public async Task<CustomFileResult> GenerateReportAsync(StudentEnrollmentReportType reportType)
	{
		List<StudentEnrollment>? enrollments = await studentEnrollmentRepository.GetListAsync(includeDetails: true);

		IDocument? reportDocument = StudentManager.CreateReport(enrollments, reportType);

		var pdfBytes = await Task.Run(() =>
		{
			using MemoryStream ms = new MemoryStream();
			reportDocument.GeneratePdf(ms);
			return ms.ToArray();
		});

		// Build a file name based on the report type and current timestamp.
		string fileName = $"StudentEnrollmentReport_{reportType}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

		return new CustomFileResult
		(
			pdfBytes,
			"application/pdf",
			fileName
		);
	}

	public async Task UpdateStudentEnrollmentDivisionAsync(UpdateStudentDivisionDto input)
	{
		if (input.StudentDivisionUpdates == null || input.StudentDivisionUpdates.Count == 0)
		{
			throw new UserFriendlyException("No student data provided.");
		}

		foreach (var update in input.StudentDivisionUpdates)
		{
			// Fetch the student by its ID.
			StudentEnrollment? student = await studentEnrollmentRepository.GetAsync(update.StudentId);

			// Update the student's division and reset the roll number.
			student.DivisionId = update.DivisionId;

			await studentEnrollmentRepository.UpdateAsync(student);
		}
	}
}