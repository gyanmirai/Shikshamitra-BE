using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadRoutingSettingsDto
{
	public bool IsEnabled { get; set; }
	public Guid? DefaultOwnerUserId { get; set; }
	public AdmissionCrmLeadRoutingStrategy RoutingStrategy { get; set; }
	public List<Guid> OwnerPoolUserIds { get; set; } = [];
	public List<AdmissionCrmLeadRoutingRuleDto> Rules { get; set; } = [];

	public static AdmissionCrmLeadRoutingSettingsDto CreateDefault()
	{
		return new AdmissionCrmLeadRoutingSettingsDto();
	}
}
