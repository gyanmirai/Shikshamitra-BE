using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Setup;

public class AcademicSetupVisitStatusDto
{
	public bool HasVisitedConfiguration { get; set; }
	public bool CanAccessSetup { get; set; }
	public DateTime? FirstVisitedAt { get; set; }
}

public class AcademicSetupVisitInput
{
	[StringLength(32)] public string? SettingKey { get; set; }
}

public class AcademicSetupVisitDto
{
	public bool FirstVisit { get; set; }
	public bool HasVisitedConfiguration { get; set; }
	public DateTime? FirstVisitedAt { get; set; }
}

public class AcademicSetupContextInput
{
	public Guid? AcademicMediumId { get; set; }
	public Guid? StandardId { get; set; }
}

public class AcademicSetupProgressInput : AcademicSetupContextInput
{
	[Required, StringLength(64)] public string ConcurrencyStamp { get; set; } = "";
	[Required, StringLength(24)] public string CurrentStageKey { get; set; } = "welcome";
	[StringLength(32)] public string? CurrentSettingKey { get; set; }
	[Required, MaxLength(9)] public string[] DeferredSettingKeys { get; set; } = [];
	public bool FinishReview { get; set; }
}

public class AcademicSetupStateDto
{
	public Guid AcademicYearId { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public string CurrentStageKey { get; set; } = "welcome";
	public string? CurrentSettingKey { get; set; }
	public string[] DeferredSettingKeys { get; set; } = [];
	public DateTime? ReviewedAt { get; set; }
	public List<AcademicSetupSettingStateDto> Settings { get; set; } = [];
}

public class AcademicSetupSettingStateDto
{
	public string Key { get; set; } = "";
	public bool CanView { get; set; }
	public bool CanManage { get; set; }
	public string ConfigurationState { get; set; } = "unavailable";
}
