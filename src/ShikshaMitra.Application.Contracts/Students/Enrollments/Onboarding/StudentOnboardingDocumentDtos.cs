using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingDocumentsDto
{
	public Guid CaseId { get; set; }
	public Guid StudentId { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public bool CanUpload { get; set; }
	public bool CanOverride { get; set; }
	public List<StudentOnboardingDocumentDto> Documents { get; set; } = [];
}

public class StudentOnboardingDocumentDto
{
	public Guid DocumentTypeId { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool IsRequired { get; set; }
	public double MaxSizeMb { get; set; }
	public List<string> AllowedExtensions { get; set; } = [];
	public Guid? StudentDocumentId { get; set; }
	public string? FileName { get; set; }
	public bool HasStudentUpload { get; set; }
	public bool HasAcceptedAdmissionEvidence { get; set; }
	public bool IsValidated { get; set; }
	public bool IsOverridden { get; set; }
	public string? OverrideReason { get; set; }
}

public class StudentOnboardingDocumentOverrideInput
{
	public List<Guid> DocumentTypeIds { get; set; } = [];
	public bool OverrideAll { get; set; }
	[Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class StudentOnboardingDocumentUploadInput
{
	[Required] public Guid DocumentTypeId { get; set; }
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
	[Required] public IRemoteStreamContent File { get; set; } = null!;
}

public interface IStudentOnboardingDocumentAppService : IApplicationService
{
	Task<StudentOnboardingDocumentsDto> GetAsync(Guid caseId);
	Task<StudentOnboardingDocumentsDto> UploadAsync(Guid caseId, StudentOnboardingDocumentUploadInput input);
	Task<StudentOnboardingDocumentsDto> OverrideAsync(Guid caseId, StudentOnboardingDocumentOverrideInput input);
}
