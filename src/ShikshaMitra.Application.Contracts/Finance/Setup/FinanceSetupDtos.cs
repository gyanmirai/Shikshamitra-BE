using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Setup;

public class FinanceSetupProgressInput
{
	[Required, StringLength(64)] public string ConcurrencyStamp { get; set; } = "";
	[Required, StringLength(24)] public string CurrentStageKey { get; set; } = "accounts";
	[StringLength(32)] public string? CurrentSettingKey { get; set; }
	[Required, MaxLength(12)] public string[] DeferredSettingKeys { get; set; } = [];
	public bool FinishReview { get; set; }
}

public class FinanceSetupStateDto
{
	public Guid AcademicYearId { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public string CurrentStageKey { get; set; } = "accounts";
	public string? CurrentSettingKey { get; set; }
	public string[] DeferredSettingKeys { get; set; } = [];
	public DateTime? ReviewedAt { get; set; }
	public List<FinanceSetupSettingStateDto> Settings { get; set; } = [];
}

public class FinanceSetupSettingStateDto
{
	public string Key { get; set; } = "";
	public bool CanView { get; set; }
	public bool CanManage { get; set; }
	public string ConfigurationState { get; set; } = "unavailable";
}
