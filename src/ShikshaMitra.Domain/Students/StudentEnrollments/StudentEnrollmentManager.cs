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
using ShikshaMitra.Students.StudentRecords;
using System.Linq;


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
		return studentEnrollment ?? throw new BusinessException($"Student Enrollment with ID {enrollmentId} does not exist.");
	}

	// Private helper method to get a user by ID
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

	#region Create Student Inquiry

	public async Task<StudentEnrollment> CreateStudentInquiryAsync(
			StudentEnrollmentState studentEnrollmentState,
			// Inquiry
			string? formNumber,
			string? firstName,
			string? middleName,
			string? lastName,
			string? previousSchoolName,
			DateTimeOffset? dateOfBirth,
			DateTimeOffset? dateOfInquiry,
			GenderType? genderType,
			string? parentContactNumber,
			string? alternativeContactNumber,
			string? addressLine1,
			string? addressLine2,
			string? addressLine3,
			Country? country,
			State? state,
			string? village,
			string? pan,
			string? apparId,
			string? city,
			string? pinCode,
			FollowUpStatusType? inquiryStatusType,
			Guid academicYearId,
			Guid educationBoardId,
			Guid educationMediumId,
			Guid standardId,
			Guid? inquiryPreferenceId
			)
	{
		// Generate Inquiry Number
		string inquiryNumber = await GenerateInquiryNumberAsync();

		int tempGrNo = await GenerateStudentEnrollmentTempGRCodeAsync();

		// create registration entity
		var registration = new StudentEnrollment(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			studentEnrollmentState: studentEnrollmentState,
			studentEnrollmentApprovalState: StudentEnrollmentApprovalState.Pending,
			// Inquiry details
			inquiryNumber: inquiryNumber,
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
			addressLine1: addressLine1,
			addressLine2: addressLine2,
			addressLine3: addressLine3,
			country: country,
			village: village,
			pan: pan,
			apparId: apparId,
			city: city,
			state: state,
			pinCode: pinCode,
			inquiryStatusType: inquiryStatusType,
			academicYearId: academicYearId,
			educationBoardId: educationBoardId,
			educationMediumId: educationMediumId,
			standardId: standardId,
			inquiryPreferenceId: inquiryPreferenceId,
			userId: null,
			profilePicture: null,
			email: null,
			aadhaarNo: null,
			religion: null,
			familyCast: null,
			castType: null,
			motherTongue: null,
			nationality: null,
			birthPlace: null,
			temporaryGr: tempGrNo,
			remarks: null,
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
			motherDetails: new StudentContactDetails(
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
			guardianDetails: new StudentContactDetails(
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
				localAddressLine1: null,
				localAddressLine2: null,
				localAddressLine3: null,
				localCountryName: null,
				localCountryIsoCode: null,
				localStateName: null,
				localStateIsoCode: null,
				localCity: null,
				localPinCode: null,
				permanentAddressLine1: null,
				permanentAddressLine2: null,
				permanentAddressLine3: null,
				permanentCountryName: null,
				permanentCountryIsoCode: null,
				permanentStateName: null,
				permanentStateIsoCode: null,
				permanentCity: null,
				permanentPinCode: null,
				emergencyAddressLine1: null,
				emergencyAddressLine2: null,
				emergencyAddressLine3: null,
				emergencyCountry: null,
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
				pulseRate: null,
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

	#region Create Student Registration

	public async Task<StudentEnrollment> CreateStudentRegistrationAsync(
			string? formNumber,
			string? firstName,
			string? middleName,
			string? lastName,
			string? previousSchoolName,
			DateTimeOffset? dateOfBirth,
			GenderType? genderType,
			string? parentContactNumber,
			string? alternativeContactNumber,
			string? addressLine1,
			string? addressLine2,
			string? addressLine3,
			Country? country,
			string? village,
			string? pan,
			string? apparId,
			string? city,
			State? state,
			string? pinCode,
			Guid academicYearId,
			Guid educationBoardId,
			Guid educationMediumId,
			Guid standardId,
			Guid? inquiryPreferenceId,
			// basic details
			Guid? userId,
			byte[]? profilePicture,
			string? email,
			string? aadhaarNo,
			string? religion,
			string? familyCast,
			CastType? castType,
			string? motherTongue,
			string? nationality,
			string? birthPlace,
			int? temporaryGr,
			string? remarks
			)
	{
		// Generate Inquiry Number
		string inquiryNumber = await GenerateInquiryNumberAsync();

		int tempGrNo = await GenerateStudentEnrollmentTempGRCodeAsync();
		// create registration entity
		var registration = new StudentEnrollment(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			studentEnrollmentState: StudentEnrollmentState.Registration,
			studentEnrollmentApprovalState: StudentEnrollmentApprovalState.Pending,
			// Inquiry details
			inquiryNumber: inquiryNumber,
			formNumber: formNumber,
			firstName: firstName,
			middleName: middleName,
			lastName: lastName,
			previousSchoolName: previousSchoolName,
			dateOfBirth: dateOfBirth,
			dateOfInquiry: null,
			genderType: genderType,
			parentContactNumber: parentContactNumber,
			alternativeContactNumber: alternativeContactNumber,
			addressLine1: addressLine1,
			addressLine2: addressLine2,
			addressLine3: addressLine3,
			country: country,
			state: state,
			village: village,
			pan: pan,
			apparId: apparId,
			city: city,
			pinCode: pinCode,
			inquiryStatusType: null,
			academicYearId: academicYearId,
			educationBoardId: educationBoardId,
			educationMediumId: educationMediumId,
			standardId: standardId,
			inquiryPreferenceId: inquiryPreferenceId,
			// basic details
			userId: userId,
			profilePicture: profilePicture,
			email: email,
			aadhaarNo: aadhaarNo,
			religion: religion,
			familyCast: familyCast,
			castType: castType,
			motherTongue: motherTongue,
			nationality: nationality,
			birthPlace: birthPlace,
			temporaryGr: tempGrNo,
			remarks: remarks,
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
			motherDetails: new StudentContactDetails(
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
			guardianDetails: new StudentContactDetails(
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
				localAddressLine1: null,
				localAddressLine2: null,
				localAddressLine3: null,
				localCountryName: null,
				localCountryIsoCode: null,
				localStateIsoCode: null,
				localStateName: null,
				localCity: null,
				localPinCode: null,
				permanentAddressLine1: null,
				permanentAddressLine2: null,
				permanentAddressLine3: null,
				permanentCountryName: null,
				permanentCountryIsoCode: null,
				permanentStateIsoCode: null,
				permanentStateName: null,
				permanentCity: null,
				permanentPinCode: null,
				emergencyAddressLine1: null,
				emergencyAddressLine2: null,
				emergencyAddressLine3: null,
				emergencyCountry: null,
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
				pulseRate: null,
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

	#region Update Student Inquiry

	public async Task<StudentEnrollment> UpdateStudentInquiryAsync
	(
		Guid studentEnrollmentId,
		StudentEnrollmentState studentEnrollmentState,
		StudentEnrollmentApprovalState studentEnrollmentApprovalState,
		// Inquiry
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset? dateOfBirth,
		DateTimeOffset? dateOfInquiry,
		GenderType? genderType,
		string? parentContactNumber,
		string? alternativeContactNumber,
		string? addressLine1,
		string? addressLine2,
		string? addressLine3,
		Country? country,
		string? village,
		string? pan,
		string? apparId,
		string? city,
		State? state,
		string? pinCode,
		FollowUpStatusType? inquiryStatusType,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid? inquiryPreferenceId
	)
	{
		// Get the student enrollment details
		StudentEnrollment studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the student inquiry details
		studentEnrollment.UpdateStudentInquiry(
			studentEnrollmentState: studentEnrollmentState,
			studentEnrollmentApprovalState: studentEnrollmentApprovalState,
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
			addressLine1: addressLine1,
			addressLine2: addressLine2,
			addressLine3: addressLine3,
			country: country,
			village: village,
			pan: pan,
			apparId: apparId,
			city: city,
			state: state,
			pinCode: pinCode,
			inquiryStatusType: inquiryStatusType,
			academicYearId: academicYearId,
			educationBoardId: educationBoardId,
			educationMediumId: educationMediumId,
			standardId: standardId,
			inquiryPreferenceId: inquiryPreferenceId
		);

		// Validate the updated student enrollment if needed
		ValidateStudentEnrollment(studentEnrollment);
		return studentEnrollment;
	}
	#endregion

	#region Update Student Basic details


	public async Task<StudentEnrollment> UpdateStudentBasicDetailsByEnrollmentIdAsync(
		Guid studentEnrollmentId,
		string? formNumber,
		string? firstName,
		string? middleName,
		string? lastName,
		string? previousSchoolName,
		DateTimeOffset? dateOfBirth,
		GenderType? genderType,
		string? parentContactNumber,
		string? alternativeContactNumber,
		string? addressLine1,
		string? addressLine2,
		string? addressLine3,
		Country? country,
		string? village,
		string? pan,
		string? apparId,
		string? city,
		State? state,
		string? pinCode,
		Guid academicYearId,
		Guid educationBoardId,
		Guid educationMediumId,
		Guid standardId,
		Guid? inquiryPreferenceId,
		// basic details
		Guid? userId,
		byte[]? profilePicture,
		string? email,
		string? aadhaarNo,
		string? religion,
		string? familyCast,
		CastType? castType,
		string? motherTongue,
		string? nationality,
		string? birthPlace,
		int temporaryGr,
		string? remarks
	)
	{
		// Validate user existence 
		//await GetUserAsync((Guid) userId);
		// Get the Registration details
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);
		// Update the registration details
		studentEnrollment.UpdateStudentBasicDetails(
			formNumber: formNumber,
			firstName: firstName,
			middleName: middleName,
			lastName: lastName,
			previousSchoolName: previousSchoolName,
			dateOfBirth: dateOfBirth,
			genderType: genderType,
			parentContactNumber: parentContactNumber,
			alternativeContactNumber: alternativeContactNumber,
			addressLine1: addressLine1,
			addressLine2: addressLine2,
			addressLine3: addressLine3,
			country: country,
					village: village,
			pan: pan,
			apparId: apparId,
			city: city,
			state: state,
			pinCode: pinCode,
			academicYearId: academicYearId,
			educationBoardId: educationBoardId,
			educationMediumId: educationMediumId,
			standardId: standardId,
			inquiryPreferenceId: inquiryPreferenceId,
			userId: userId,
			profilePicture: profilePicture,
			email: email,
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
		return studentEnrollment;
	}
	#endregion

	#region Update Family details

	public async Task<StudentEnrollment> UpdateFamilyDetailsByStudentEnrollmentIdAsync(

		Guid studentEnrollmentId,
		StudentContactDetails fatherDetails,
		StudentContactDetails motherDetails,
		StudentContactDetails guardianDetails
	)
	{
		// Get the Registration details
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the Family details
		studentEnrollment.UpdateFamilyDetails(fatherDetails, motherDetails, guardianDetails);
		return studentEnrollment;
	}

	#endregion

	#region Update Communication details

	public async Task<StudentEnrollment> UpdateCommunicationDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		StudentCommunicationDetails studentCommunicationDetails
	)
	{
		// Get the Registration details
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the Communication details
		studentEnrollment.UpdateCommunicationDetails(studentCommunicationDetails);
		return studentEnrollment;
	}

	#endregion

	#region Update Additional details

	public async Task<StudentEnrollment> UpdateAdditionalDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		StudentAdditionalDetails studentAdditionalDetails
	)
	{
		// Get the Registration details
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the Communication details
		studentEnrollment.UpdateAdditionalDetails(studentAdditionalDetails);
		return studentEnrollment;
	}

	#endregion

	#region Update Medical details

	public async Task<StudentEnrollment> UpdateMedicalDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		StudentMedicalRecords studentMedicalRecords
	)
	{
		// Get the Registration details
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the Communication details
		studentEnrollment.UpdateMedicalReports(studentMedicalRecords);
		return studentEnrollment;
	}

	#endregion

	#region Update Previous School details

	public async Task<StudentEnrollment> UpdatePreviousSchoolDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		PreviousSchoolDetails previousSchoolDetails
	)
	{
		// Get the Registration details
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update Previous School Details
		studentEnrollment.UpdatePreviousSchoolDetails(previousSchoolDetails);
		return studentEnrollment;
	}

	#endregion

	#region Update Parent Bank details

	public async Task<StudentEnrollment> UpdateParentBankDetailsByStudentEnrollmentIdAsync(
		Guid studentEnrollmentId,
		ParentBankDetails parentBankDetails
	)
	{
		// Get the Registration details
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

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
				studentEnrollmentId: registrationId,
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
			await studentDocumentRepository.FirstOrDefaultAsync(d => d.StudentEnrollmentId == studentEnrollmentId && d.Id == documentId);
		return document ?? throw new BusinessException($"Document with ID {documentId} does not exist.");
	}
	#endregion

	#region Get all Documents

	public async Task<List<StudentDocument>> GetDocumentsAsync(Guid studentEnrollmentId)
	{
		List<StudentDocument>
			documents = await studentDocumentRepository.GetListAsync(d => d.StudentEnrollmentId == studentEnrollmentId);
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

	#region Move to Registration

	public async Task<StudentEnrollment> MoveToRegistrationAsync(Guid studentEnrollmentId)
	{
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the employee status
		studentEnrollment = studentEnrollment.UpdateStudentEnrollmentState(StudentEnrollmentState.Registration);
		return studentEnrollment;
	}


	#endregion

	#region Move to Admission

	public async Task<StudentEnrollment> MoveToAdmissionAsync(Guid studentEnrollmentId)
	{
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

		// Update the employee status
		studentEnrollment = studentEnrollment.UpdateStudentEnrollmentState(StudentEnrollmentState.Admission);
		return studentEnrollment;
	}


	#endregion

	#region Move to Admission Fess

	//public async Task<StudentEnrollment> MoveToAdmissionFeesAsync(Guid studentEnrollmentId)
	//{
	//	var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);

	//	// Update the employee status
	//	studentEnrollment = studentEnrollment.UpdateStudentEnrollmentState(StudentEnrollmentState.AdmissionFees);
	//	return studentEnrollment;
	//}

	public async Task<StudentEnrollment> UpdateStudentEnrollmentApprovalStateAsync(Guid studentEnrollmentId, StudentEnrollmentApprovalState approvalState)
	{
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);
		// Update the employee status
		studentEnrollment = studentEnrollment.UpdateStudentEnrollmentApprovalState(approvalState);

		// update in the database
		await studentEnrollmentRepository.UpdateAsync(studentEnrollment);
		return studentEnrollment;
	}

	public async Task<StudentEnrollment> UpdateStudentEnrollmentInquiryStateAsync(Guid studentEnrollmentId, StudentEnrollmentInquiryState approvalState)
	{
		var studentEnrollment = await GetStudentEnrollmentAsync(studentEnrollmentId);
		// Update the employee status
		studentEnrollment = studentEnrollment.UpdateStudentEnrollmentInquiryState(approvalState);

		// update in the database
		await studentEnrollmentRepository.UpdateAsync(studentEnrollment);
		return studentEnrollment;
	}
	#endregion

	public async Task<Student> ConvertToStudentAsync(
			StudentEnrollment enrollment,
			Guid? educationMediumStageId,
			Guid? divisionId,
			int? rollNo = null)
	{
		// Create full name from components
		string fullName = string.Join(" ", new[] {
				enrollment.FirstName,
				enrollment.MiddleName,
				enrollment.LastName
			}.Where(n => !string.IsNullOrWhiteSpace(n)));

		// Create new Student entity with all fields from enrollment
		var student = new Student(
			id: enrollment.Id,
			tenantId: enrollment.TenantId,
			formNumber: enrollment.FormNumber,
			firstName: enrollment.FirstName,
			middleName: enrollment.MiddleName,
			lastName: enrollment.LastName,
			dateOfBirth: enrollment.DateOfBirth,
			genderType: enrollment.GenderType,
			email: enrollment.Email,
			parentContactNumber: enrollment.ParentContactNumber,
			alternativeContactNumber: enrollment.AlternativeContactNumber,
			profilePicture: enrollment.ProfilePicture,

			// Address
			addressLine1: enrollment.AddressLine1,
			addressLine2: enrollment.AddressLine2,
			addressLine3: enrollment.AddressLine3,
			country: enrollment.Country,
			state: enrollment.State,
			city: enrollment.City,
			pinCode: enrollment.PinCode,

			// Personal Details
			aadhaarNo: enrollment.AadhaarNo,
			religion: enrollment.Religion,
			familyCast: enrollment.FamilyCast,
			castType: enrollment.CastType,
			motherTongue: enrollment.MotherTongue,
			nationality: enrollment.Nationality,
			birthPlace: enrollment.BirthPlace,
			remarks: enrollment.Remarks,
			userId: enrollment.UserId,

			// Academic Details
			academicYearId: enrollment.AcademicYearId,
			educationBoardId: enrollment.EducationBoardId,
			educationMediumId: enrollment.EducationMediumId,
			educationMediumStageId: educationMediumStageId,
			standardId: enrollment.StandardId,
			divisionId: divisionId,
			tempGrNo: enrollment.TemporaryGr,
			grNoRecord: null,
			rollNo: rollNo,

			// Family Details
			fatherDetails: enrollment.FatherDetails,
			motherDetails: enrollment.MotherDetails,
			guardianDetails: enrollment.GuardianDetails,

			// Additional Details
			studentCommunicationDetails: enrollment.StudentCommunicationDetails,
			studentAdditionalDetails: enrollment.StudentAdditionalDetails,
			studentMedicalRecords: enrollment.StudentMedicalRecords,
			previousSchoolDetails: enrollment.PreviousSchoolDetails,
			parentBankDetails: enrollment.ParentBankDetails,
			documents: new List<StudentDocument>(enrollment.Documents) // Create a new list with the same documents
		);


		return student;
	}

	private async Task<int> GenerateStudentEnrollmentTempGRCodeAsync()
	{
		// Get the last EmployeeCode
		StudentEnrollment? lastEnrolledStudent = await studentEnrollmentRepository.GetLastStudentEnrollmentAsync();

		int nextNumber = 1;

		if (lastEnrolledStudent != null)
		{
			var lastNumber = lastEnrolledStudent.TemporaryGr;

			nextNumber = lastNumber + 1;
		}

		return nextNumber;
	}
}
