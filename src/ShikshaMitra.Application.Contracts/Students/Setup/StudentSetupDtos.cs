using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Setup;

public class StudentSetupVisitStatusDto
{
	public bool HasVisitedConfiguration { get; set; }
	public bool CanAccessSetup { get; set; }
	public DateTime? FirstVisitedAt { get; set; }
}

public class StudentSetupVisitInput
{
	[StringLength(32)] public string? SettingKey { get; set; }
}

public class StudentSetupVisitDto
{
	public bool FirstVisit { get; set; }
	public bool HasVisitedConfiguration { get; set; }
	public DateTime? FirstVisitedAt { get; set; }
}

public class StudentSetupContextInput
{
	public Guid? AcademicMediumId { get; set; }
	public Guid? StandardId { get; set; }
}

public class StudentSetupProgressInput : StudentSetupContextInput
{
	[Required, StringLength(64)] public string ConcurrencyStamp { get; set; } = "";
	[Required, StringLength(24)] public string CurrentStageKey { get; set; } = "welcome";
	[StringLength(32)] public string? CurrentSettingKey { get; set; }
	[Required, MaxLength(9)] public string[] DeferredSettingKeys { get; set; } = [];
	public bool FinishReview { get; set; }
}

public class StudentSetupStateDto
{
	public Guid AcademicYearId { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public string CurrentStageKey { get; set; } = "welcome";
	public string? CurrentSettingKey { get; set; }
	public string[] DeferredSettingKeys { get; set; } = [];
	public DateTime? ReviewedAt { get; set; }
	public List<StudentSetupSettingStateDto> Settings { get; set; } = [];
}

public class StudentSetupSettingStateDto
{
	public string Key { get; set; } = "";
	public bool CanView { get; set; }
	public bool CanManage { get; set; }
	public string ConfigurationState { get; set; } = "unavailable";
	public string? ConfigurationSource { get; set; }
}
