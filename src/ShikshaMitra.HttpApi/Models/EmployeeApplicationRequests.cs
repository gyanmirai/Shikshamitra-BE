using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.Employees.PersonalInfo;
using Volo.Abp.Validation;

namespace ShikshaMitra.Models;

public class PublicEmployeeApplicationSubmitRequest
{
	[Required]
	public string Data { get; set; } = string.Empty;

	[DisableValidation]
	public IFormFile? Resume { get; set; }

	[DisableValidation]
	public IFormFile? Photo { get; set; }
}

public class EmployeeApplicationFinalizeRequest
{
	[Required]
	public PersonalInfoRequest PersonalInfo { get; set; } = new();

	public List<EmployeeApplicationWorkExperienceDto> WorkExperiences { get; set; } = [];
	public bool UseSubmittedPhoto { get; set; } = true;
	public bool AttachResumeAsDocument { get; set; } = true;
}
