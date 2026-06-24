using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentCertificateAppService(
	IRepository<StudentCertificate, Guid> certificateRepository,
	IRepository<StudentCertificateTemplate, Guid> templateRepository,
	IStudentRepository studentRepository,
	StudentScopeAccessService studentScopeAccessService,
	IRepository<global::ShikshaMitra.TenantSettings.AcademicYears.AcademicYear, Guid> academicYearRepository,
	IRepository<global::ShikshaMitra.TenantSettings.Standards.Standard, Guid> standardRepository,
	IRepository<global::ShikshaMitra.TenantSettings.Divisions.Division, Guid> divisionRepository) : ApplicationService, IStudentCertificateAppService
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
		"Reason"
	];

	public Task<List<string>> GetSupportedPlaceholdersAsync() =>
		Task.FromResult(SupportedPlaceholders.ToList());

	public async Task<StudentCertificateTemplateDto> GetTemplateAsync(StudentCertificateType certificateType)
	{
		var template = await GetActiveTemplateEntityAsync(certificateType);
		return template == null
			? BuildDefaultTemplateDto(certificateType)
			: MapTemplate(template);
	}

	public async Task<StudentCertificateTemplateDto> UpsertTemplateAsync(
		StudentCertificateType certificateType,
		StudentCertificateTemplateDto input)
	{
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
		var student = await LoadScopedStudentAsync(input.StudentId, nameof(PreviewAsync));
		var issueDate = input.IssueDate ?? Clock.Now;
		var certificateNo = await GenerateCertificateNumberAsync(input.CertificateType, issueDate.Year);

		return await BuildCertificateResponseAsync(
			student,
			input.CertificateType,
			issueDate,
			StudentCertificateStatus.Draft,
			certificateNo,
			input.CertificateFields,
			certificateId: null,
			templateSnapshot: null);
	}

	public async Task<StudentCertificateResponseDto> GenerateAsync(CreateStudentCertificateDto input)
	{
		var student = await LoadScopedStudentAsync(input.StudentId, nameof(GenerateAsync));
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
				templateSnapshot: null));
	}

	public async Task<StudentCertificateResponseDto> GetAsync(Guid certificateId)
	{
		var certificate = await LoadScopedCertificateAsync(certificateId, nameof(GetAsync));
		return BuildStoredCertificateResponse(certificate);
	}

	public async Task<List<StudentCertificateHistoryDto>> GetHistoryAsync(Guid studentId)
	{
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
			CreatedDate = x.CreationTime
		}).ToList();
	}

	public async Task<StudentCertificateResponseDto> DuplicateAsync(Guid certificateId)
	{
		var source = await LoadScopedCertificateAsync(certificateId, nameof(DuplicateAsync));
		var issueDate = Clock.Now;
		return await PersistGeneratedCertificateWithRetryAsync(
			source.StudentId,
			source.CertificateType,
			issueDate,
			(certificateNo, certificateId) => Task.FromResult(
				BuildDuplicateCertificateResponse(source, certificateNo, issueDate, certificateId)));
	}

	public async Task<StudentCertificatePdfDto> GeneratePdfAsync(Guid certificateId)
	{
		var certificate = await GetAsync(certificateId);

		QuestPDF.Settings.License = LicenseType.Community;
		var bytes = Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(48);
				page.DefaultTextStyle(x => x.FontSize(11));
				page.Content().Column(column =>
				{
					column.Spacing(18);
					column.Item().AlignCenter().Text(certificate.Template.RenderedHeaderContent).FontSize(16).SemiBold();
					column.Item().Text($"Student: {certificate.Student.StudentName}");
					column.Item().Text($"GR No: {certificate.Student.GrNo ?? "-"}");
					column.Item().Text($"Admission No: {certificate.Student.AdmissionNo ?? "-"}");
					column.Item().Text($"Class: {certificate.Student.ClassName ?? "-"} {certificate.Student.DivisionName ?? string.Empty}".Trim());
					column.Item().Text($"Date of Birth: {FormatDate(certificate.Student.DateOfBirth)}");
					column.Item().Text($"Gender: {certificate.Student.Gender ?? "-"}");
					column.Item().LineHorizontal(1);
					column.Item().Text(certificate.Template.RenderedFooterContent);
				});
			});
		}).GeneratePdf();

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

		return BuildCertificateResponseFromSnapshot(
			certificate.Id,
			certificate.CertificateNumber,
			certificate.CertificateType,
			certificate.IssueDate,
			certificate.Status,
			student,
			organization,
			certificateData,
			template);
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
		var student = await studentRepository.GetInquiryForUpdateAsync(studentId);
		if (student == null)
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
		if (certificate == null)
		{
			throw new BusinessException(StudentErrorCodes.CertificateNotFound)
				.WithData("CertificateId", certificateId);
		}

		await LoadScopedStudentAsync(certificate.StudentId, operation);
		return certificate;
	}

	private async Task<StudentCertificateResponseDto> PersistGeneratedCertificateWithRetryAsync(
		Guid studentId,
		StudentCertificateType certificateType,
		DateTimeOffset issueDate,
		Func<string, Guid, Task<StudentCertificateResponseDto>> responseFactory)
	{
		for (var attempt = 1; attempt <= MaxCertificateNumberGenerationAttempts; attempt++)
		{
			try
			{
				using var uow = UnitOfWorkManager.Begin(
					new AbpUnitOfWorkOptions { IsTransactional = true },
					requiresNew: true);
				var certificateNo = await GenerateCertificateNumberAsync(certificateType, issueDate.Year);
				var certificateId = GuidGenerator.Create();
				var response = await responseFactory(certificateNo, certificateId);
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
					JsonSerializer.Serialize(response.Template, JsonOptions));

				await certificateRepository.InsertAsync(certificate, autoSave: true);
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

	private async Task<string> GenerateCertificateNumberAsync(StudentCertificateType certificateType, int year)
	{
		var prefix = $"{GetCertificatePrefix(certificateType)}-{year}-";
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
			GrNo = student.GrNo?.ToString(),
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
