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
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.RunPayroll;
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
using Volo.Abp.Uow;
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
	IUnitOfWorkManager unitOfWorkManager,
	IStringEncryptionService stringEncryptionService,
	IEmployeeOnboardingInvitationEmailService invitationEmailService,
	EmployeeOnboardingAcademicScopeAssigner academicScopeAssigner,
	ICompensationRevisionAppService compensationRevisions,
	IRepository<EmployeeCompensationRevision, Guid> compensationRepository,
	IPayrollInputRevisionStore payrollFence,
	EmployeePersonalInfoProjection personalInfoProjection,
	EmployeeActivationReadinessService activationReadiness)
	: ApplicationService, IEmployeeApplicationAppService
{
	private const int TokenExpiryDays = 30;
	private const int MaxApplicationNumberInsertAttempts = 5;
	private const string DefaultPolicyAssignmentReason = "Staff onboarding default policy assignment";
	private const string DefaultPolicyConfirmationTokenMarker = "__employee_onboarding_default_policy_assignment__";

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
				.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id)
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
		var application = await InsertInvitationWithRetryAsync(input, token, now);
		var emailResult = await invitationEmailService.SendInvitationAsync(application, publicPath);
		return CreateInviteResponse(application, token, emailResult);
	}

	private async Task<EmployeeApplication> InsertInvitationWithRetryAsync(
		CreateEmployeeApplicationInviteDto input,
		string token,
		DateTime now)
	{
		for (var attempt = 1; attempt <= MaxApplicationNumberInsertAttempts; attempt++)
		{
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

			try
			{
				using var insertionUnitOfWork = unitOfWorkManager.Begin(
					new AbpUnitOfWorkOptions { IsTransactional = true },
					requiresNew: true);
				application = await applicationRepository.InsertAsync(application, autoSave: true);
				await insertionUnitOfWork.CompleteAsync();
				return application;
			}
			catch (Exception ex) when (
				IsApplicationNumberUniqueConstraintViolation(ex) &&
				attempt < MaxApplicationNumberInsertAttempts)
			{
				Logger.LogWarning(
					ex,
					"Staff application number collision detected. Retrying invitation creation (attempt {Attempt}/{MaxAttempts}).",
					attempt + 1,
					MaxApplicationNumberInsertAttempts);
			}
		}

		throw new InvalidOperationException(
			"Staff application invitation retry loop exited unexpectedly.");
	}

	private async Task EnsureInviteEmailAvailableAsync(string email)
	{
		var normalizedEmail = email.Trim().ToLowerInvariant();
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(employeeQuery.Where(x => x.Email != null && x.Email.ToLower() == normalizedEmail)))
		{
			throw new BusinessException(
					HRErrorCodes.EmployeeApplicationEmailAlreadyInUse,
					"A staff member or open onboarding application already uses this email.")
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
					"A staff member or open onboarding application already uses this email.")
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
		application.RegenerateInvitationToken(
			HashToken(token),
			stringEncryptionService.Encrypt(token),
			now.AddDays(TokenExpiryDays),
			now);
		application = await applicationRepository.UpdateAsync(application, autoSave: true);
		var emailResult = await invitationEmailService.SendInvitationAsync(application, publicPath);
		return CreateInviteResponse(application, token, emailResult);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Finalize)]
	public async Task<EmployeeApplicationFinalizeResultDto> FinalizeAsync(Guid id, FinalizeEmployeeApplicationDto input)
	{
		using var finalizationUnitOfWork = unitOfWorkManager.Begin(
			new AbpUnitOfWorkOptions { IsTransactional = true },
			requiresNew: true);
		// Serialize with salary saves before reading application state. A lost response may be retried safely.
		await payrollFence.LockAsync();
		var application = await GetApplicationAsync(id);
		var salaryRequestKey = $"onboarding:{id:D}";
		if (application.Status == EmployeeApplicationStatus.Finalized && application.FinalizedEmployeeId is { } finalizedEmployeeId)
		{
			if (!await compensationRepository.AnyAsync(x => x.EmployeeId == finalizedEmployeeId && x.IdempotencyKey == salaryRequestKey))
				throw new BusinessException(HRErrorCodes.EmployeeApplicationAlreadyFinalized);
			var completed = new EmployeeApplicationFinalizeResultDto
			{
				Application = MapToDto(application, includePublicPath: true),
				Employee = await personalInfoProjection.ToPersonalInfoDtoAsync(await employeeRepository.GetAsync(finalizedEmployeeId)),
				ActivationReadiness = await activationReadiness.GetAsync(finalizedEmployeeId)
			};
			await finalizationUnitOfWork.CompleteAsync();
			return completed;
		}
		if (input.Salary == null)
			throw new AbpValidationException("Preview and assign salary before finalizing onboarding.");
		if (!application.SubmittedAt.HasValue)
		{
			throw new BusinessException(
				HRErrorCodes.EmployeeApplicationSubmissionRequired,
				"The candidate must submit the onboarding application before HR can finalize it.");
		}

		if (application.Status is EmployeeApplicationStatus.Rejected or EmployeeApplicationStatus.Withdrawn)
		{
			throw new BusinessException(HRErrorCodes.EmployeeApplicationInvalidStatusTransition);
		}

		await EnsureFinalizationPolicyOverrideAsync(input.PersonalInfo);

		var isTeachingEmployee = input.PersonalInfo.EmployeeGroup == EmployeeGroupType.Teaching;
		if (isTeachingEmployee)
		{
			if (!input.AcademicYearId.HasValue ||
			    input.AcademicYearId.Value == Guid.Empty ||
			    input.AcademicScopes is not { Count: > 0 })
			{
				throw new AbpValidationException(
					"Teaching staff academic permission validation failed.");
			}

			await academicScopeAssigner.ValidateAsync(
				input.AcademicYearId.Value,
				input.AcademicScopes);
		}

		var (photoFileName, photoContent) = await ReadProfilePhotoForFinalizationAsync(application, input.UseSubmittedPhoto);
		var employee = await personalInfoCreationWorkflow.CreateInCurrentUnitOfWorkAsync(
			input.PersonalInfo,
			photoFileName,
			photoContent);

		if (isTeachingEmployee)
		{
			if (!employee.UserId.HasValue ||
			    !input.AcademicYearId.HasValue ||
			    input.AcademicScopes is not { Count: > 0 })
			{
				throw new AbpValidationException(
					"Teaching staff academic permission validation failed.");
			}

			await academicScopeAssigner.AssignAsync(
				employee.UserId.Value,
				input.AcademicYearId.Value,
				input.AcademicScopes);
		}

		// Reuse the canonical compensation validation, permissions, statutory snapshot and calculation.
		// All writes enlist in this finalization transaction; no employee survives a failed salary save.
		input.Salary.IdempotencyKey = salaryRequestKey;
		if (input.StatutoryProfile != null)
			input.Salary.StatutoryProfileOverride = input.StatutoryProfile;
		var salary = await compensationRevisions.SaveAsync(employee.Id, input.Salary);
		if (salary.ActivationReadiness.BlockerCodes.Contains("PAYROLL_SETUP_REQUIRED"))
			throw new AbpValidationException("Salary must cover the employee's required activation month.");

		var workExperiences = (input.WorkExperiences.Count > 0
				? input.WorkExperiences
				: application.WorkExperiences
					.Select(EmployeeApplicationFormMapper.ToWorkExperienceDto)
					.ToList())
			.Select(EmployeeApplicationFormMapper.ToWorkExperience)
			.ToList();

		if (workExperiences.Count > 0)
		{
			var updatedEmployee = await employeeManager.UpdateExperienceInfoByEmployeeIdAsync(employee.Id, workExperiences);
			await employeeRepository.UpdateAsync(updatedEmployee, autoSave: true);
		}

		await ApplyFamilyInfoAsync(
			employee.Id,
			input.FamilyInfo ?? EmployeeApplicationFormMapper.ToFamilyInfoDto(application.FamilyInfo));
		await ApplyAdditionalInfoAsync(
			employee.Id,
			input.AdditionalInfo ?? EmployeeApplicationFormMapper.ToAdditionalInfoDto(application.AdditionalInfo));

		if (input.AttachResumeAsDocument)
		{
			await AttachResumeAsync(employee.Id, application);
		}

		application.MarkFinalized(employee.Id, Clock.Now);
		application = await applicationRepository.UpdateAsync(application, autoSave: true);

		var result = new EmployeeApplicationFinalizeResultDto
		{
			Application = MapToDto(application, includePublicPath: true),
			Employee = employee,
			ActivationReadiness = salary.ActivationReadiness
		};
		await finalizationUnitOfWork.CompleteAsync();
		return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View)]
	public async Task<EmployeeApplicationFileDto?> GetResumeAsync(Guid id)
	{
		var application = await GetApplicationAsync(id);
		if (!application.HasResume || application.ResumeBlobName == null)
		{
			return null;
		}

		var fileName = application.ResumeFileName ?? "resume.pdf";
		var (safeFileName, canonicalContentType) = NormalizeStoredUploadMetadata(
			fileName,
			application.ResumeContentType,
			EmployeeApplicationUploadKind.Resume);
		var content = await ReadBlobAsync(application.ResumeBlobName);
		FileValidationService.ValidateDocumentUpload(
			safeFileName,
			content,
			application.ResumeSize ?? content.Length);
		return new EmployeeApplicationFileDto
		{
			FileName = safeFileName,
			ContentType = canonicalContentType,
			Content = content
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

		var fileName = application.PhotoFileName ?? "photo.png";
		var (safeFileName, canonicalContentType) = NormalizeStoredUploadMetadata(
			fileName,
			application.PhotoContentType,
			EmployeeApplicationUploadKind.Photo);
		var content = await ReadBlobAsync(application.PhotoBlobName);
		FileValidationService.ValidateProfilePictureUpload(safeFileName, content);
		return new EmployeeApplicationFileDto
		{
			FileName = safeFileName,
			ContentType = canonicalContentType,
			Content = content
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

			var personalInfo = EmployeeApplicationSnapshotMapper.ToPersonalInfo(input.PersonalInfo);
			var familyInfo = EmployeeApplicationSnapshotMapper.ToFamilyInfo(input.FamilyInfo);
			var additionalInfo = EmployeeApplicationSnapshotMapper.ToAdditionalInfo(input.AdditionalInfo);
			var workExperiences = input.WorkExperiences
				.Select(EmployeeApplicationFormMapper.ToWorkExperience)
				.ToList();

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
		var validationResults = PublicEmployeeApplicationSubmitValidation.Validate(
			input,
			PublicEmployeeApplicationSubmitValidation.GetInstitutionDate(now));
		if (validationResults.Count > 0)
		{
			throw new AbpValidationException("Staff application validation failed.", validationResults.ToList());
		}
	}

	private static void ValidateUploadInputs(
		EmployeeApplicationFileInput? resume,
		EmployeeApplicationFileInput? photo)
	{
		if (resume != null && resume.Content.Length > 0)
		{
			var (fileName, _) = NormalizeUploadMetadata(
				resume.FileName,
				resume.ContentType,
				EmployeeApplicationUploadKind.Resume);
			FileValidationService.ValidateDocumentUpload(fileName, resume.Content, resume.Size);
		}

		if (photo != null && photo.Content.Length > 0)
		{
			var (fileName, _) = NormalizeUploadMetadata(
				photo.FileName,
				photo.ContentType,
				EmployeeApplicationUploadKind.Photo);
			FileValidationService.ValidateProfilePictureUpload(fileName, photo.Content);
		}
	}

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

		var (fileName, _) = NormalizeStoredUploadMetadata(
			application.PhotoFileName ?? "photo.png",
			application.PhotoContentType,
			EmployeeApplicationUploadKind.Photo);
		var content = await ReadBlobAsync(application.PhotoBlobName);
		FileValidationService.ValidateProfilePictureUpload(fileName, content);
		return (fileName, content);
	}

	private async Task AttachResumeAsync(Guid employeeId, EmployeeApplication application)
	{
		if (string.IsNullOrWhiteSpace(application.ResumeBlobName))
		{
			return;
		}

		var content = await ReadBlobAsync(application.ResumeBlobName);
		var (fileName, canonicalContentType) = NormalizeStoredUploadMetadata(
			application.ResumeFileName ?? "resume.pdf",
			application.ResumeContentType,
			EmployeeApplicationUploadKind.Resume);
		FileValidationService.ValidateDocumentUpload(
			fileName,
			content,
			application.ResumeSize ?? content.Length);
		await employeeDocumentManager.AddDocumentsAsync(employeeId, [
			new EmployeeDocumentData
			{
				FileName = fileName,
				ContentType = canonicalContentType,
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

		var (fileName, canonicalContentType) = NormalizeUploadMetadata(
			file.FileName,
			file.ContentType,
			EmployeeApplicationUploadKind.Resume);
		FileValidationService.ValidateDocumentUpload(fileName, file.Content, file.Size);

		var blobName = $"staff-applications/{applicationId}/resume/{GuidGenerator.Create():N}_{fileName}";
		await blobContainer.SaveAsync(blobName, file.Content, overrideExisting: true);
		return new EmployeeApplicationUploadMetadata
		{
			BlobName = blobName,
			FileName = fileName,
			ContentType = canonicalContentType,
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

		var (fileName, canonicalContentType) = NormalizeUploadMetadata(
			file.FileName,
			file.ContentType,
			EmployeeApplicationUploadKind.Photo);
		FileValidationService.ValidateProfilePictureUpload(fileName, file.Content);

		var blobName = $"staff-applications/{applicationId}/photo/{GuidGenerator.Create():N}_{fileName}";
		await blobContainer.SaveAsync(blobName, file.Content, overrideExisting: true);
		return new EmployeeApplicationUploadMetadata
		{
			BlobName = blobName,
			FileName = fileName,
			ContentType = canonicalContentType,
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

	private static (string FileName, string ContentType) NormalizeUploadMetadata(
		string? fileName,
		string? contentType,
		EmployeeApplicationUploadKind kind)
	{
		if (!EmployeeApplicationUploadPolicy.TryNormalize(
			    fileName,
			    contentType,
			    kind,
			    out var safeFileName,
			    out var canonicalContentType))
		{
			throw new BusinessException(
					HRErrorCodes.EmployeeApplicationInvalidUpload,
					"The upload filename, extension, or content type is not allowed.")
				.WithData(
					"allowedExtensions",
					EmployeeApplicationUploadPolicy.GetAllowedExtensions(kind));
		}

		return (safeFileName, canonicalContentType);
	}

	private static (string FileName, string ContentType) NormalizeStoredUploadMetadata(
		string? fileName,
		string? contentType,
		EmployeeApplicationUploadKind kind)
	{
		if (!EmployeeApplicationUploadPolicy.TryNormalizeStored(
			    fileName,
			    contentType,
			    kind,
			    out var safeFileName,
			    out var canonicalContentType))
		{
			throw new BusinessException(
					HRErrorCodes.EmployeeApplicationInvalidUpload,
					"The stored upload filename, extension, or content type is not allowed.")
				.WithData(
					"allowedExtensions",
					EmployeeApplicationUploadPolicy.GetAllowedExtensions(kind));
		}

		return (safeFileName, canonicalContentType);
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
		$"EA-{now:yyyyMMddHHmmss}-{GuidGenerator.Create().ToString("N")[^10..].ToUpperInvariant()}";

	private static bool IsApplicationNumberUniqueConstraintViolation(Exception exception)
	{
		for (var current = exception; current != null; current = current.InnerException)
		{
			var message = current.Message;
			if (message.Contains(
				    "IX_EmployeeApplications_TenantId_ApplicationNumber",
				    StringComparison.OrdinalIgnoreCase) ||
			    message.Contains(
				    "AppEmployeeApplications.TenantId, AppEmployeeApplications.ApplicationNumber",
				    StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

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
			Country = EmployeeApplicationFormMapper.ToLocation(application.Country),
			State = EmployeeApplicationFormMapper.ToLocation(application.State),
			City = application.City,
			PinCode = application.PinCode,
			PersonalInfo = EmployeeApplicationFormMapper.ToPersonalInfoDto(application.PersonalInfo),
			FamilyInfo = EmployeeApplicationFormMapper.ToFamilyInfoDto(application.FamilyInfo),
			AdditionalInfo = EmployeeApplicationFormMapper.ToAdditionalInfoDto(application.AdditionalInfo),
			WorkExperiences = application.WorkExperiences
				.Select(EmployeeApplicationFormMapper.ToWorkExperienceDto)
				.ToList(),
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
				"Staff application {ApplicationId} has an encrypted invite token that could not be decrypted.",
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

}
