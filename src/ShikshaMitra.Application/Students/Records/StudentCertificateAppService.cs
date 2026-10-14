using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize]
public partial class StudentCertificateAppService(
	IRepository<StudentCertificate, Guid> certificateRepository,
	IRepository<StudentCertificateTemplate, Guid> templateRepository,
	IStudentRepository studentRepository,
	StudentScopeAccessService studentScopeAccessService,
	IRepository<global::ShikshaMitra.TenantSettings.AcademicYears.AcademicYear, Guid> academicYearRepository,
	IRepository<global::ShikshaMitra.TenantSettings.Standards.Standard, Guid> standardRepository,
	IRepository<global::ShikshaMitra.TenantSettings.Divisions.Division, Guid> divisionRepository,
	StudentRecordsAccessService recordsAccess, StudentRecordsPolicyResolver recordsPolicies,
	StudentRecordsEnrollmentScopeService enrollmentScope,
	IRepository<StudentRecordLegacyOverride, Guid> legacyOverrides,
	IRepository<StudentRecordDocumentEvent, Guid> documentEvents,
	IRepository<StudentRecordTransition, Guid> transitions) : ApplicationService, IStudentCertificateAppService
{
	private const int MaxCertificateNumberGenerationAttempts = 3;
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static readonly string[] SupportedPlaceholders =
	[
		"StudentName",
		"GRNo",
		"AdmissionNo",
		"Class",
		"Division",
		"DOB",
		"Gender",
		"CertificateNo",
		"IssueDate",
		"AcademicYear",
		"OrganizationName",
		"Reason",
		"Conduct",
		"FirstTrialDetails"
	];

	public async Task<List<string>> GetSupportedPlaceholdersAsync()
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		await recordsAccess.EnsureActiveActorAsync();
		return SupportedPlaceholders.ToList();
	}

	public async Task<StudentCertificateTemplateDto> GetTemplateAsync(StudentCertificateType certificateType)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		await recordsAccess.EnsureActiveActorAsync();
		ValidateCertificateType(certificateType);
		var template = await GetActiveTemplateEntityAsync(certificateType);
		return template == null
			? BuildDefaultTemplateDto(certificateType)
			: MapTemplate(template);
	}

	public async Task<StudentCertificateTemplateDto> UpsertTemplateAsync(
		StudentCertificateType certificateType,
		StudentCertificateTemplateDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Add);
		await recordsAccess.EnsureActiveActorAsync();
		ValidateCertificateType(certificateType);
		var template = await GetActiveTemplateEntityAsync(certificateType);
		if (template == null)
		{
			template = new StudentCertificateTemplate(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				certificateType,
				input.HeaderContent,
				input.FooterContent,
				input.LogoUrl,
				input.SchoolAddress,
				input.PrincipalSignatureUrl,
				input.SchoolStampUrl,
				input.IsActive);

			await templateRepository.InsertAsync(template, autoSave: true);
			return MapTemplate(template);
		}

		template.Update(
			input.HeaderContent,
			input.FooterContent,
			input.LogoUrl,
			input.SchoolAddress,
			input.PrincipalSignatureUrl,
			input.SchoolStampUrl,
			input.IsActive);

		await templateRepository.UpdateAsync(template, autoSave: true);
		return MapTemplate(template);
	}

	public async Task<StudentCertificateResponseDto> PreviewAsync(StudentCertificatePreviewRequestDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		var student = await LoadScopedStudentAsync(input.StudentId, nameof(PreviewAsync));
		var exitSnapshot = await ValidateIssuanceAsync(student, input);
		var issueDate = input.IssueDate ?? Clock.Now;
		var certificateNo = await GenerateCertificateNumberAsync(input.CertificateType, issueDate.Year, student.AcademicYearId);

		var response = await BuildCertificateResponseAsync(
			student,
			input.CertificateType,
			issueDate,
			StudentCertificateStatus.Draft,
			certificateNo,
			input.CertificateFields,
			certificateId: null,
			templateSnapshot: null);
		response.TransitionId = input.TransitionId;
		if (exitSnapshot != null) response.CertificateData["_ExitTransition"] = exitSnapshot;
		response.Template.Document = StudentCertificateDocumentFactory.Create(response);
		return response;
	}

	public async Task<StudentCertificateResponseDto> GenerateAsync(CreateStudentCertificateDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Add);
		var student = await LoadScopedStudentAsync(input.StudentId, nameof(GenerateAsync));
		await ValidateIssuanceAsync(student, input);
		if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 100) throw new BusinessException("Students:Records:DocumentIdempotencyRequired");
		var issueDate = input.IssueDate ?? Clock.Now;
		return await PersistGeneratedCertificateWithRetryAsync(
			student.Id,
			input.CertificateType,
			issueDate,
			async (certificateNo, certificateId) => await BuildCertificateResponseAsync(
				student,
				input.CertificateType,
				issueDate,
				StudentCertificateStatus.Generated,
				certificateNo,
				input.CertificateFields,
				certificateId,
				templateSnapshot: null), input.IdempotencyKey.Trim(), input.TransitionId);
	}

	public async Task<StudentCertificateResponseDto> GetAsync(Guid certificateId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		var certificate = await LoadScopedCertificateAsync(certificateId, nameof(GetAsync));
		return BuildStoredCertificateResponse(certificate);
	}

	public async Task<List<StudentCertificateHistoryDto>> GetHistoryAsync(Guid studentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		await LoadScopedStudentAsync(studentId, nameof(GetHistoryAsync));

		var queryable = await certificateRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			queryable
				.Where(x => x.StudentId == studentId && x.TenantId == CurrentTenant.Id)
				.OrderByDescending(x => x.CreationTime));

		return rows.Select(x => new StudentCertificateHistoryDto
		{
			CertificateId = x.Id,
			CertificateNumber = x.CertificateNumber,
			CertificateType = x.CertificateType.ToString(),
			IssueDate = x.IssueDate,
			GeneratedBy = x.GeneratedByUserId,
			Status = x.Status.ToString(),
			CreatedDate = x.CreationTime, TransitionId = x.TransitionId, SourceCertificateId = x.SourceCertificateId,
			SupportedActions = x.Status == StudentCertificateStatus.Cancelled ? ["View"] : ["View", "Download", "Reprint", "Duplicate", "Void"]
		}).ToList();
	}

	public async Task<StudentCertificateResponseDto> DuplicateAsync(Guid certificateId, StudentDocumentActionDto? input = null)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Add);
		ValidateDocumentReason(input);
		if (input!.IdempotencyKey != null && (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 100))
			throw new BusinessException("Students:Records:DocumentIdempotencyRequired");
		var source = await LoadScopedCertificateAsync(certificateId, nameof(DuplicateAsync));
		if (source.Status == StudentCertificateStatus.Cancelled) throw new BusinessException("Students:Records:DocumentVoided");
		await ValidateIssuanceAsync(await LoadScopedStudentAsync(source.StudentId, "duplicate certificate"), new() { StudentId = source.StudentId, CertificateType = source.CertificateType, TransitionId = source.TransitionId });
		var issueDate = Clock.Now;
		return await PersistGeneratedCertificateWithRetryAsync(
			source.StudentId,
			source.CertificateType,
			issueDate,
			(certificateNo, certificateId) => Task.FromResult(
				BuildDuplicateCertificateResponse(source, certificateNo, issueDate, certificateId)), input.IdempotencyKey?.Trim() ?? GuidGenerator.Create().ToString("N"), source.TransitionId, source.Id, input.Reason);
	}

	public async Task<StudentCertificatePdfDto> GeneratePdfAsync(Guid certificateId)
	{
		var certificate = await GetAsync(certificateId);
		if (certificate.Status == StudentCertificateStatus.Cancelled.ToString()) throw new BusinessException("Students:Records:DocumentVoided");

		var bytes = StudentCertificatePdfRenderer.Render(certificate);

		return new StudentCertificatePdfDto
		{
			FileName = $"{certificate.CertificateNo}.pdf",
			ContentType = "application/pdf",
			Content = bytes
		};
	}

	private StudentCertificateResponseDto BuildStoredCertificateResponse(StudentCertificate certificate)
	{
		var certificateData = DeserializeDictionary(certificate.CertificateDataJson);
		var template = DeserializeTemplate(certificate.TemplateSnapshotJson) ?? new StudentCertificateTemplateRenderDto();
		var student = BuildStoredStudentDto(certificate.StudentId, certificateData);
		var organization = BuildStoredOrganizationDto(certificate.TenantId, certificateData, template);

		var response = BuildCertificateResponseFromSnapshot(
			certificate.Id,
			certificate.CertificateNumber,
			certificate.CertificateType,
			certificate.IssueDate,
			certificate.Status,
			student,
			organization,
			certificateData,
			template);
		response.TransitionId = certificate.TransitionId;
		response.Template.Document ??= StudentCertificateDocumentFactory.Create(response);
		return response;
	}

	private async Task<StudentCertificateResponseDto> BuildCertificateResponseAsync(
		Student student,
		StudentCertificateType certificateType,
		DateTimeOffset issueDate,
		StudentCertificateStatus status,
		string certificateNo,
		Dictionary<string, string?> inputFields,
		Guid? certificateId,
		StudentCertificateTemplateRenderDto? templateSnapshot)
	{
		var studentDto = await BuildStudentDtoAsync(student);
		var template = templateSnapshot ?? await BuildTemplateRenderDtoAsync(certificateType);
		var organization = BuildOrganizationDto(template);
		var certificateData = BuildCertificateData(inputFields, studentDto, organization, certificateNo, issueDate);

		template.RenderedHeaderContent = RenderTemplate(template.HeaderContent, certificateData);
		template.RenderedFooterContent = RenderTemplate(template.FooterContent, certificateData);

		return new StudentCertificateResponseDto
		{
			CertificateId = certificateId,
			CertificateNo = certificateNo,
			CertificateType = certificateType.ToString(),
			IssueDate = issueDate,
			Status = status.ToString(),
			Student = studentDto,
			Organization = organization,
			CertificateData = certificateData,
			PlaceholderValues = SupportedPlaceholders.ToDictionary(
				placeholder => placeholder,
				placeholder => certificateData.TryGetValue(placeholder, out var value) ? value : null),
			Template = template
		};
	}

	private async Task<StudentCertificateTemplateRenderDto> BuildTemplateRenderDtoAsync(StudentCertificateType certificateType)
	{
		var template = await GetActiveTemplateEntityAsync(certificateType);
		var dto = template == null
			? BuildDefaultTemplateDto(certificateType)
			: MapTemplate(template);

		return new StudentCertificateTemplateRenderDto
		{
			HeaderContent = dto.HeaderContent,
			FooterContent = dto.FooterContent,
			PrincipalSignatureUrl = dto.PrincipalSignatureUrl,
			SchoolStampUrl = dto.SchoolStampUrl,
			LogoUrl = dto.LogoUrl,
			SchoolAddress = dto.SchoolAddress
		};
	}

	private async Task<StudentCertificateTemplate?> GetActiveTemplateEntityAsync(StudentCertificateType certificateType)
	{
		var queryable = await templateRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(
			queryable
				.Where(x => x.CertificateType == certificateType && x.IsActive && x.TenantId == CurrentTenant.Id)
				.OrderByDescending(x => x.CreationTime));
	}

	private async Task<Student> LoadScopedStudentAsync(Guid studentId, string operation)
	{
		var scopedStudent = await recordsAccess.GetScopedStudentAsync(studentId);
		if (scopedStudent.Status != StudentLifeCycleStatusEnum.Enrolled)
			await recordsAccess.EnsureWritableAsync(studentId);
		var policy = (await recordsPolicies.ResolveAsync(scopedStudent.AcademicYearId)).Policy;
		if (policy.IsMasked("Identity", await recordsAccess.GetDatabaseRoleNamesAsync())) throw new BusinessException("Students:Records:DocumentFieldsRestricted");
		var student = await studentRepository.GetInquiryForUpdateAsync(studentId);
		if (student == null || student.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException(StudentErrorCodes.StudentNotFound)
				.WithData("StudentId", studentId);
		}

		await studentScopeAccessService.EnsureCanAccessStudentAsync(student, operation);
		return student;
	}

	private async Task<StudentCertificate> LoadScopedCertificateAsync(Guid certificateId, string operation)
	{
		var certificate = await certificateRepository.FindAsync(certificateId);
		if (certificate == null || certificate.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException(StudentErrorCodes.CertificateNotFound)
				.WithData("CertificateId", certificateId);
		}

		await LoadScopedStudentAsync(certificate.StudentId, operation);
		if ((await recordsPolicies.ResolveAsync(certificate.AcademicYearId)).Policy.IsMasked("Identity", await recordsAccess.GetDatabaseRoleNamesAsync()))
			throw new BusinessException("Students:Records:DocumentFieldsRestricted");
		return certificate;
	}

	private async Task<StudentCertificateResponseDto> PersistGeneratedCertificateWithRetryAsync(
		Guid studentId,
		StudentCertificateType certificateType,
		DateTimeOffset issueDate,
		Func<string, Guid, Task<StudentCertificateResponseDto>> responseFactory,
		string idempotencyKey, Guid? transitionId = null, Guid? sourceCertificateId = null, string? duplicateReason = null)
	{
		for (var attempt = 1; attempt <= MaxCertificateNumberGenerationAttempts; attempt++)
		{
			try
			{
				using var uow = UnitOfWorkManager.Begin(
					new AbpUnitOfWorkOptions { IsTransactional = true },
					requiresNew: true);
				var existing = await certificateRepository.FindAsync(x => x.TenantId == CurrentTenant.Id && x.IdempotencyKey == idempotencyKey);
				if (existing != null)
				{
					if (existing.StudentId != studentId || existing.CertificateType != certificateType || existing.TransitionId != transitionId || existing.SourceCertificateId != sourceCertificateId ||
						(sourceCertificateId.HasValue && DeserializeDictionary(existing.CertificateDataJson).GetValueOrDefault("_DuplicateReason") != duplicateReason?.Trim()))
						throw new BusinessException("Students:Records:DocumentIdempotencyConflict");
					return BuildStoredCertificateResponse(await LoadScopedCertificateAsync(existing.Id, "recover certificate issuance"));
				}
				var student = await LoadScopedStudentAsync(studentId, "issue certificate");
				var exit = await ValidateIssuanceAsync(student, new() { StudentId = studentId, CertificateType = certificateType, TransitionId = transitionId });
				var certificateNo = await GenerateCertificateNumberAsync(certificateType, issueDate.Year, student.AcademicYearId);
				var certificateId = GuidGenerator.Create();
				var response = await responseFactory(certificateNo, certificateId);
				var academicYearId = sourceCertificateId.HasValue ? (await certificateRepository.GetAsync(sourceCertificateId.Value)).AcademicYearId : student.AcademicYearId;
				response.TransitionId = transitionId;
				if (exit != null) response.CertificateData["_ExitTransition"] = exit;
				if (sourceCertificateId.HasValue) response.CertificateData["_DuplicateReason"] = duplicateReason?.Trim();
				response.Template.Document ??= StudentCertificateDocumentFactory.Create(response);
				var certificate = new StudentCertificate(
					certificateId,
					CurrentTenant.Id,
					studentId,
					certificateNo,
					certificateType,
					issueDate,
					CurrentUser.Id,
					StudentCertificateStatus.Generated,
					JsonSerializer.Serialize(response.CertificateData, JsonOptions),
					JsonSerializer.Serialize(response.Template, JsonOptions), academicYearId);

				certificate.RecordIssuanceContext(idempotencyKey, transitionId, sourceCertificateId);
				await certificateRepository.InsertAsync(certificate, autoSave: true);
				await AddDocumentEventAsync(certificate, sourceCertificateId.HasValue ? "Duplicate" : "Issued", duplicateReason);
				await uow.CompleteAsync();
				return response;
			}
			catch (Exception ex) when (IsCertificateNumberRetryablePersistenceFailure(ex))
			{
				if (attempt == MaxCertificateNumberGenerationAttempts)
				{
					throw new BusinessException(
							StudentErrorCodes.CertificateNumberGenerationConflict,
							"Could not generate a unique certificate number. Please retry.")
						.WithData("CertificateType", certificateType)
						.WithData("IssueYear", issueDate.Year);
				}
			}
		}

		throw new BusinessException(StudentErrorCodes.CertificateNumberGenerationConflict);
	}

	private async Task<string> GenerateCertificateNumberAsync(StudentCertificateType certificateType, int year, Guid? academicYearId)
	{
		var policy = (await recordsPolicies.ResolveAsync(academicYearId)).Policy;
		var prefix = $"{policy.Documents.CertificateNumberPrefix}-{GetCertificatePrefix(certificateType)}-{year}-";
		var queryable = await certificateRepository.GetQueryableAsync();
		var numbers = await AsyncExecuter.ToListAsync(
			queryable
				.Where(x =>
					x.TenantId == CurrentTenant.Id &&
					x.CertificateType == certificateType &&
					x.CertificateNumber.StartsWith(prefix))
				.Select(x => x.CertificateNumber));

		var max = numbers
			.Select(x => int.TryParse(x[prefix.Length..], out var sequence) ? sequence : 0)
			.DefaultIfEmpty(0)
			.Max();

		return $"{prefix}{max + 1:000000}";
	}

	private async Task<StudentCertificateStudentDto> BuildStudentDtoAsync(Student student)
	{
		var name = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
			.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
		var academicYear = student.AcademicYearId.HasValue
			? await academicYearRepository.FindAsync(student.AcademicYearId.Value)
			: null;
		var standard = student.StandardId.HasValue
			? await standardRepository.FindAsync(student.StandardId.Value)
			: null;
		var division = student.DivisionId.HasValue
			? await divisionRepository.FindAsync(student.DivisionId.Value)
			: null;

		return new StudentCertificateStudentDto
		{
			StudentId = student.Id,
			StudentName = name,
			GrNo = student.GrNumber ?? student.GrNo?.ToString(),
			AdmissionNo = student.AdditionalDetail?.RegistrationFormNo,
			AcademicYearName = academicYear?.Name,
			ClassName = standard?.Name,
			DivisionName = division?.Name,
			DateOfBirth = student.AdditionalDetail?.DateOfBirth,
			Gender = student.AdditionalDetail?.Gender?.ToString()
		};
	}

	private StudentCertificateOrganizationDto BuildOrganizationDto(StudentCertificateTemplateRenderDto? templateSnapshot) =>
		new()
		{
			OrganizationId = CurrentTenant.Id,
			OrganizationName = CurrentTenant.Name ?? "ShikshaMitra",
			LogoUrl = templateSnapshot?.LogoUrl,
			Address = templateSnapshot?.SchoolAddress,
			PhoneNumber = null
		};

	private StudentCertificateResponseDto BuildDuplicateCertificateResponse(
		StudentCertificate source,
		string certificateNo,
		DateTimeOffset issueDate,
		Guid certificateId)
	{
		var certificateData = DeserializeDictionary(source.CertificateDataJson);
		certificateData["CertificateNo"] = certificateNo;
		certificateData["IssueDate"] = FormatDate(issueDate);

		var template = DeserializeTemplate(source.TemplateSnapshotJson) ?? new StudentCertificateTemplateRenderDto();
		RenderTemplateSnapshot(template, certificateData);

		var student = BuildStoredStudentDto(source.StudentId, certificateData);
		var organization = BuildStoredOrganizationDto(source.TenantId, certificateData, template);

		return BuildCertificateResponseFromSnapshot(
			certificateId,
			certificateNo,
			source.CertificateType,
			issueDate,
			StudentCertificateStatus.Generated,
			student,
			organization,
			certificateData,
			template);
	}

	private static StudentCertificateResponseDto BuildCertificateResponseFromSnapshot(
		Guid? certificateId,
		string certificateNo,
		StudentCertificateType certificateType,
		DateTimeOffset issueDate,
		StudentCertificateStatus status,
		StudentCertificateStudentDto student,
		StudentCertificateOrganizationDto organization,
		Dictionary<string, string?> certificateData,
		StudentCertificateTemplateRenderDto template)
	{
		return new StudentCertificateResponseDto
		{
			CertificateId = certificateId,
			CertificateNo = certificateNo,
			CertificateType = certificateType.ToString(),
			IssueDate = issueDate,
			Status = status.ToString(),
			Student = student,
			Organization = organization,
			CertificateData = certificateData,
			PlaceholderValues = SupportedPlaceholders.ToDictionary(
				placeholder => placeholder,
				placeholder => certificateData.TryGetValue(placeholder, out var value) ? value : null),
			Template = template
		};
	}

	private static StudentCertificateStudentDto BuildStoredStudentDto(
		Guid studentId,
		IReadOnlyDictionary<string, string?> certificateData) =>
		new()
		{
			StudentId = studentId,
			StudentName = GetCertificateDataValue(certificateData, "StudentName") ?? string.Empty,
			GrNo = GetCertificateDataValue(certificateData, "GRNo"),
			AdmissionNo = GetCertificateDataValue(certificateData, "AdmissionNo"),
			AcademicYearName = GetCertificateDataValue(certificateData, "AcademicYear"),
			ClassName = GetCertificateDataValue(certificateData, "Class"),
			DivisionName = GetCertificateDataValue(certificateData, "Division"),
			DateOfBirth = ParseDate(GetCertificateDataValue(certificateData, "DOB")),
			Gender = GetCertificateDataValue(certificateData, "Gender")
		};

	private StudentCertificateOrganizationDto BuildStoredOrganizationDto(
		Guid? tenantId,
		IReadOnlyDictionary<string, string?> certificateData,
		StudentCertificateTemplateRenderDto template) =>
		new()
		{
			OrganizationId = tenantId,
			OrganizationName = GetCertificateDataValue(certificateData, "OrganizationName") ?? CurrentTenant.Name ?? "ShikshaMitra",
			LogoUrl = template.LogoUrl,
			Address = template.SchoolAddress,
			PhoneNumber = null
		};

	private static Dictionary<string, string?> BuildCertificateData(
		Dictionary<string, string?> inputFields,
		StudentCertificateStudentDto student,
		StudentCertificateOrganizationDto organization,
		string certificateNo,
		DateTimeOffset issueDate)
	{
		var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		foreach (var field in inputFields)
		{
			data[field.Key] = string.IsNullOrWhiteSpace(field.Value) ? null : field.Value!.Trim();
		}

		data["StudentName"] = student.StudentName;
		data["GRNo"] = student.GrNo;
		data["AdmissionNo"] = student.AdmissionNo;
		data["Class"] = student.ClassName;
		data["Division"] = student.DivisionName;
		data["DOB"] = FormatDate(student.DateOfBirth);
		data["Gender"] = student.Gender;
		data["CertificateNo"] = certificateNo;
		data["IssueDate"] = FormatDate(issueDate);
		data["AcademicYear"] = student.AcademicYearName;
		data["OrganizationName"] = organization.OrganizationName;
		data.TryAdd("Reason", null);

		return data;
	}

	private static StudentCertificateTemplateDto BuildDefaultTemplateDto(StudentCertificateType certificateType) =>
		new()
		{
			CertificateType = certificateType,
			HeaderContent = StudentCertificateTemplate.DefaultHeader(certificateType),
			FooterContent = StudentCertificateTemplate.DefaultFooter(),
			IsActive = true
		};

	private static StudentCertificateTemplateDto MapTemplate(StudentCertificateTemplate template) =>
		new()
		{
			Id = template.Id,
			CertificateType = template.CertificateType,
			HeaderContent = template.HeaderContent,
			FooterContent = template.FooterContent,
			LogoUrl = template.LogoUrl,
			SchoolAddress = template.SchoolAddress,
			PrincipalSignatureUrl = template.PrincipalSignatureUrl,
			SchoolStampUrl = template.SchoolStampUrl,
			IsActive = template.IsActive
		};

	private static string RenderTemplate(string content, Dictionary<string, string?> values)
	{
		var rendered = content;
		foreach (var placeholder in values)
		{
			rendered = rendered.Replace(
				"{{" + placeholder.Key + "}}",
				placeholder.Value ?? string.Empty,
				StringComparison.OrdinalIgnoreCase);
		}

		return rendered;
	}

	private static void RenderTemplateSnapshot(
		StudentCertificateTemplateRenderDto template,
		Dictionary<string, string?> values)
	{
		template.RenderedHeaderContent = RenderTemplate(template.HeaderContent, values);
		template.RenderedFooterContent = RenderTemplate(template.FooterContent, values);
	}

	private static Dictionary<string, string?> DeserializeDictionary(string json) =>
		JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions) ?? new Dictionary<string, string?>();

	private static StudentCertificateTemplateRenderDto? DeserializeTemplate(string json) =>
		JsonSerializer.Deserialize<StudentCertificateTemplateRenderDto>(json, JsonOptions);

	private static string GetCertificatePrefix(StudentCertificateType certificateType) => certificateType switch
	{
		StudentCertificateType.Bonafide => "BON",
		StudentCertificateType.Character => "CHA",
		StudentCertificateType.Leaving => "LC",
		StudentCertificateType.FirstTrial => "FT",
		_ => "CERT"
	};

	private static string? GetCertificateDataValue(IReadOnlyDictionary<string, string?> values, string key) =>
		values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
			? value
			: null;

	private static DateTimeOffset? ParseDate(string? value) =>
		DateTimeOffset.TryParse(value, out var parsed)
			? parsed
			: null;

	private static bool IsCertificateNumberRetryablePersistenceFailure(Exception exception)
	{
		const string tableHint = "AppStudentCertificates";
		const string indexHint = "IX_AppStudentCertificates_TenantId_CertificateType_Certificate";

		for (var ex = exception; ex != null; ex = ex.InnerException!)
		{
			var message = ex.Message;
			if (string.IsNullOrEmpty(message))
			{
				continue;
			}

			if (message.Contains(indexHint, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase) &&
			    message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase) &&
			    message.Contains("unique constraint failed", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (message.Contains("23505", StringComparison.Ordinal) &&
			    (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase) ||
			     message.Contains(indexHint, StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}

			if (message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
			    message.Contains("database table is locked", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static string? FormatDate(DateTimeOffset? value) =>
		value?.ToString("yyyy-MM-dd");
}
