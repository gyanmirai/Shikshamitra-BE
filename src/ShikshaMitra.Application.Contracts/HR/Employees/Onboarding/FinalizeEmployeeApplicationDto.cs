using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.HR.Employees.PersonalInfo;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class FinalizeEmployeeApplicationDto
{
	[Required]
	public CreateEmployeePersonalInfoDto PersonalInfo { get; set; } = new();

	public Guid? AcademicYearId { get; set; }
	public List<EmployeeApplicationAcademicScopeDto> AcademicScopes { get; set; } = [];
	public EmployeeApplicationFamilyInfoDto? FamilyInfo { get; set; }
	public EmployeeApplicationAdditionalInfoDto? AdditionalInfo { get; set; }
	public List<EmployeeApplicationWorkExperienceDto> WorkExperiences { get; set; } = [];
	public bool UseSubmittedPhoto { get; set; } = true;
	public bool AttachResumeAsDocument { get; set; } = true;
}
