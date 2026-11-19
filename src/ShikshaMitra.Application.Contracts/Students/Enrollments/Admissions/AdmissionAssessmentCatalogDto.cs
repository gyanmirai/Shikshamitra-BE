using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentCatalogDto
{
	public List<AdmissionAssessmentClassOptionDto> Classes { get; set; } = [];
	public List<AdmissionAssessmentSubjectOptionDto> Subjects { get; set; } = [];
}

public class AdmissionAssessmentClassOptionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid BoardId { get; set; }
	public string BoardName { get; set; } = string.Empty;
	public Guid MediumId { get; set; }
	public string MediumName { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; } = string.Empty;
}

public class AdmissionAssessmentSubjectOptionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid MediumId { get; set; }
	public List<Guid> AssignedStandardIds { get; set; } = [];
}
