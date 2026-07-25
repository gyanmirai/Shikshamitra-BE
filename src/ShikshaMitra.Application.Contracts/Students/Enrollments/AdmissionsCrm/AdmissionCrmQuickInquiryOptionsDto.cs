using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmQuickInquiryChannelOptionDto
{
	public AdmissionInquiryChannel Channel { get; set; }
	public string Label { get; set; } = string.Empty;
	public int DisplayOrder { get; set; }
	public bool IsDefault { get; set; }
}

public class AdmissionCrmQuickInquiryRouteOptionDto
{
	public Guid RouteDefinitionId { get; set; }
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmRouteType RouteType { get; set; }
	public int Version { get; set; }
	public bool IsDefault { get; set; }
}

public class AdmissionCrmQuickInquiryOptionsDto
{
	public Guid AcademicYearId { get; set; }
	public string CycleName { get; set; } = string.Empty;
	public bool IsAdmissionCycleActive { get; set; }
	public bool RegistrationFormRequiredBeforeExam { get; set; }
	public bool EntranceExamRequired { get; set; }
	public bool JourneyLocked { get; set; }
	public IReadOnlyList<AdmissionCrmQuickInquiryChannelOptionDto> Channels { get; set; } = [];
	public IReadOnlyList<AdmissionCrmQuickInquiryRouteOptionDto> Routes { get; set; } = [];
}
