using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Validation;

namespace ShikshaMitra.HR.Employees.Onboarding;

[RemoteService(false)]
public class EmployeeApplicationAppService(
	IRepository<EmployeeApplication, Guid> applicationRepository,
	IEmployeeRepository employeeRepository,
	IEmployeeAdditionalDetailsRepository additionalDetailsRepository,
	EmployeeManager employeeManager,
	EmployeeDocumentManager employeeDocumentManager,
	EmployeePersonalInfoCreationWorkflow personalInfoCreationWorkflow,
	HrPolicyResolver policyResolver,
	IAcademicYearRepository academicYearRepository,
	IBlobContainer blobContainer,
	IDataFilter dataFilter,
	IStringEncryptionService stringEncryptionService,
	IEmployeeOnboardingInvitationEmailService invitationEmailService)
	: ApplicationService, IEmployeeApplicationAppService
{
	private const int TokenExpiryDays = 30;
	private const string DefaultPolicyAssignmentReason = "Employee onboarding default policy assignment";
	private const string DefaultPolicyConfirmationTokenMarker = "__employee_onboarding_default_policy_assignment__";
	private static readonly string[] ResumeExtensions = [".pdf", ".doc", ".docx"];

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View)]
	public async Task<PagedResultDto<EmployeeApplicationListItemDto>> GetListAsync(GetEmployeeApplicationListInput input)
	{
		var query = await applicationRepository.GetQueryableAsync();

		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLowerInvariant();
			query = query.Where(x =>
				x.ApplicationNumber.ToLower().Contains(search) ||
				(x.FirstName != null && x.FirstName.ToLower().Contains(search)) ||
				(x.MiddleName != null && x.MiddleName.ToLower().Contains(search)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(search)) ||
				(x.Email != null && x.Email.ToLower().Contains(search)) ||
				(x.MobileNo != null && x.MobileNo.ToLower().Contains(search)) ||
				(x.PositionAppliedFor != null && x.PositionAppliedFor.ToLower().Contains(search)));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var applications = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		return new PagedResultDto<EmployeeApplicationListItemDto>(
			totalCount,
			applications.Select(MapToListItemDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Add)]
	public async Task<EmployeeApplicationInviteDto> CreateAsync(CreateEmployeeApplicationInviteDto input)
	{
		await EnsureInviteEmailAvailableAsync(input.Email);

		var token = CreateRawToken();
		var now = Clock.Now;
		var publicPath = CreatePublicPath(token);
		var application = new EmployeeApplication(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			CreateApplicationNumber(now),
			HashToken(token),
			stringEncryptionService.Encrypt(token),
			now.AddDays(TokenExpiryDays),
			now,
			input.FirstName,
			input.MiddleName,
			input.LastName,
			input.Email,
			input.MobileNo,
			input.PositionAppliedFor,
			input.DepartmentAppliedFor);

		application = await applicationRepository.InsertAsync(application, autoSave: true);
		var emailResult = await invitationEmailService.SendInvitationAsync(application, publicPath);
		return CreateInviteResponse(application, token, emailResult);
	}

	private async Task EnsureInviteEmailAvailableAsync(string email)
	{
		var normalizedEmail = email.Trim().ToLowerInvariant();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(employeeQuery.Where(x => x.Email != null && x.Email.ToLower() == normalizedEmail)))
		{
			throw new BusinessException(
					HRErrorCodes.EmployeeApplicationEmailAlreadyInUse,
					"An employee or open onboarding application already uses this email.")
				.WithData("field", nameof(CreateEmployeeApplicationInviteDto.Email));
		}

		var applicationQuery = await applicationRepository.GetQueryableAsync();
		var terminalStatuses = new[]
		{
			EmployeeApplicationStatus.Finalized,
			EmployeeApplicationStatus.Rejected,
			EmployeeApplicationStatus.Withdrawn
		};
		var hasOpenApplication = await AsyncExecuter.AnyAsync(applicationQuery.Where(x =>
			x.Email != null &&
			x.Email.ToLower() == normalizedEmail &&
			!terminalStatuses.Contains(x.Status)));
		if (hasOpenApplication)
		{
			throw new BusinessException(
					HRErrorCodes.EmployeeApplicationEmailAlreadyInUse,
					"An employee or open onboarding application already uses this email.")
				.WithData("field", nameof(CreateEmployeeApplicationInviteDto.Email));
		}
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View)]
	public async Task<EmployeeApplicationDto> GetAsync(Guid id)
	{
		return MapToDto(await GetApplicationAsync(id));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Edit)]
	public async Task<EmployeeApplicationDto> UpdateStatusAsync(Guid id, EmployeeApplicationStatusUpdateDto input)
	{
		var application = await GetApplicationAsync(id);
		application.ChangeStatus(input.Status, input.Reason);
		application = await applicationRepository.UpdateAsync(application, autoSave: true);
		return MapToDto(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Edit)]
	public async Task<EmployeeApplicationInviteDto> RegenerateLinkAsync(Guid id)
	{
		var application = await GetApplicationAsync(id);
		var token = CreateRawToken();
		var now = Clock.Now;
		var publicPath = CreatePublicPath(token);
		application.SetToken(HashToken(token), stringEncryptionService.Encrypt(token), now.AddDays(TokenExpiryDays), now);
		application = await applicationRepository.UpdateAsync(application, autoSave: true);
		var emailResult = await invitationEmailService.SendInvitationAsync(application, publicPath);
		return CreateInviteResponse(application, token, emailResult);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Finalize)]
	public async Task<EmployeeApplicationFinalizeResultDto> FinalizeAsync(Guid id, FinalizeEmployeeApplicationDto input)
	{
		var application = await GetApplicationAsync(id);
		if (!application.SubmittedAt.HasValue)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidStatusTransition);
		}

		if (application.Status is EmployeeApplicationStatus.Rejected or EmployeeApplicationStatus.Withdrawn)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidStatusTransition);
		}

		await EnsureFinalizationPolicyOverrideAsync(input.PersonalInfo);

		var (photoFileName, photoContent) = await ReadProfilePhotoForFinalizationAsync(application, input.UseSubmittedPhoto);
		var employee = await personalInfoCreationWorkflow.CreateAsync(
			input.PersonalInfo,
			photoFileName,
			photoContent);

		var workExperiences = (input.WorkExperiences.Count > 0
				? input.WorkExperiences
				: application.WorkExperiences.Select(MapToWorkExperienceDto).ToList())
			.Select(MapToWorkExperience)
			.ToList();

		if (workExperiences.Count > 0)
		{
			var updatedEmployee = await employeeManager.UpdateExperienceInfoByEmployeeIdAsync(employee.Id, workExperiences);
			await employeeRepository.UpdateAsync(updatedEmployee, autoSave: true);
		}

		await ApplyFamilyInfoAsync(employee.Id, input.FamilyInfo ?? MapToFamilyInfoDto(application.FamilyInfo));
		await ApplyAdditionalInfoAsync(employee.Id, input.AdditionalInfo ?? MapToAdditionalInfoDto(application.AdditionalInfo));

		if (input.AttachResumeAsDocument)
		{
			await AttachResumeAsync(employee.Id, application);
		}

		application.MarkFinalized(employee.Id, Clock.Now);
		application = await applicationRepository.UpdateAsync(application, autoSave: true);

		return new EmployeeApplicationFinalizeResultDto
		{
			Application = MapToDto(application, includePublicPath: true),
			Employee = employee
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View)]
	public async Task<EmployeeApplicationFileDto?> GetResumeAsync(Guid id)
	{
		var application = await GetApplicationAsync(id);
		if (!application.HasResume || application.ResumeBlobName == null)
		{
			return null;
		}

		return new EmployeeApplicationFileDto
		{
			FileName = application.ResumeFileName ?? "resume",
			ContentType = application.ResumeContentType ?? "application/octet-stream",
			Content = await ReadBlobAsync(application.ResumeBlobName)
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View)]
	public async Task<EmployeeApplicationFileDto?> GetPhotoAsync(Guid id)
	{
		var application = await GetApplicationAsync(id);
		if (!application.HasPhoto || application.PhotoBlobName == null)
		{
			return null;
		}

		return new EmployeeApplicationFileDto
		{
			FileName = application.PhotoFileName ?? "photo",
			ContentType = application.PhotoContentType ?? "application/octet-stream",
			Content = await ReadBlobAsync(application.PhotoBlobName)
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var application = await GetApplicationAsync(id);
		if (application.Status == EmployeeApplicationStatus.Finalized)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationCannotDeleteFinalized);
		}

		await applicationRepository.DeleteAsync(application, autoSave: true);
	}

	[AllowAnonymous]
	public async Task<PublicEmployeeApplicationDto> GetPublicAsync(string token)
	{
		var application = await GetApplicationByTokenAsync(token);
		using (CurrentTenant.Change(application.TenantId))
		{
			return MapToPublicDto(application, Clock.Now);
		}
	}

	[AllowAnonymous]
	public async Task<EmployeeApplicationDto> SubmitPublicAsync(
		string token,
		PublicEmployeeApplicationSubmitDto input,
		EmployeeApplicationFileInput? resume,
		EmployeeApplicationFileInput? photo)
	{
		var application = await GetApplicationByTokenAsync(token);
		using (CurrentTenant.Change(application.TenantId))
		{
			var now = Clock.Now;
			application.EnsureCanSubmit(now);
			ValidatePublicSubmitInput(input, now);
			ValidateUploadInputs(resume, photo);

			var personalInfo = MapToPersonalInfoSnapshot(input.PersonalInfo);
			var familyInfo = MapToFamilyInfoSnapshot(input.FamilyInfo);
			var additionalInfo = MapToAdditionalInfoSnapshot(input.AdditionalInfo);
			var workExperiences = input.WorkExperiences.Select(MapToWorkExperience).ToList();

			var resumeMetadata = await SaveResumeAsync(application.Id, resume);
			var photoMetadata = await SavePhotoAsync(application.Id, photo);

			application.Submit(
				now,
				personalInfo,
				familyInfo,
				additionalInfo,
				workExperiences,
				resumeMetadata,
				photoMetadata);

			application = await applicationRepository.UpdateAsync(application, autoSave: true);
			return MapToDto(application, includePublicPath: false);
		}
	}

	private static void ValidatePublicSubmitInput(PublicEmployeeApplicationSubmitDto input, DateTime now)
	{
		var validationResults = PublicEmployeeApplicationSubmitValidation.Validate(input, now);
		if (validationResults.Count > 0)
		{
			throw new AbpValidationException("Employee application validation failed.", validationResults.ToList());
		}
	}

	private static void ValidateUploadInputs(
		EmployeeApplicationFileInput? resume,
		EmployeeApplicationFileInput? photo)
	{
		if (resume != null && resume.Content.Length > 0)
		{
			var fileName = EnsureSafeFileName(resume.FileName);
			var extension = Path.GetExtension(fileName).ToLowerInvariant();
			if (!ResumeExtensions.Contains(extension))
			{
				throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidUpload)
					.WithData("allowedExtensions", string.Join(", ", ResumeExtensions));
			}

			FileValidationService.ValidateDocumentUpload(fileName, resume.Content, resume.Size);
		}

		if (photo != null && photo.Content.Length > 0)
		{
			var fileName = EnsureSafeFileName(photo.FileName);
			FileValidationService.ValidateProfilePictureUpload(fileName, photo.Content);
		}
	}

	private static EmployeeApplicationPersonalInfoSnapshot MapToPersonalInfoSnapshot(
		EmployeeApplicationPersonalInfoDto input) =>
		new(
			input.FirstName,
			input.MiddleName,
			input.ShortName,
			input.LastName,
			input.Email,
			input.MobileNo,
			input.SecondaryMobileNo,
			input.DateOfBirth.HasValue ? DateOnly.FromDateTime(input.DateOfBirth.Value) : null,
			input.Gender,
			input.Religion,
			input.CasteType,
			input.FamilyCaste,
			input.MaritalStatus,
			input.BloodGroup,
			input.IsTeaching,
			input.Department,
			input.Designation,
			input.Qualification,
			input.IdentificationMark1,
			input.Address1,
			input.Address2,
			input.Address3,
			MapToLocationSnapshot(input.Country),
			MapToLocationSnapshot(input.State),
			input.City,
			input.Pincode);

	private static EmployeeApplicationFamilyInfoSnapshot? MapToFamilyInfoSnapshot(
		EmployeeApplicationFamilyInfoDto? input)
	{
		if (input == null)
		{
			return null;
		}

		var father = MapToFamilyMemberSnapshot(input.FatherDetails);
		var mother = MapToFamilyMemberSnapshot(input.MotherDetails);
		var spouse = MapToFamilyMemberSnapshot(input.SpouseDetails);
		return father == null && mother == null && spouse == null
			? null
			: new EmployeeApplicationFamilyInfoSnapshot(father, mother, spouse);
	}

	private static EmployeeApplicationFamilyMemberSnapshot? MapToFamilyMemberSnapshot(
		EmployeeApplicationFamilyMemberDto? input) =>
		input == null || IsEmptyFamilyMember(input)
			? null
			: new EmployeeApplicationFamilyMemberSnapshot(
				input.Name,
				input.Occupation,
				input.MobileNo,
				input.Address1,
				input.Address2,
				input.Address3,
				MapToLocationSnapshot(input.Country),
				MapToLocationSnapshot(input.State),
				input.City,
				input.Pincode);

	private static EmployeeApplicationAdditionalInfoSnapshot? MapToAdditionalInfoSnapshot(
		EmployeeApplicationAdditionalInfoDto? input) =>
		input == null || !HasAdditionalInfo(input)
			? null
			: new EmployeeApplicationAdditionalInfoSnapshot(
				input.PersonWithDisability,
				input.SecondLanguage,
				input.IsLibraryMember,
				input.IsTransportMember);

	private static EmployeeApplicationLocationSnapshot? MapToLocationSnapshot(
		EmployeeApplicationLocationInfo? input) =>
		input == null || string.IsNullOrWhiteSpace(input.Name)
			? null
			: new EmployeeApplicationLocationSnapshot(input.Name, input.IsoCode);

	private async Task ApplyFamilyInfoAsync(Guid employeeId, EmployeeApplicationFamilyInfoDto? familyInfo)
	{
		if (!HasFamilyInfo(familyInfo))
		{
			return;
		}

		await employeeManager.UpdateFamilyInfoByEmployeeIdAsync(
			employeeId,
			MapToContactDetails(familyInfo?.FatherDetails),
			MapToContactDetails(familyInfo?.MotherDetails),
			MapToContactDetails(familyInfo?.SpouseDetails));
	}

	private async Task ApplyAdditionalInfoAsync(Guid employeeId, EmployeeApplicationAdditionalInfoDto? additionalInfo)
	{
		if (!HasAdditionalInfo(additionalInfo))
		{
			return;
		}

		var existingDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (existingDetails == null)
		{
			await additionalDetailsRepository.InsertAsync(new EmployeeAdditionalDetails(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				employeeId,
				bplCardNo: null,
				personWithDisability: additionalInfo!.PersonWithDisability,
				secondLanguage: additionalInfo.SecondLanguage,
				libraryMember: additionalInfo.IsLibraryMember,
				transportMember: additionalInfo.IsTransportMember));
			return;
		}

		existingDetails.Update(
			bplCardNo: existingDetails.BplCardNo,
			personWithDisability: additionalInfo!.PersonWithDisability,
			secondLanguage: additionalInfo.SecondLanguage,
			libraryMember: additionalInfo.IsLibraryMember,
			transportMember: additionalInfo.IsTransportMember);
		await additionalDetailsRepository.UpdateAsync(existingDetails);
	}

	private static ContactDetails? MapToContactDetails(EmployeeApplicationFamilyMemberDto? input) =>
		input == null || IsEmptyFamilyMember(input)
			? null
			: new ContactDetails(
				name: input.Name,
				occupation: input.Occupation,
				contactNo: input.MobileNo,
				address1: input.Address1,
				address2: input.Address2,
				address3: input.Address3,
				city: input.City,
				stateName: input.State?.Name,
				stateIsoCode: input.State?.IsoCode,
				countryName: input.Country?.Name,
				countryIsoCode: input.Country?.IsoCode,
				pinCode: input.Pincode);

	private static bool HasFamilyInfo(EmployeeApplicationFamilyInfoDto? input) =>
		input != null &&
		(!IsEmptyFamilyMember(input.FatherDetails) ||
		 !IsEmptyFamilyMember(input.MotherDetails) ||
		 !IsEmptyFamilyMember(input.SpouseDetails));

	private static bool HasAdditionalInfo(EmployeeApplicationAdditionalInfoDto? input) =>
		input != null &&
		(input.PersonWithDisability ||
		 input.IsLibraryMember ||
		 input.IsTransportMember ||
		 !string.IsNullOrWhiteSpace(input.SecondLanguage));

	private static bool IsEmptyFamilyMember(EmployeeApplicationFamilyMemberDto? input) =>
		input == null ||
		(string.IsNullOrWhiteSpace(input.Name) &&
		 string.IsNullOrWhiteSpace(input.Occupation) &&
		 string.IsNullOrWhiteSpace(input.MobileNo) &&
		 string.IsNullOrWhiteSpace(input.Address1) &&
		 string.IsNullOrWhiteSpace(input.Address2) &&
		 string.IsNullOrWhiteSpace(input.Address3) &&
		 string.IsNullOrWhiteSpace(input.City) &&
		 string.IsNullOrWhiteSpace(input.Pincode) &&
		 IsEmptyLocation(input.Country) &&
		 IsEmptyLocation(input.State));

	private static bool IsEmptyLocation(EmployeeApplicationLocationInfo? input) =>
		input == null ||
		(string.IsNullOrWhiteSpace(input.Name) && string.IsNullOrWhiteSpace(input.IsoCode));

	private async Task<EmployeeApplication> GetApplicationAsync(Guid id)
	{
		return await applicationRepository.GetAsync(id)
			?? throw new EntityNotFoundException(typeof(EmployeeApplication), id);
	}

	private async Task<EmployeeApplication> GetApplicationByTokenAsync(string token)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationTokenInvalid);
		}

		var tokenHash = HashToken(token);
		using (dataFilter.Disable<IMultiTenant>())
		{
			var query = await applicationRepository.GetQueryableAsync();
			var application = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.TokenHash == tokenHash));
			return application ?? throw new BusinessException(HRErrorCodes.EmployeeApplicationTokenInvalid);
		}
	}

	private async Task<(string? fileName, byte[]? content)> ReadProfilePhotoForFinalizationAsync(
		EmployeeApplication application,
		bool useSubmittedPhoto)
	{
		if (!useSubmittedPhoto || string.IsNullOrWhiteSpace(application.PhotoBlobName))
		{
			return (null, null);
		}

		return (
			application.PhotoFileName ?? "profile-picture",
			await ReadBlobAsync(application.PhotoBlobName));
	}

	private async Task AttachResumeAsync(Guid employeeId, EmployeeApplication application)
	{
		if (string.IsNullOrWhiteSpace(application.ResumeBlobName))
		{
			return;
		}

		var content = await ReadBlobAsync(application.ResumeBlobName);
		await employeeDocumentManager.AddDocumentsAsync(employeeId, [
			new EmployeeDocumentData
			{
				FileName = application.ResumeFileName ?? "resume.pdf",
				ContentType = application.ResumeContentType ?? "application/octet-stream",
				Content = content,
				Size = application.ResumeSize ?? content.Length
			}
		]);
	}

	private async Task<EmployeeApplicationUploadMetadata?> SaveResumeAsync(
		Guid applicationId,
		EmployeeApplicationFileInput? file)
	{
		if (file == null || file.Content.Length == 0)
		{
			return null;
		}

		var fileName = EnsureSafeFileName(file.FileName);
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		if (!ResumeExtensions.Contains(extension))
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidUpload)
				.WithData("allowedExtensions", string.Join(", ", ResumeExtensions));
		}

		FileValidationService.ValidateDocumentUpload(fileName, file.Content, file.Size);

		var blobName = $"employee-applications/{applicationId}/resume/{GuidGenerator.Create():N}_{fileName}";
		await blobContainer.SaveAsync(blobName, file.Content, overrideExisting: true);
		return new EmployeeApplicationUploadMetadata
		{
			BlobName = blobName,
			FileName = fileName,
			ContentType = file.ContentType,
			Size = file.Size
		};
	}

	private async Task<EmployeeApplicationUploadMetadata?> SavePhotoAsync(
		Guid applicationId,
		EmployeeApplicationFileInput? file)
	{
		if (file == null || file.Content.Length == 0)
		{
			return null;
		}

		var fileName = EnsureSafeFileName(file.FileName);
		FileValidationService.ValidateProfilePictureUpload(fileName, file.Content);

		var blobName = $"employee-applications/{applicationId}/photo/{GuidGenerator.Create():N}_{fileName}";
		await blobContainer.SaveAsync(blobName, file.Content, overrideExisting: true);
		return new EmployeeApplicationUploadMetadata
		{
			BlobName = blobName,
			FileName = fileName,
			ContentType = file.ContentType,
			Size = file.Size
		};
	}

	private async Task<byte[]> ReadBlobAsync(string blobName)
	{
		await using var stream = await blobContainer.GetAsync(blobName);
		using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);
		return memoryStream.ToArray();
	}

	private static string EnsureSafeFileName(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidUpload);
		}

		var sanitized = Path.GetFileName(fileName);
		if (sanitized != fileName)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidUpload);
		}

		return sanitized;
	}

	private EmployeeApplicationInviteDto CreateInviteResponse(
		EmployeeApplication application,
		string token,
		EmployeeOnboardingInvitationEmailResult emailResult)
	{
		return new EmployeeApplicationInviteDto
		{
			Application = MapToDto(application, includePublicPath: true),
			Token = token,
			PublicPath = CreatePublicPath(token),
			TokenExpiresAt = application.TokenExpiresAt,
			EmailDeliverySucceeded = emailResult.Succeeded,
			EmailDeliveryMessage = emailResult.Message
		};
	}

	private static string CreatePublicPath(string token) => $"/employee-application/{token}";

	private static string CreateRawToken()
	{
		var bytes = RandomNumberGenerator.GetBytes(32);
		return Convert.ToBase64String(bytes)
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
	}

	private static string HashToken(string token)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	private string CreateApplicationNumber(DateTime now) =>
		$"EA-{now:yyyyMMddHHmmss}-{GuidGenerator.Create().ToString("N")[..6].ToUpperInvariant()}";

	private static EmployeeApplicationListItemDto MapToListItemDto(EmployeeApplication application) =>
		new()
		{
			Id = application.Id,
			ApplicationNumber = application.ApplicationNumber,
			Status = application.Status,
			FullName = application.FullName,
			Email = application.Email,
			MobileNo = application.MobileNo,
			PositionAppliedFor = application.PositionAppliedFor,
			DepartmentAppliedFor = application.DepartmentAppliedFor,
			TokenExpiresAt = application.TokenExpiresAt,
			LinkLastIssuedAt = application.LinkLastIssuedAt,
			SubmittedAt = application.SubmittedAt,
			FinalizedAt = application.FinalizedAt,
			FinalizedEmployeeId = application.FinalizedEmployeeId,
			HasResume = application.HasResume,
			HasPhoto = application.HasPhoto,
			CreationTime = application.CreationTime
		};

	private EmployeeApplicationDto MapToDto(EmployeeApplication application, bool includePublicPath = true)
	{
		return new EmployeeApplicationDto
		{
			Id = application.Id,
			ApplicationNumber = application.ApplicationNumber,
			Status = application.Status,
			FullName = application.FullName,
			FirstName = application.FirstName,
			MiddleName = application.MiddleName,
			LastName = application.LastName,
			PublicPath = includePublicPath ? CreatePublicPathFromStoredToken(application) : null,
			Email = application.Email,
			MobileNo = application.MobileNo,
			SecondaryMobileNo = application.SecondaryMobileNo,
			PositionAppliedFor = application.PositionAppliedFor,
			DepartmentAppliedFor = application.DepartmentAppliedFor,
			DateOfBirth = application.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
			Gender = application.Gender,
			Religion = application.Religion,
			CasteType = application.CasteType,
			FamilyCaste = application.FamilyCaste,
			MaritalStatus = application.MaritalStatus,
			BloodGroup = application.BloodGroup,
			QualificationSummary = application.QualificationSummary,
			SkillsSummary = application.SkillsSummary,
			Address1 = application.Address1,
			Address2 = application.Address2,
			Address3 = application.Address3,
			Country = MapToLocation(application.Country),
			State = MapToLocation(application.State),
			City = application.City,
			PinCode = application.PinCode,
			PersonalInfo = MapToPersonalInfoDto(application.PersonalInfo),
			FamilyInfo = MapToFamilyInfoDto(application.FamilyInfo),
			AdditionalInfo = MapToAdditionalInfoDto(application.AdditionalInfo),
			WorkExperiences = application.WorkExperiences.Select(MapToWorkExperienceDto).ToList(),
			TokenExpiresAt = application.TokenExpiresAt,
			LinkLastIssuedAt = application.LinkLastIssuedAt,
			SubmittedAt = application.SubmittedAt,
			FinalizedAt = application.FinalizedAt,
			FinalizedEmployeeId = application.FinalizedEmployeeId,
			HasResume = application.HasResume,
			HasPhoto = application.HasPhoto,
			ResumeFileName = application.ResumeFileName,
			ResumeContentType = application.ResumeContentType,
			ResumeSize = application.ResumeSize,
			PhotoFileName = application.PhotoFileName,
			PhotoContentType = application.PhotoContentType,
			PhotoSize = application.PhotoSize,
			StatusReason = application.StatusReason,
			InternalNotes = application.InternalNotes,
			CreationTime = application.CreationTime
		};
	}

	private string? CreatePublicPathFromStoredToken(EmployeeApplication application)
	{
		if (string.IsNullOrWhiteSpace(application.EncryptedToken))
		{
			return null;
		}

		try
		{
			var token = stringEncryptionService.Decrypt(application.EncryptedToken);
			return string.IsNullOrWhiteSpace(token) ? null : CreatePublicPath(token);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(
				ex,
				"Employee application {ApplicationId} has an encrypted invite token that could not be decrypted.",
				application.Id);
			return null;
		}
	}

	private static PublicEmployeeApplicationDto MapToPublicDto(EmployeeApplication application, DateTime now)
	{
		var isExpired = application.TokenExpiresAt <= now;
		var isSubmitted = application.SubmittedAt.HasValue;
		var isClosed = application.Status is
			EmployeeApplicationStatus.Finalized or
			EmployeeApplicationStatus.Rejected or
			EmployeeApplicationStatus.Withdrawn;
		return new PublicEmployeeApplicationDto
		{
			Id = application.Id,
			ApplicationNumber = application.ApplicationNumber,
			Status = application.Status,
			FullName = application.FullName,
			FirstName = application.FirstName,
			MiddleName = application.MiddleName,
			LastName = application.LastName,
			Email = application.Email,
			MobileNo = application.MobileNo,
			PositionAppliedFor = application.PositionAppliedFor,
			DepartmentAppliedFor = application.DepartmentAppliedFor,
			TokenExpiresAt = application.TokenExpiresAt,
			IsExpired = isExpired,
			IsSubmitted = isSubmitted,
			CanSubmit = !isExpired && !isSubmitted && !isClosed
		};
	}

	private async Task EnsureFinalizationPolicyOverrideAsync(CreateEmployeePersonalInfoDto personalInfo)
	{
		if (personalInfo.PolicyOverride != null)
		{
			return;
		}

		var dateOfJoining = personalInfo.DateOfJoining.ToDateTime(TimeOnly.MinValue);
		var academicYearQuery = await academicYearRepository.GetQueryableAsync();
		var academicYear = await AsyncExecuter.FirstOrDefaultAsync(academicYearQuery.Where(x =>
			x.StartDate <= dateOfJoining &&
			x.EndDate >= dateOfJoining));

		if (academicYear == null)
		{
			throw new BusinessException(HRErrorCodes.AcademicYearRequiredForPolicyResolution);
		}

		var resolved = await policyResolver.ResolvePreviewAsync(
			null,
			null,
			dateOfJoining,
			academicYear.Id);

		personalInfo.PolicyOverride = new CreateEmployeePolicyOverrideDto
		{
			AcademicYearId = resolved.AcademicYearId,
			AttendancePolicyId = resolved.AttendancePolicy.Id,
			PayrollPolicyId = resolved.PayrollPolicy.Id,
			LeavePolicyId = resolved.LeavePolicy?.Id
				?? throw new BusinessException(HRErrorCodes.DefaultLeavePolicyRequired),
			EffectiveFrom = dateOfJoining,
			AssignmentReason = DefaultPolicyAssignmentReason,
			ConfirmationToken = DefaultPolicyConfirmationTokenMarker
		};
	}

	private static EmployeeApplicationLocationInfo? MapToLocation(Country? value) =>
		value == null ? null : new EmployeeApplicationLocationInfo { Name = value.Name, IsoCode = value.IsoCode };

	private static EmployeeApplicationLocationInfo? MapToLocation(State? value) =>
		value == null ? null : new EmployeeApplicationLocationInfo { Name = value.Name, IsoCode = value.IsoCode };

	private static EmployeeApplicationLocationInfo? MapToLocation(EmployeeApplicationLocationSnapshot? value) =>
		value == null ? null : new EmployeeApplicationLocationInfo { Name = value.Name, IsoCode = value.IsoCode };

	private static EmployeeApplicationPersonalInfoDto? MapToPersonalInfoDto(
		EmployeeApplicationPersonalInfoSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationPersonalInfoDto
			{
				FirstName = value.FirstName,
				MiddleName = value.MiddleName,
				ShortName = value.ShortName,
				LastName = value.LastName,
				Email = value.Email,
				MobileNo = value.MobileNo,
				SecondaryMobileNo = value.SecondaryMobileNo,
				DateOfBirth = value.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
				Gender = value.Gender,
				Religion = value.Religion,
				CasteType = value.CasteType,
				FamilyCaste = value.FamilyCaste,
				MaritalStatus = value.MaritalStatus,
				BloodGroup = value.BloodGroup,
				IsTeaching = value.IsTeaching,
				Department = value.Department,
				Designation = value.Designation,
				Qualification = value.Qualification,
				IdentificationMark1 = value.IdentificationMark1,
				Address1 = value.Address1,
				Address2 = value.Address2,
				Address3 = value.Address3,
				Country = MapToLocation(value.Country),
				State = MapToLocation(value.State),
				City = value.City,
				Pincode = value.Pincode
			};

	private static EmployeeApplicationFamilyInfoDto? MapToFamilyInfoDto(
		EmployeeApplicationFamilyInfoSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationFamilyInfoDto
			{
				FatherDetails = MapToFamilyMemberDto(value.FatherDetails),
				MotherDetails = MapToFamilyMemberDto(value.MotherDetails),
				SpouseDetails = MapToFamilyMemberDto(value.SpouseDetails)
			};

	private static EmployeeApplicationFamilyMemberDto? MapToFamilyMemberDto(
		EmployeeApplicationFamilyMemberSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationFamilyMemberDto
			{
				Name = value.Name,
				Occupation = value.Occupation,
				MobileNo = value.MobileNo,
				Address1 = value.Address1,
				Address2 = value.Address2,
				Address3 = value.Address3,
				Country = MapToLocation(value.Country),
				State = MapToLocation(value.State),
				City = value.City,
				Pincode = value.Pincode
			};

	private static EmployeeApplicationAdditionalInfoDto? MapToAdditionalInfoDto(
		EmployeeApplicationAdditionalInfoSnapshot? value) =>
		value == null
			? null
			: new EmployeeApplicationAdditionalInfoDto
			{
				PersonWithDisability = value.PersonWithDisability,
				SecondLanguage = value.SecondLanguage,
				IsLibraryMember = value.IsLibraryMember,
				IsTransportMember = value.IsTransportMember
			};

	private static Country? MapToCountry(EmployeeApplicationLocationInfo? value) =>
		value == null || string.IsNullOrWhiteSpace(value.Name)
			? null
			: new Country(value.Name, value.IsoCode);

	private static State? MapToState(EmployeeApplicationLocationInfo? value) =>
		value == null || string.IsNullOrWhiteSpace(value.Name)
			? null
			: new State(value.Name, value.IsoCode);

	private static EmployeeApplicationWorkExperienceDto MapToWorkExperienceDto(WorkExperience experience)
	{
		var (city, state, country) = ParseWorkExperienceAddress(experience.OrganizationAddress);
		return new EmployeeApplicationWorkExperienceDto
		{
			MediumStageName = experience.OrganizationName,
			Designation = experience.Designation,
			Department = experience.Department,
			City = city,
			State = state,
			Country = country,
			StartDate = experience.FromDate?.ToDateTime(TimeOnly.MinValue),
			EndDate = experience.ToDate?.ToDateTime(TimeOnly.MinValue)
		};
	}

	private static WorkExperience MapToWorkExperience(EmployeeApplicationWorkExperienceDto input)
	{
		return new WorkExperience(
			input.MediumStageName,
			BuildAddress(input),
			input.StartDate.HasValue ? DateOnly.FromDateTime(input.StartDate.Value) : null,
			input.EndDate.HasValue ? DateOnly.FromDateTime(input.EndDate.Value) : null,
			input.Department,
			input.Designation,
			null,
			null);
	}

	private static string? BuildAddress(EmployeeApplicationWorkExperienceDto input)
	{
		var parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(input.City))
		{
			parts.Add(input.City);
		}

		if (!string.IsNullOrWhiteSpace(input.State?.Name))
		{
			parts.Add(FormatLocation(input.State));
		}

		if (!string.IsNullOrWhiteSpace(input.Country?.Name))
		{
			parts.Add(FormatLocation(input.Country));
		}

		return parts.Count == 0 ? null : string.Join(", ", parts);
	}

	private static string FormatLocation(EmployeeApplicationLocationInfo location) =>
		string.IsNullOrWhiteSpace(location.IsoCode)
			? location.Name
			: $"{location.Name} ({location.IsoCode})";

	private static (string? city, EmployeeApplicationLocationInfo? state, EmployeeApplicationLocationInfo? country)
		ParseWorkExperienceAddress(string? address)
	{
		if (string.IsNullOrWhiteSpace(address))
		{
			return (null, null, null);
		}

		var parts = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var city = parts.Length > 0 ? parts[0] : null;
		var state = parts.Length > 1 ? ParseLocationPart(parts[1]) : null;
		var country = parts.Length > 2 ? ParseLocationPart(parts[2]) : null;
		return (city, state, country);
	}

	private static EmployeeApplicationLocationInfo? ParseLocationPart(string? part)
	{
		if (string.IsNullOrWhiteSpace(part))
		{
			return null;
		}

		var openParen = part.LastIndexOf('(');
		var closeParen = part.LastIndexOf(')');
		if (openParen > 0 && closeParen > openParen)
		{
			return new EmployeeApplicationLocationInfo
			{
				Name = part[..openParen].Trim(),
				IsoCode = part[(openParen + 1)..closeParen].Trim()
			};
		}

		return new EmployeeApplicationLocationInfo
		{
			Name = part.Trim(),
			IsoCode = string.Empty
		};
	}
}
