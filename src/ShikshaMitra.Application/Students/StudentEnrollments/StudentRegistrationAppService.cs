using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments;

[RemoteService(false)]
public class StudentRegistrationAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	IRepository<StudentFamilyDetail, Guid> studentFamilyDetailRepository,
	IRepository<StudentCommunicationDetail, Guid> studentCommunicationDetailRepository,
	IRepository<StudentMedicalRecord, Guid> studentMedicalRecordRepository,
	IRepository<StudentPreviousSchoolDetail, Guid> studentPreviousSchoolDetailRepository,
	IRepository<StudentBankDetail, Guid> studentBankDetailRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	StudentEnrollmentDtoEnricher dtoEnricher
) : ApplicationService, IStudentRegistrationAppService
{
	public async Task<PagedResultDto<StudentRegistrationFormDto>> GetRegistrationFormsByMediumAsync(
		Guid academicMediumId, 
		int skipCount = 0, 
		int maxResultCount = 10, 
		string? searchTerm = null)
	{
		await CheckAcademicMediumExistsAsync(academicMediumId);

		// Get queryable with aggregate parts auto-included
		var queryable = await studentRepository.GetQueryableAsync();
		
		var query = queryable
			.Where(s => s.Status == StudentLifeCycleStatusEnum.RegistrationFormIssued
				&& s.EducationMediumId == academicMediumId
				&& s.TenantId == CurrentTenant.Id);

		// Implement search functionality
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var normalizedSearchTerm = searchTerm.Trim().ToLower();
			query = query.Where(x =>
				(x.FirstName != null && x.FirstName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.AdditionalDetail != null && x.AdditionalDetail.RegistrationFormNo != null && x.AdditionalDetail.RegistrationFormNo.ToLower().Contains(normalizedSearchTerm)) ||
				(x.AdditionalDetail != null && x.AdditionalDetail.InquiryNo != null && x.AdditionalDetail.InquiryNo.ToLower().Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.PrimaryContactNo != null && x.ContactDetail.PrimaryContactNo.Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.Email != null && x.ContactDetail.Email.ToLower().Contains(normalizedSearchTerm))
			);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		
		if (totalCount == 0)
		{
			return new PagedResultDto<StudentRegistrationFormDto>(0, new List<StudentRegistrationFormDto>());
		}

		var pagedQuery = query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skipCount)
			.Take(maxResultCount);

		var results = await AsyncExecuter.ToListAsync(pagedQuery);
		var resultList = ObjectMapper.Map<List<Student>, List<StudentRegistrationFormDto>>(results);

		// Enrich DTOs with related entity names (Standard, Medium, Board, Year)
		await dtoEnricher.EnrichRegistrationFormDtoListAsync(resultList, results);

		return new PagedResultDto<StudentRegistrationFormDto>(totalCount, resultList);
	}

	public async Task<PagedResultDto<StudentRegistrationDto>> GetRegistrationsByMediumAsync(
		Guid academicMediumId, 
		int skipCount = 0, 
		int maxResultCount = 10, 
		string? searchTerm = null)
	{
		await CheckAcademicMediumExistsAsync(academicMediumId);

		// Get queryable with aggregate parts auto-included
		var queryable = await studentRepository.GetQueryableAsync();
		
		var query = queryable
			.Where(s => s.Status == StudentLifeCycleStatusEnum.Registation
				&& s.EducationMediumId == academicMediumId
				&& s.TenantId == CurrentTenant.Id);

		// Implement search functionality
		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var normalizedSearchTerm = searchTerm.Trim().ToLower();
			query = query.Where(x =>
				(x.FirstName != null && x.FirstName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(normalizedSearchTerm)) ||
				(x.AdditionalDetail != null && x.AdditionalDetail.RegistrationFormNo != null && x.AdditionalDetail.RegistrationFormNo.ToLower().Contains(normalizedSearchTerm)) ||
				(x.AdditionalDetail != null && x.AdditionalDetail.InquiryNo != null && x.AdditionalDetail.InquiryNo.ToLower().Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.PrimaryContactNo != null && x.ContactDetail.PrimaryContactNo.Contains(normalizedSearchTerm)) ||
				(x.ContactDetail != null && x.ContactDetail.Email != null && x.ContactDetail.Email.ToLower().Contains(normalizedSearchTerm))
			);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		
		if (totalCount == 0)
		{
			return new PagedResultDto<StudentRegistrationDto>(0, new List<StudentRegistrationDto>());
		}

		var pagedQuery = query
			.OrderByDescending(x => x.CreationTime)
			.Skip(skipCount)
			.Take(maxResultCount);

		var results = await AsyncExecuter.ToListAsync(pagedQuery);
		var resultList = ObjectMapper.Map<List<Student>, List<StudentRegistrationDto>>(results);

		// Enrich DTOs with related entity names (Standard, Medium, Board, Year)
		await dtoEnricher.EnrichRegistrationDtoListAsync(resultList, results);

		return new PagedResultDto<StudentRegistrationDto>(totalCount, resultList);
	}

	public async Task<UpdateGeneralInfoOutputDto> UpdateGeneralInfoAsync(Guid studentId, UpdateGeneralInfoDto input)
	{
		// Load the student aggregate with all parts
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status to move to Registration
		// Allow from RegistrationFormIssued OR if already in Registration (for updates)
		if (student.Status != StudentLifeCycleStatusEnum.RegistrationFormIssued && 
		    student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in RegistrationFormIssued or Registration status to update general information. Current status: {student.Status}"
			);
		}

		// Update the student's general information (root entity)
		student.UpdateGeneralInformation(
			input.FirstName,
			input.MiddleName,
			input.LastName
		);

		// Update additional details
		if (student.AdditionalDetail != null)
		{
			student.AdditionalDetail.UpdateGeneralInformation(
				input.DateOfBirth,
				input.Gender,
				input.ProfilePictureUrl,
				input.AadharNo,
				input.ApparId,
				input.PanNo,
				input.Religion,
				input.FamilyCast,
				input.CastCategory,
				input.Nationality,
				input.BirthPlace,
				input.MotherTongue,
				input.PreviousSchool,
				input.LastClassPassed,
				StudentRegistrationStateEnum.General
			);
		}

		// Update contact details
		if (student.ContactDetail != null)
		{
			student.ContactDetail.UpdateContactInfo(
				input.PrimaryContactNo,
				input.SecondaryContactNo,
				input.Email,
				input.Address1,
				input.Address2,
				input.Address3,
				input.City,
				input.State,
				input.Country,
				input.Pincode
			);
		}

		// Transition to Registration status (if not already there)
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			student.ChangeStatus(StudentLifeCycleStatusEnum.Registation);
		}

		// Save the updated aggregate
		await studentRepository.UpdateAsync(student, autoSave: true);

		// Return output DTO
		return new UpdateGeneralInfoOutputDto
		{
			StudentId = student.Id,
			Status = student.Status,
			FirstName = student.FirstName,
			MiddleName = student.MiddleName,
			LastName = student.LastName,
			DateOfBirth = student.AdditionalDetail?.DateOfBirthday,
			Gender = student.AdditionalDetail?.Gender
		};
	}

	public async Task<UpdateGeneralInfoOutputDto> CreateDirectRegistrationAsync(CreateDirectRegistrationDto input)
	{
		// Validate academic medium exists
		await CheckAcademicMediumExistsAsync(input.EducationMediumId);

		// Create a new student directly in Registration status
		var studentId = GuidGenerator.Create();

		// Create the student aggregate with minimal initial data
		var student = new Student(
			id: studentId,
			tenantId: CurrentTenant.Id,
			firstName: input.FirstName,
			middleName: input.MiddleName,
			lastName: input.LastName,
			educationMediumId: input.EducationMediumId,
			standardId: input.StandardId,
			divisionId: input.DivisionId,
			status: StudentLifeCycleStatusEnum.Registation
		);

		// Create and attach additional details
		student.GetType().GetProperty("AdditionalDetail")?.SetValue(student, new StudentAdditionalDetail(
			id: studentId,
			tenantId: CurrentTenant.Id,
			dateOfBirthday: input.DateOfBirth,
			gender: input.Gender,
			dateOfInquiry: null,
			inquiryNo: string.Empty,
			inquiryPreferenceId: null
		));

		// Update additional details with full information
		if (student.AdditionalDetail != null)
		{
			student.AdditionalDetail.UpdateGeneralInformation(
				input.DateOfBirth,
				input.Gender,
				input.ProfilePictureUrl,
				input.AadharNo,
				input.ApparId,
				input.PanNo,
				input.Religion,
				input.FamilyCast,
				input.CastCategory,
				input.Nationality,
				input.BirthPlace,
				input.MotherTongue,
				input.PreviousSchool,
				input.LastClassPassed,
				StudentRegistrationStateEnum.General
			);
		}

		// Create and attach contact details
		student.GetType().GetProperty("ContactDetail")?.SetValue(student, new StudentContactDetail(
			id: studentId,
			tenantId: CurrentTenant.Id,
			primaryContactNo: input.PrimaryContactNo,
			secondaryContactNo: input.SecondaryContactNo,
			email: input.Email,
			address1: input.Address1,
			address2: input.Address2,
			address3: input.Address3,
			city: input.City,
			state: input.State,
			country: input.Country,
			pincode: input.Pincode
		));

		// Save the new student
		await studentRepository.InsertAsync(student, autoSave: true);

		// Return output DTO
		return new UpdateGeneralInfoOutputDto
		{
			StudentId = student.Id,
			Status = student.Status,
			FirstName = student.FirstName,
			MiddleName = student.MiddleName,
			LastName = student.LastName,
			DateOfBirth = student.AdditionalDetail?.DateOfBirthday,
			Gender = student.AdditionalDetail?.Gender
		};
	}

	public async Task<StudentFamilyInfoDto> GetFamilyInfoAsync(Guid studentId)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to view family information. Current status: {student.Status}"
			);
		}

		// Load family detail if exists
		var familyDetail = await studentFamilyDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

		var result = new StudentFamilyInfoDto
		{
			StudentId = student.Id,
			Status = student.Status,
			FatherDetails = familyDetail?.FatherDetails != null ? MapParentGuardianDetailsToDto(familyDetail.FatherDetails) : null,
			MotherDetails = familyDetail?.MotherDetails != null ? MapParentGuardianDetailsToDto(familyDetail.MotherDetails) : null,
			GuardianDetails = familyDetail?.GuardianDetails != null ? MapParentGuardianDetailsToDto(familyDetail.GuardianDetails) : null,
			SiblingStudents = new List<SiblingStudentDto>()
		};

		// Load sibling information if exists
		if (familyDetail?.SiblingStudentIds != null && familyDetail.SiblingStudentIds.Any())
		{
			var siblings = new List<SiblingStudentDto>();
			
			foreach (var siblingId in familyDetail.SiblingStudentIds)
			{
				var sibling = await studentRepository.FirstOrDefaultAsync(x => x.Id == siblingId);
				if (sibling != null)
				{
					Standard? standard = null;
					Division? division = null;

					if (sibling.StandardId.HasValue)
					{
						standard = await standardRepository.FirstOrDefaultAsync(x => x.Id == sibling.StandardId.Value);
					}

					if (sibling.DivisionId.HasValue)
					{
						division = await divisionRepository.FirstOrDefaultAsync(x => x.Id == sibling.DivisionId.Value);
					}

					siblings.Add(new SiblingStudentDto
					{
						StudentId = sibling.Id,
						FullName = $"{sibling.FirstName} {sibling.MiddleName} {sibling.LastName}".Trim(),
						StandardName = standard?.Name,
						DivisionName = division?.Name,
						GrNo = sibling.GrNo
					});
				}
			}

			result.SiblingStudents = siblings;
		}

		return result;
	}

	public async Task<StudentFamilyInfoDto> UpdateFamilyInfoAsync(Guid studentId, UpdateFamilyInfoDto input)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to update family information. Current status: {student.Status}"
			);
		}

		// Validate sibling student IDs exist if provided
		if (input.SiblingStudentIds != null && input.SiblingStudentIds.Any())
		{
			foreach (var siblingId in input.SiblingStudentIds)
			{
				if (!await studentRepository.AnyAsync(x => x.Id == siblingId))
				{
					throw new BusinessException(
						"ShikshaMitra:InvalidSiblingStudent",
						$"Sibling student with ID {siblingId} not found"
					);
				}
			}
		}

		// Load or create family detail
		var familyDetail = await studentFamilyDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);
		var isNewFamilyDetail = familyDetail == null;
		
		if (isNewFamilyDetail)
		{
			// Create new family detail entity
			familyDetail = new StudentFamilyDetail(studentId, CurrentTenant.Id);
			await studentFamilyDetailRepository.InsertAsync(familyDetail, autoSave: false);
		}

		// Map DTOs to value objects
		var fatherDetails = input.FatherDetails != null ? MapDtoToParentGuardianDetails(input.FatherDetails) : null;
		var motherDetails = input.MotherDetails != null ? MapDtoToParentGuardianDetails(input.MotherDetails) : null;
		var guardianDetails = input.GuardianDetails != null ? MapDtoToParentGuardianDetails(input.GuardianDetails) : null;

		// Update family information
		familyDetail.UpdateFamilyInformation(
			fatherDetails,
			motherDetails,
			guardianDetails,
			input.SiblingStudentIds
		);

		// Update registration state in additional detail
		if (student.AdditionalDetail != null)
		{
			var currentState = student.AdditionalDetail.StudentRegistrationState;
			if (currentState < StudentRegistrationStateEnum.Family)
			{
				// Update state to Family using reflection (since there's no direct method)
				var property = student.AdditionalDetail.GetType().GetProperty("StudentRegistrationState");
				property?.SetValue(student.AdditionalDetail, StudentRegistrationStateEnum.Family);
				// Persist the registration state change
				await studentAdditionalDetailRepository.UpdateAsync(student.AdditionalDetail, autoSave: false);
			}
		}

		// Note: No need to call UpdateAsync on familyDetail if it's new - InsertAsync already tracked it

		return await GetFamilyInfoAsync(studentId);
	}

	private ParentGuardianDto MapParentGuardianDetailsToDto(ParentGuardianDetails details)
	{
		return new ParentGuardianDto
		{
			FullName = details.FullName,
			DateOfBirth = details.DateOfBirth,
			Occupation = details.Occupation,
			Email = details.Email,
			ContactNo = details.ContactNo,
			OfficeNo = details.OfficeNo,
			Qualification = details.Qualification,
			YearOfPassing = details.YearOfPassing,
			Designation = details.Designation,
			Organization = details.Organization,
			AadharNo = details.AadharNo,
			OfficeAddress = details.OfficeAddress != null ? MapAddressDetailsToDto(details.OfficeAddress) : null,
			LocalAddress = details.LocalAddress != null ? MapAddressDetailsToDto(details.LocalAddress) : null
		};
	}

	private AddressDto MapAddressDetailsToDto(AddressDetails address)
	{
		return new AddressDto
		{
			BuildingNo = address.BuildingNo,
			AreaLocalityRoadName = address.AreaLocalityRoadName,
			Landmark = address.Landmark,
			State = address.State,
			City = address.City,
			Pincode = address.Pincode,
			Country = address.Country
		};
	}

	private ParentGuardianDetails MapDtoToParentGuardianDetails(ParentGuardianDto dto)
	{
		var officeAddress = dto.OfficeAddress != null ? MapDtoToAddressDetails(dto.OfficeAddress) : null;
		var localAddress = dto.LocalAddress != null ? MapDtoToAddressDetails(dto.LocalAddress) : null;

		return new ParentGuardianDetails(
			dto.FullName,
			dto.DateOfBirth,
			dto.Occupation,
			dto.Email,
			dto.ContactNo,
			dto.OfficeNo,
			dto.Qualification,
			dto.YearOfPassing,
			dto.Designation,
			dto.Organization,
			dto.AadharNo,
			officeAddress,
			localAddress
		);
	}

	private AddressDetails MapDtoToAddressDetails(AddressDto dto)
	{
		return new AddressDetails(
			dto.BuildingNo,
			dto.AreaLocalityRoadName,
			dto.Landmark,
			dto.State,
			dto.City,
			dto.Pincode,
			dto.Country
		);
	}

	public async Task<StudentCommunicationInfoDto> GetCommunicationInfoAsync(Guid studentId)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to view communication information. Current status: {student.Status}"
			);
		}

		// Load communication detail if exists
		var communicationDetail = await studentCommunicationDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

		var result = new StudentCommunicationInfoDto
		{
			StudentId = student.Id,
			Status = student.Status,
			LocalAddress = communicationDetail?.LocalAddress != null ? MapAddressDetailsToDto(communicationDetail.LocalAddress) : null,
			PermanentAddress = communicationDetail?.PermanentAddress != null ? MapAddressDetailsToDto(communicationDetail.PermanentAddress) : null,
			EmergencyAddress = communicationDetail?.EmergencyAddress != null ? MapAddressDetailsToDto(communicationDetail.EmergencyAddress) : null,
			IsPermanentSameAsLocal = communicationDetail?.IsPermanentSameAsLocal ?? false,
			IsEmergencySameAsLocal = communicationDetail?.IsEmergencySameAsLocal ?? false,
			EmergencyContactName = communicationDetail?.EmergencyContactName,
			EmergencyContactPhone = communicationDetail?.EmergencyContactPhone,
			EmergencyContactRelation = communicationDetail?.EmergencyContactRelation
		};

		return result;
	}

	public async Task<StudentCommunicationInfoDto> UpdateCommunicationInfoAsync(Guid studentId, UpdateCommunicationInfoDto input)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to update communication information. Current status: {student.Status}"
			);
		}

		// Load or create communication detail
		var communicationDetail = await studentCommunicationDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);
		var isNewCommunicationDetail = communicationDetail == null;
		
		if (isNewCommunicationDetail)
		{
			// Create new communication detail entity
			communicationDetail = new StudentCommunicationDetail(studentId, CurrentTenant.Id);
			await studentCommunicationDetailRepository.InsertAsync(communicationDetail, autoSave: false);
		}

		// Map DTOs to value objects
		var localAddress = input.LocalAddress != null ? MapDtoToAddressDetails(input.LocalAddress) : null;
		var permanentAddress = input.PermanentAddress != null ? MapDtoToAddressDetails(input.PermanentAddress) : null;
		var emergencyAddress = input.EmergencyAddress != null ? MapDtoToAddressDetails(input.EmergencyAddress) : null;

		// Update communication information
		communicationDetail.UpdateCommunicationInformation(
			localAddress,
			permanentAddress,
			emergencyAddress,
			input.IsPermanentSameAsLocal,
			input.IsEmergencySameAsLocal,
			input.EmergencyContactName,
			input.EmergencyContactPhone,
			input.EmergencyContactRelation
		);

		// Update registration state in additional detail
		if (student.AdditionalDetail != null)
		{
			var currentState = student.AdditionalDetail.StudentRegistrationState;
			if (currentState < StudentRegistrationStateEnum.Communication)
			{
				// Update state to Communication using reflection (since there's no direct method)
				var property = student.AdditionalDetail.GetType().GetProperty("StudentRegistrationState");
				property?.SetValue(student.AdditionalDetail, StudentRegistrationStateEnum.Communication);
				// Persist the registration state change
				await studentAdditionalDetailRepository.UpdateAsync(student.AdditionalDetail, autoSave: false);
			}
		}

		// Commit all changes in a single transaction
		await UnitOfWorkManager.Current!.SaveChangesAsync();

		// Return the updated communication info
		return await GetCommunicationInfoAsync(studentId);
	}

	public async Task<StudentAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid studentId)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to view additional details. Current status: {student.Status}"
			);
		}

		// Load additional detail (it should exist from general info step)
		var additionalDetail = student.AdditionalDetail;

		var result = new StudentAdditionalDetailsDto
		{
			StudentId = student.Id,
			Status = student.Status,
			Category = null, // This would need to be added to StudentAdditionalDetail domain entity if needed
			IsBPLStudent = additionalDetail?.IsBPLStudent ?? false,
			BPLCardNo = additionalDetail?.BPLCardNo,
			SecondLanguage = additionalDetail?.SecondLanguage,
			HaveDisability = additionalDetail?.HaveDisability ?? false,
			EmergencyRelativeName = additionalDetail?.EmergencyRelationName,
			EmergencyRelation = additionalDetail?.EmergencyRelationType,
			EmergencyContactNo = additionalDetail?.EmergencyContactNo,
			IsSingleChild = additionalDetail?.IsSingleChild ?? false,
			IsSingleParentChild = additionalDetail?.IsSingleParentChild ?? false
		};

		return result;
	}

	public async Task<StudentAdditionalDetailsDto> UpdateAdditionalDetailsAsync(Guid studentId, UpdateAdditionalDetailsDto input)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to update additional details. Current status: {student.Status}"
			);
		}

		// Update additional detail properties using reflection (since the domain entity has private setters)
		if (student.AdditionalDetail != null)
		{
			var additionalDetail = student.AdditionalDetail;
			var type = additionalDetail.GetType();
			
			type.GetProperty("IsBPLStudent")?.SetValue(additionalDetail, input.IsBPLStudent);
			type.GetProperty("BPLCardNo")?.SetValue(additionalDetail, input.BPLCardNo);
			type.GetProperty("SecondLanguage")?.SetValue(additionalDetail, input.SecondLanguage);
			type.GetProperty("HaveDisability")?.SetValue(additionalDetail, input.HaveDisability);
			type.GetProperty("EmergencyRelationName")?.SetValue(additionalDetail, input.EmergencyRelativeName);
			type.GetProperty("EmergencyRelationType")?.SetValue(additionalDetail, input.EmergencyRelation);
			type.GetProperty("EmergencyContactNo")?.SetValue(additionalDetail, input.EmergencyContactNo);
			type.GetProperty("IsSingleChild")?.SetValue(additionalDetail, input.IsSingleChild);
			type.GetProperty("IsSingleParentChild")?.SetValue(additionalDetail, input.IsSingleParentChild);

			// Update registration state
			var currentState = additionalDetail.StudentRegistrationState;
			if (currentState < StudentRegistrationStateEnum.AdditionalDetails)
			{
				type.GetProperty("StudentRegistrationState")?.SetValue(additionalDetail, StudentRegistrationStateEnum.AdditionalDetails);
			}
		}

		// Guard against null AdditionalDetail - fail fast with clear business error
		if (student.AdditionalDetail == null)
		{
			throw new BusinessException(
				"ShikshaMitra:MissingAdditionalDetail",
				"Student additional detail not found. Please complete the general information step first."
			);
		}

		// Save changes
		await studentAdditionalDetailRepository.UpdateAsync(student.AdditionalDetail, autoSave: true);

		// Return the updated additional details
		return await GetAdditionalDetailsAsync(studentId);
	}

	public async Task<StudentMedicalRecordsDto> GetMedicalRecordsAsync(Guid studentId)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to view medical records. Current status: {student.Status}"
			);
		}

		// Load medical record if exists
		var medicalRecord = await studentMedicalRecordRepository.FirstOrDefaultAsync(x => x.Id == studentId);

		var result = new StudentMedicalRecordsDto
		{
			StudentId = student.Id,
			Status = student.Status,
			WeightKg = medicalRecord?.WeightKg,
			HeightCm = medicalRecord?.HeightCm,
			BodyMassIndex = medicalRecord?.BodyMassIndex,
			PulseRate = medicalRecord?.PulseRate,
			Hemoglobin = medicalRecord?.Hemoglobin,
			Allergies = medicalRecord?.Allergies,
			ChildImmunization = medicalRecord?.ChildImmunization,
			CovidVaccinationDoses = medicalRecord?.CovidVaccinationDoses,
			ImmunizationRemarks = medicalRecord?.ImmunizationRemarks,
			BloodGroup = medicalRecord?.BloodGroup,
			ChronicConditions = medicalRecord?.ChronicConditions,
			CurrentMedications = medicalRecord?.CurrentMedications
		};

		return result;
	}

	public async Task<StudentMedicalRecordsDto> UpdateMedicalRecordsAsync(Guid studentId, UpdateMedicalRecordsDto input)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to update medical records. Current status: {student.Status}"
			);
		}

		// Load or create medical record
		var medicalRecord = await studentMedicalRecordRepository.FirstOrDefaultAsync(x => x.Id == studentId);
		var isNewMedicalRecord = medicalRecord == null;
		
		if (isNewMedicalRecord)
		{
			// Create new medical record entity
			medicalRecord = new StudentMedicalRecord(studentId, CurrentTenant.Id);
			await studentMedicalRecordRepository.InsertAsync(medicalRecord, autoSave: false);
		}

		// Update medical records
		medicalRecord.UpdateMedicalRecords(
			input.WeightKg,
			input.HeightCm,
			input.BodyMassIndex,
			input.PulseRate,
			input.Hemoglobin,
			input.Allergies,
			input.ChildImmunization,
			input.CovidVaccinationDoses,
			input.ImmunizationRemarks,
			input.BloodGroup,
			input.ChronicConditions,
			input.CurrentMedications
		);

		// Update registration state in additional detail
		if (student.AdditionalDetail != null)
		{
			var currentState = student.AdditionalDetail.StudentRegistrationState;
			if (currentState < StudentRegistrationStateEnum.MedicalRecords)
			{
				var property = student.AdditionalDetail.GetType().GetProperty("StudentRegistrationState");
				property?.SetValue(student.AdditionalDetail, StudentRegistrationStateEnum.MedicalRecords);
				// Persist the registration state change
				await studentAdditionalDetailRepository.UpdateAsync(student.AdditionalDetail, autoSave: false);
			}
		}

		// Commit all changes in a single transaction
		await UnitOfWorkManager.Current!.SaveChangesAsync();

		// Return the updated medical records
		return await GetMedicalRecordsAsync(studentId);
	}

	public async Task<StudentPreviousSchoolDetailsDto> GetPreviousSchoolDetailsAsync(Guid studentId)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to view previous school details. Current status: {student.Status}"
			);
		}

		// Load previous school detail if exists
		var previousSchoolDetail = await studentPreviousSchoolDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

		var result = new StudentPreviousSchoolDetailsDto
		{
			StudentId = student.Id,
			Status = student.Status,
			SchoolName = previousSchoolDetail?.SchoolName,
			SchoolAddress = previousSchoolDetail?.SchoolAddress,
			Board = previousSchoolDetail?.Board,
			Medium = previousSchoolDetail?.Medium,
			LastClassAttended = previousSchoolDetail?.LastClassAttended,
			TCNumber = previousSchoolDetail?.TCNumber,
			DateOfLeaving = previousSchoolDetail?.DateOfLeaving,
			PercentageObtained = previousSchoolDetail?.PercentageObtained,
			AcademicYear = previousSchoolDetail?.AcademicYear,
			ReasonForLeaving = previousSchoolDetail?.ReasonForLeaving,
			Remarks = previousSchoolDetail?.Remarks
		};

		return result;
	}

	public async Task<StudentPreviousSchoolDetailsDto> UpdatePreviousSchoolDetailsAsync(Guid studentId, UpdatePreviousSchoolDetailsDto input)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to update previous school details. Current status: {student.Status}"
			);
		}

		// Load or create previous school detail
		var previousSchoolDetail = await studentPreviousSchoolDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);
		var isNewPreviousSchoolDetail = previousSchoolDetail == null;
		
		if (isNewPreviousSchoolDetail)
		{
			// Create new previous school detail entity
			previousSchoolDetail = new StudentPreviousSchoolDetail(studentId, CurrentTenant.Id);
			await studentPreviousSchoolDetailRepository.InsertAsync(previousSchoolDetail, autoSave: false);
		}

		// Update previous school details
		previousSchoolDetail.UpdatePreviousSchoolDetails(
			input.SchoolName,
			input.SchoolAddress,
			input.Board,
			input.Medium,
			input.LastClassAttended,
			input.TCNumber,
			input.DateOfLeaving,
			input.PercentageObtained,
			input.AcademicYear,
			input.ReasonForLeaving,
			input.Remarks
		);

		// Update registration state in additional detail
		if (student.AdditionalDetail != null)
		{
			var currentState = student.AdditionalDetail.StudentRegistrationState;
			if (currentState < StudentRegistrationStateEnum.PrevSchoolDetails)
			{
				var property = student.AdditionalDetail.GetType().GetProperty("StudentRegistrationState");
				property?.SetValue(student.AdditionalDetail, StudentRegistrationStateEnum.PrevSchoolDetails);
				// Persist the registration state change
				await studentAdditionalDetailRepository.UpdateAsync(student.AdditionalDetail, autoSave: false);
			}
		}

		// Commit all changes in a single transaction
		await UnitOfWorkManager.Current!.SaveChangesAsync();

		// Return the updated previous school details
		return await GetPreviousSchoolDetailsAsync(studentId);
	}

	public async Task<StudentBankDetailsDto> GetBankDetailsAsync(Guid studentId)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to view bank details. Current status: {student.Status}"
			);
		}

		// Load bank detail if exists
		var bankDetail = await studentBankDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);

		var result = new StudentBankDetailsDto
		{
			StudentId = student.Id,
			Status = student.Status,
			BankName = bankDetail?.BankName,
			AccountNo = bankDetail?.AccountNo,
			IFSCCode = bankDetail?.IFSCCode,
			BranchName = bankDetail?.BranchName,
			AccountHolderName = bankDetail?.AccountHolderName,
			AccountType = bankDetail?.AccountType,
			BankAddress = bankDetail?.BankAddress,
			MICRCode = bankDetail?.MICRCode,
			SwiftCode = bankDetail?.SwiftCode
		};

		return result;
	}

	public async Task<StudentBankDetailsDto> UpdateBankDetailsAsync(Guid studentId, UpdateBankDetailsDto input)
	{
		// Load the student aggregate
		var student = await studentRepository.GetAsync(studentId);

		// Validate student is in a valid status
		if (student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException(
				"ShikshaMitra:InvalidStudentStatus",
				$"Student must be in Registration status to update bank details. Current status: {student.Status}"
			);
		}

		// Load or create bank detail
		var bankDetail = await studentBankDetailRepository.FirstOrDefaultAsync(x => x.Id == studentId);
		var isNewBankDetail = bankDetail == null;
		
		if (isNewBankDetail)
		{
			// Create new bank detail entity
			bankDetail = new StudentBankDetail(studentId, CurrentTenant.Id);
			await studentBankDetailRepository.InsertAsync(bankDetail, autoSave: false);
		}

		// Update bank details
		bankDetail.UpdateBankDetails(
			input.BankName,
			input.AccountNo,
			input.IFSCCode,
			input.BranchName,
			input.AccountHolderName,
			input.AccountType,
			input.BankAddress,
			input.MICRCode,
			input.SwiftCode
		);

		// Update registration state in additional detail
		if (student.AdditionalDetail != null)
		{
			var currentState = student.AdditionalDetail.StudentRegistrationState;
			if (currentState < StudentRegistrationStateEnum.BankDetails)
			{
				var property = student.AdditionalDetail.GetType().GetProperty("StudentRegistrationState");
				property?.SetValue(student.AdditionalDetail, StudentRegistrationStateEnum.BankDetails);
				// Persist the registration state change
				await studentAdditionalDetailRepository.UpdateAsync(student.AdditionalDetail, autoSave: false);
			}
		}

		// Commit all changes in a single transaction
		await UnitOfWorkManager.Current!.SaveChangesAsync();

		// Return the updated bank details
		return await GetBankDetailsAsync(studentId);
	}

	private async Task CheckAcademicMediumExistsAsync(Guid academicMediumId)
	{
		if (!await academicMediumRepository.AnyAsync(x => x.Id == academicMediumId))
		{
			throw new BusinessException("", L["Academic Medium Not Found"]);
		}
	}
}
