using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryCaptureContextDto
{
	public DateOnly? AcademicYearStartDate { get; set; }
	public int? MinimumAgeYears { get; set; }
	public AdmissionOfferingDto Offering { get; set; } = new();
	public AdmissionFormDto? Form { get; set; }
	public AdmissionInquiryPolicy? Policy { get; set; }
	public AdmissionOfferingAvailabilityDto Availability { get; set; } = new();
}

public class AdmissionInquiryCapturePeriodDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

public class AdmissionInquiryCaptureCatalogueDto
{
	public List<AdmissionInquiryCapturePeriodDto> Periods { get; set; } = [];
	public List<PublicAdmissionInquiryAcademicOptionDto> AcademicOptions { get; set; } = [];
	public List<AdmissionOfferingListDto> Offerings { get; set; } = [];
}
