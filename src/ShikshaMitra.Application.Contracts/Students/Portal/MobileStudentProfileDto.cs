using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Portal;

public class MobileStudentProfileDto
{
	public Guid StudentId { get; set; }
	public MobileProfileYearDto? AcademicYear { get; set; }
	public MobileProfileSummaryDto Summary { get; set; } = new();
	public List<MobileProfileFormDto> FormSources { get; set; } = [];
}
public record MobileProfileYearDto(Guid Id, string Name);
public class MobileProfileSummaryDto
{
	public string Name { get; set; } = "";
	public string? GrNumber { get; set; }
	public string? StandardName { get; set; }
	public string? DivisionName { get; set; }
	public string? MediumName { get; set; }
}
public class MobileProfileFormDto
{
	public Guid SubmissionId { get; set; }
	public string FormName { get; set; } = "";
	public int FormVersion { get; set; }
	public AdmissionFormDto Definition { get; set; } = new();
	public Dictionary<string, JsonElement> CurrentValues { get; set; } = [];
	public List<string> FormMaskedFieldCodes { get; set; } = [];
	public Dictionary<string, List<AdmissionFormOptionDto>> OptionsByField { get; set; } = [];
	public Dictionary<string, List<AdmissionFormOptionDto>> OptionsByFieldPath { get; set; } = [];
}
public interface IMobileStudentProfileAppService : IApplicationService
{
	Task<MobileStudentProfileDto> GetAsync(Guid studentId, Guid? academicYearId = null);
}
