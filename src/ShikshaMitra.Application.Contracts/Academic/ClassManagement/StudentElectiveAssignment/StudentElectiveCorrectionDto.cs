using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class StudentElectiveCorrectionInput
{
	public Guid AcademicMediumId { get; set; }
	public Guid ClassId { get; set; }
	public Guid DivisionId { get; set; }
	public Guid ElectiveGroupId { get; set; }
	public Guid StudentId { get; set; }
	public Guid SelectedSubjectId { get; set; }
	public Guid? PreferredSubjectId { get; set; }
	[StringLength(1000)]
	public string? Reason { get; set; }
}

public class ConfirmStudentElectiveCorrectionInput : StudentElectiveCorrectionInput
{
	[Required]
	public string ReviewToken { get; set; } = "";
}

public class StudentElectiveCorrectionReviewDto
{
	public string ReviewToken { get; set; } = "";
	public DateTime ExpiresAt { get; set; }
	public string StudentName { get; set; } = "";
	public string PreviousSubject { get; set; } = "";
	public string SelectedSubject { get; set; } = "";
	public string? PreviousPreference { get; set; }
	public string? PreferredSubject { get; set; }
	public List<StudentElectiveCorrectionImpactDto> Impacts { get; set; } = [];
}

public class StudentElectiveCorrectionImpactDto
{
	public string Area { get; set; } = "";
	public Guid? RecordId { get; set; }
	public string Description { get; set; } = "";
	public string Severity { get; set; } = "warning";
}

public interface IStudentElectiveCorrectionAppService
{
	Task<StudentElectiveCorrectionReviewDto> PreviewAsync(StudentElectiveCorrectionInput input);
	Task ConfirmAsync(ConfirmStudentElectiveCorrectionInput input);
}
