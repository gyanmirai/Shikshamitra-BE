using System;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.HR.Payroll.Templates;

namespace ShikshaMitra.HR.Payroll.Compensation;

public class PreviewProtectionMigrationDto
{
	public Guid SourceRevisionId { get; set; }
	public DateTime EffectiveMonth { get; set; }
}

public class ApplyProtectionMigrationDto : PreviewProtectionMigrationDto
{
	[Volo.Abp.Auditing.DisableAuditing]
	[Required, MaxLength(8192)]
	public string PreviewToken { get; set; } = string.Empty;
}

public class ProtectionMigrationPreviewDto
{
	public Guid SourceRevisionId { get; set; }
	public string PreviousPolicy { get; set; } = string.Empty;
	public decimal PreviousGross { get; set; }
	public decimal PreviousNet { get; set; }
	public decimal PreviousEmployerCost { get; set; }
	public SalaryTemplatePreviewDto Preview { get; set; } = new();
}
