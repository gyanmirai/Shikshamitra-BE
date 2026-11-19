using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Timetable.Portal;

public interface IPortalElectivePreferenceAppService : IApplicationService
{
	Task<PortalElectivePreferencesDto> GetAsync(Guid studentId, Guid academicYearId);
	Task<PortalElectivePreferencesDto> SubmitAsync(PortalElectivePreferenceSubmitInput input);
}

public class PortalElectivePreferencesDto
{
	public Guid StudentId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; } = "";
	public string Availability { get; set; } = "available";
	public string? Message { get; set; }
	public bool CanViewTimetable { get; set; }
	public List<PortalElectivePreferenceGroupDto> Groups { get; set; } = [];
}

public class PortalElectivePreferenceGroupDto
{
	public Guid ElectiveGroupId { get; set; }
	public string Name { get; set; } = "";
	public List<PortalElectivePreferenceOptionDto> Options { get; set; } = [];
	public Guid? PreferredSubjectId { get; set; }
	public string? PreferredSubjectName { get; set; }
	public Guid? AssignedSubjectId { get; set; }
	public string? AssignedSubjectName { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public string Status { get; set; } = "selectionRequired";
	public bool CanSubmit { get; set; }
}

public class PortalElectivePreferenceOptionDto
{
	public Guid SubjectId { get; set; }
	public string Name { get; set; } = "";
}

public class PortalElectivePreferenceSubmitInput
{
	public Guid StudentId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid ElectiveGroupId { get; set; }
	public Guid PreferredSubjectId { get; set; }
	public Guid ClientRequestId { get; set; }
}

public class PortalElectivePreferenceSubmitInputValidator : AbstractValidator<PortalElectivePreferenceSubmitInput>
{
	public PortalElectivePreferenceSubmitInputValidator()
	{
		RuleFor(x => x.StudentId).NotEmpty();
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.ElectiveGroupId).NotEmpty();
		RuleFor(x => x.PreferredSubjectId).NotEmpty();
		RuleFor(x => x.ClientRequestId).NotEmpty();
	}
}
