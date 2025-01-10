using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using System;
using System.Collections.Generic;
using Volo.Abp.BlobStoring;
using Volo.Abp.Guids;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;


namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollmentManager(
	IStudentEnrollmentRepository studentEnrollmentRepository,
	IIdentityUserRepository userRepository,
	IIdentityRoleRepository roleRepository,
	IGuidGenerator guidGenerator,
	IBlobContainer blobContainer,
	IStudentDocumentRepository studentDocumentRepository
	) : DomainService
{
	private async Task<StudentEnrollment> GetStudentEnrollmentAsync(Guid enrollmentId)
	{
		var studentEnrollment = await studentEnrollmentRepository.GetAsync(enrollmentId);
		return studentEnrollment ?? throw new BusinessException($"Registration with ID {enrollmentId} does not exist.");
	}

	private async Task<IdentityUser> GetUserAsync(Guid userId)
	{
		var user = await userRepository.GetAsync(userId);
		return user ?? throw new BusinessException($"User with ID {userId} does not exist.");
	}

	// Private helper method to get a role by ID
	private async Task<IdentityRole> GetRoleAsync(Guid roleId)
	{
		var role = await roleRepository.GetAsync(roleId);
		return role ?? throw new BusinessException($"Role with ID {roleId} does not exist.");
	}
	
	#region Create Student Enrollment basic information
	
	public async Task<StudentEnrollment> CreateStudentEnrollmentAsync(
			// Guid userId,
			// Guid roleId,
			StudentEnrollmentState studentEnrollmentState,
			// Inquiry
			string? formNumber,
			string? firstName,
			string? middleName,
			string? lastName,
			string? previousSchoolName,
			DateTimeOffset dateOfBirth,
			DateTimeOffset dateOfInquiry,
			GenderType genderType,
			string? parentContactNumber,
			string? alternativeContactNumber,
			string? city,
			string? state,
			int? pinCode,
			string? address,
			InquiryStatusType? inquiryStatusType,
			Guid academicYearId,
			Guid educationBoardId,
			Guid educationMediumId,
			Guid standardId,
			Guid inquiryPreferenceId,

			// registration
			byte[]? profilePicture,
			string? aadhaarNo,
			string? religion,
			string? familyCast,
			CastType? castType,
			string? motherTongue,
			string? nationality,
			string? birthPlace,
			string? temporaryGr,
			string? remarks
			)
		{
			// Validate user existence
			// await GetUserAsync(userId);

			// Validate role existence
			// await GetRoleAsync(roleId);

			// Generate Inquiry Number
			string inquiryNumber = await GenerateInquiryNumberAsync();
			
			
			Console.WriteLine($"Generate Inquiry Number ++++++++++++++++++: {inquiryNumber}");

			string registrationCode = "test101";
			
			// create registration entity
			var registration = new StudentEnrollment(
				id: guidGenerator.Create(),
				tenantId: CurrentTenant.Id,
				// userId: userId,
				// roleId: roleId,
				studentEnrollmentState : studentEnrollmentState,
				// Inquiry details
				inquiryNumber: inquiryNumber,
				registrationCode:registrationCode,
				formNumber: formNumber,
				firstName: firstName,
				middleName: middleName,
				lastName: lastName,
				previousSchoolName: previousSchoolName,
				dateOfBirth: dateOfBirth,
				dateOfInquiry: dateOfInquiry,
				genderType: genderType,
				parentContactNumber: parentContactNumber,
				alternativeContactNumber: alternativeContactNumber,
				city: city,
				state: state,
				pinCode: pinCode,
				address: address,
				inquiryStatusType: inquiryStatusType,
				academicYearId: academicYearId,
				educationBoardId: educationBoardId,
				educationMediumId: educationMediumId,
				standardId: standardId,
				inquiryPreferenceId: inquiryPreferenceId,
				// registration
				profilePicture: profilePicture,
				aadhaarNo : aadhaarNo,
				religion : religion,
				familyCast : familyCast,
				castType : castType,
				motherTongue:motherTongue,
				nationality : nationality,
				birthPlace : birthPlace,
				temporaryGr : temporaryGr,
				remarks : remarks,
				fatherDetails: new StudentContactDetails(
					name: null,
					dateOfBirth: null,
					occupation: null,
					qualification: null,
					contactNo: null,
					email: null,
					passOutYear: null,
					officeNo: null,
					officeAddress: null,
					designation: null,
					organisation: null,
					aadhaarNo: null,
					remarks: null,
					address: null
				),
				motherDetails:  new StudentContactDetails(
					name: null,
					dateOfBirth: null,
					occupation: null,
					qualification: null,
					contactNo: null,
					email: null,
					passOutYear: null,
					officeNo: null,
					officeAddress: null,
					designation: null,
					organisation: null,
					aadhaarNo: null,
					remarks: null,
					address: null
				),
				guardianDetails:   new StudentContactDetails(
					name: null,
					dateOfBirth: null,
					occupation: null,
					qualification: null,
					contactNo: null,
					email: null,
					passOutYear: null,
					officeNo: null,
					officeAddress: null,
					designation: null,
					organisation: null,
					aadhaarNo: null,
					remarks: null,
					address: null
				),
				studentCommunicationDetails: new StudentCommunicationDetails(
					primaryContactNo: null,
					parentContactNo: null,
					parentEmail: null,
					studentContactNo: null,
					studentEmail: null,
					whatsAppNo: null,
					localAddress: null,
					localState: null,
					localCity: null,
					localPinCode:null,
					permanentAddress: null,
					permanentState: null,
					permanentCity: null,
					permanentPinCode: null,
					emergencyAddress: null,
					emergencyState: null,
					emergencyCity: null,
					emergencyPinCode: null,
					remarks: null
				),
				studentAdditionalDetails: new StudentAdditionalDetails(
					category: null,
					rightToEducation: null,
					bPLStudent: null,
					bPLCardNo: null,
					personWithDisability: null,
					secondLanguage: null,
					emergencyContactNo: null,
					singleParentChild: null,
					singleChild: null,
					sponsorName: null
					),
				studentMedicalRecords: new StudentMedicalRecords(
					weight: null,
					height: null,
					bodyMassIndex: null,
					haemoglobin: null,
					allergies: null,
					covidVaccination: null,
					childImmunisation: null,
					immunisationRemarks: null
					),
				previousSchoolDetails: new PreviousSchoolDetails(
					previousSchoolName: null,
					previousSchoolAddress: null,
					previousSchoolBoard: null,
					previousSchoolMedium: null,
					tcNo: null,
					lastClassPassed: null,
					percentage: null
				),
				parentBankDetails: new ParentBankDetails(
					bankName: null,
					branchName: null,
					accountHolderName: null,
					accountNumber: null,
					ifscCode: null
					)
			);
			return registration;
		}
	#endregion

	#region Update General Information

	public async Task<StudentEnrollment> UpdateStudentEnrollmentAsync
	(
		Guid studentEnrollmentId,
		Guid roleId,
		StudentEnrollmentState studentEnrollmentState,
		// Inquiry
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset dateOfBirth,
		DateTimeOffset dateOfInquiry,
		GenderType genderType,
		string? parentContactNumber,
		string? alternativeContactNumber,
		string? city,
		string? state,
		int? pinCode,
		string? address,
		InquiryStatusType? inquiryStatusType,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid inquiryPreferenceId,

		// registration
		byte[]? profilePicture,
		string? aadhaarNo,
		string? religion,
		string? familyCast,
		CastType? castType,
		string? motherTongue,
		string? nationality,
		string? birthPlace,
		string? temporaryGr,
		string? remarks
	)
	{
		// Get the student enrollment details
		StudentEnrollment studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the general details
		studentEnrollment.UpdateGeneralInfo(
			roleId: roleId,
			studentEnrollmentState: studentEnrollmentState,
			formNumber: formNumber,
			firstName: firstName,
			middleName: middleName,
			lastName: lastName,
			previousSchoolName: previousSchoolName,
			dateOfBirth: dateOfBirth,
			dateOfInquiry: dateOfInquiry,
			genderType: genderType,
			parentContactNumber: parentContactNumber,
			alternativeContactNumber: alternativeContactNumber,
			city: city,
			state: state,
			pinCode: pinCode,
			address: address,
			inquiryStatusType: inquiryStatusType,
			academicYearId: academicYearId,
			educationBoardId: educationBoardId,
			educationMediumId: educationMediumId,
			standardId: standardId,
			inquiryPreferenceId: inquiryPreferenceId,
			
			// registration
			profilePicture: profilePicture,
			aadhaarNo: aadhaarNo,
			religion: religion,
			familyCast: familyCast,
			castType: castType,
			motherTongue: motherTongue,
			nationality: nationality,
			birthPlace: birthPlace,
			temporaryGr: temporaryGr,
			remarks: remarks
		);

		// Validate the updated student enrollment if needed
		ValidateStudentEnrollment(studentEnrollment);
		return studentEnrollment;
	}
	#endregion

	#region Update Family Information

	public async Task<StudentEnrollment> UpdateFamilyDetailsByStudentEnrollmentIdAsync(

		Guid studentEnrollmentId,
		StudentContactDetails fatherDetails,
		StudentContactDetails motherDetails,
		StudentContactDetails guardianDetails
	)
	{
		// Get the Registration details
		var studentEnrollment =  await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the Family information
		studentEnrollment.UpdateFamilyDetails(fatherDetails, motherDetails, guardianDetails);
		return studentEnrollment;
	}

	#endregion

	#region Update Communication Information

	public async Task<StudentEnrollment> UpdateCommunicationDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		StudentCommunicationDetails studentCommunicationDetails
	)
	{
		// Get the Registration details
		var studentEnrollment =  await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the Communication details
		studentEnrollment.UpdateCommunicationDetails(studentCommunicationDetails);
		return studentEnrollment;
	}

	#endregion

	#region Update Additional Information

	public async Task<StudentEnrollment> UpdateAdditionalDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		StudentAdditionalDetails studentAdditionalDetails
	)
	{
		// Get the Registration details
		var studentEnrollment =  await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the Communication details
		studentEnrollment.UpdateAdditionalDetails(studentAdditionalDetails);
		return studentEnrollment;
	}

	#endregion

	#region Update Medical Information

	public async Task<StudentEnrollment> UpdateMedicalDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		StudentMedicalRecords studentMedicalRecords
	)
	{
		// Get the Registration details
		var studentEnrollment =  await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the Communication details
		studentEnrollment.UpdateMedicalReports(studentMedicalRecords);
		return studentEnrollment;
	}

	#endregion

	#region Update Previous School Information

	public async Task<StudentEnrollment> UpdatePreviousSchoolDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		PreviousSchoolDetails previousSchoolDetails
	)
	{
		// Get the Registration details
		var studentEnrollment =  await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update Previous School Details
		studentEnrollment.UpdatePreviousSchoolDetails(previousSchoolDetails);
		return studentEnrollment;
	}

	#endregion

	#region Update Parent Bank Information

	public async Task<StudentEnrollment> UpdateParentBankDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		ParentBankDetails parentBankDetails
	)
	{
		// Get the Registration details
		var studentEnrollment =  await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update Parent Bank Details
		studentEnrollment.UpdateParenBankDetails(parentBankDetails);
		return studentEnrollment;
	}

	#endregion

	#region Add documents
	
	public async Task AddDocumentsAsync(Guid registrationId, List<StudentDocumentData> documents)
	{
		var studentEnrollment = await studentEnrollmentRepository.GetAsync(registrationId);

		foreach (var docData in documents)
		{
			var documentId = GuidGenerator.Create();
			var blobName = $"{documentId}_{docData.FileName}";

			// Save the file content to blob storage
			await blobContainer.SaveAsync(blobName, docData.Content, overrideExisting: true);

			// Create and save the StudentDocument entity (metadata)
			var document = new StudentDocument(
				id: documentId,
				tenantId: CurrentTenant.Id,
				studentId: registrationId,
				documentName: docData.FileName,
				contentType: docData.ContentType,
				size: docData.Size,
				blobName: blobName);

			await studentDocumentRepository.InsertAsync(document);
			studentEnrollment.AddDocument(document);
		}

		// Update the registration entity
		await studentEnrollmentRepository.UpdateAsync(studentEnrollment);
	}

	#endregion

	#region Get Document
	public async Task<StudentDocument> GetDocumentAsync(Guid studentEnrollmentId, Guid documentId)
	{
		var document =
			await studentDocumentRepository.FirstOrDefaultAsync(d => d.StudentId == studentEnrollmentId && d.Id == documentId);
		return document ?? throw new BusinessException($"Document with ID {documentId} does not exist.");
	}
	#endregion

	#region Get all Documents

	public async Task<List<StudentDocument>> GetDocumentsAsync(Guid studentEnrollmentId)
	{
		List<StudentDocument>
			documents = await studentDocumentRepository.GetListAsync(d => d.StudentId == studentEnrollmentId);
		return documents;
	}

	#endregion

	#region Update Document
	
	public async Task<StudentDocument> UpdateDocumentAsync(
		Guid studentId,
		Guid documentId,
		string fileName,
		string contentType,
		long size,
		byte[] content
	)
	{
		StudentDocument document = await GetDocumentAsync(studentId, documentId);

		// Update blob
		await blobContainer.SaveAsync(document.BlobName, content, overrideExisting: true);

		// Update metadata
		document.UpdateStudentDocumentMetadata(fileName, contentType, size);

		await studentDocumentRepository.UpdateAsync(document);
		return document;
	}

	#endregion

	#region Student Enrollment Vallidations
	private static void ValidateStudentEnrollment(StudentEnrollment studentEnrollment)
	{
		// Add any domain-specific validation rules here

		// Example validations:
		// Example validations:
		if (studentEnrollment.DateOfBirth.HasValue && DateOnly.FromDateTime(studentEnrollment.DateOfBirth.Value.Date) >= DateOnly.FromDateTime(DateTime.UtcNow))
		{
			throw new BusinessException("Date of birth cannot be in the future");
		}
		// Add more validations as needed
	}

	#endregion

	#region Genrate Inquiry Number
	private async Task<string> GenerateInquiryNumberAsync()
	{
		// Get the last Student Inquiry
		StudentEnrollment? lastInquiry = await studentEnrollmentRepository.GetLastStudentInquiryAsync();

		
		Console.WriteLine($"lastInquiry ++++++++++++++++++: {lastInquiry}");
		int nextNumber = 1;

		if (lastInquiry != null)
		{
			var lastCode = lastInquiry.InquiryNumber;

			// Extract the numeric part from the last code (e.g., "INQ001" -> 1)
			if (int.TryParse(lastCode[3..], out int lastNumber))
			{
				nextNumber = lastNumber + 1;
			}
		}

		// Format the new InquiryNumber as "INQ" followed by a 3-digit number with leading zeros
		var newInquiryNumber = $"INQ{nextNumber:D3}";

		return newInquiryNumber;
	}
	#endregion

}