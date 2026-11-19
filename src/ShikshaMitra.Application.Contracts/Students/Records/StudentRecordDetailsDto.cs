using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public class StudentRecordDetailsDto
{
	public Guid StudentId { get; set; }
	public string RecordsAccess { get; set; } = "Full";
	public List<StudentRecordStructuredSectionDto> StructuredSections { get; set; } = [];
	public bool CustomDetailsMasked { get; set; }
	public bool CustomDetailsEditable { get; set; }
	public List<StudentRecordRegistrationSourceDto> RegistrationSources { get; set; } = [];
	public List<StudentRecordRegistrationSourceDto> FormSources { get; set; } = [];
}

public class StudentRecordStructuredSectionDto
{
	public string Section { get; set; } = string.Empty;
	public bool HasData { get; set; }
	public bool IsMasked { get; set; }
	public bool CanEdit { get; set; }
	public JsonElement? Data { get; set; }
}

public class StudentRecordAdmissionOnboardingDto
{
	public Guid ApplicationId { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public DateTime? FinalizedAt { get; set; }
	public Guid OnboardingCaseId { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTime? ActivatedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public string CredentialsStatus { get; set; } = "Pending";
}

public class StudentRecordRegistrationSourceDto
{
	public Guid ApplicationId { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public Guid FormInstanceId { get; set; }
	public Guid SubmissionId { get; set; }
	public DateTime SubmittedAt { get; set; }
	public DateTime? FinalizedAt { get; set; }
	public Guid OnboardingCaseId { get; set; }
	public DateTime? ActivatedAt { get; set; }
	public string OnboardingStatus { get; set; } = string.Empty;
	public Guid FormDefinitionId { get; set; }
	public Guid FormFamilyId { get; set; }
	public int FormVersion { get; set; }
	public string FormName { get; set; } = string.Empty;
	public AdmissionFormDto? Definition { get; set; }
	public Dictionary<string, JsonElement> OriginalValues { get; set; } = [];
	public Dictionary<string, JsonElement> CurrentValues { get; set; } = [];
	public bool IsBaseline { get; set; }
	public List<string> EditableFieldCodes { get; set; } = [];
	public List<string> CapturedFieldCodes { get; set; } = [];
	public List<string> FormEditableFieldCodes { get; set; } = [];
	public List<string> FormMaskedFieldCodes { get; set; } = [];
	public Dictionary<string, List<AdmissionFormOptionDto>> OptionsByField { get; set; } = [];
	public Dictionary<string, List<AdmissionFormOptionDto>> OptionsByFieldPath { get; set; } = [];
	public List<StudentRecordRegistrationDifferenceDto> Differences { get; set; } = [];
}

public class StudentRecordRegistrationDifferenceDto
{
	public string FieldCode { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public JsonElement? CurrentValue { get; set; }
	public JsonElement? SubmittedValue { get; set; }
	public bool CanReview { get; set; }
}

public class StudentRecordCustomDetailsPayload
{
	public List<StudentRecordCustomSourcePayload> Sources { get; set; } = [];
}

public class StudentRecordCustomSourcePayload
{
	public Guid ApplicationId { get; set; }
	public Guid FormInstanceId { get; set; }
	public Guid SubmissionId { get; set; }
	public Dictionary<string, JsonElement> Values { get; set; } = [];
}

public interface IStudentRecordDetailsAppService : IApplicationService
{
	Task<StudentRecordDetailsDto> GetAsync(Guid studentId, Guid? academicYearId = null);
	Task<List<AdmissionFormOptionDto>> GetFieldOptionsAsync(Guid studentId, StudentRecordFieldOptionsInput input);
}

public class SaveStudentRecordFormDto
{
	[Required, MaxLength(80)] public string ClientKey { get; set; } = string.Empty;
	[Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
	[Required] public string ExpectedSnapshot { get; set; } = string.Empty;
	public JsonElement Payload { get; set; }
}

public interface IStudentRecordFormAppService : IApplicationService
{
	Task<StudentRecordSectionSnapshotDto> GetSnapshotAsync(Guid studentId);
	Task<StudentRecordChangeDto> SaveAsync(Guid studentId, SaveStudentRecordFormDto input);
}

public class StudentRecordFieldOptionsInput
{
	public Guid SubmissionId { get; set; }
	public string FieldCode { get; set; } = string.Empty;
	public Guid? ParentId { get; set; }
	public string? SearchTerm { get; set; }
	public int SkipCount { get; set; }
	public int MaxResultCount { get; set; } = 20;
	public List<Guid> SelectedIds { get; set; } = [];
	public bool SelectedOnly { get; set; }
}
