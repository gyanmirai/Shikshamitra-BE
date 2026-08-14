using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// DTO for PF rule details.
/// </summary>
public class ProvidentFundDto : AuditedEntityDto<Guid>
{
	/// <summary>
	/// Unique PF rule code
	/// </summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>
	/// Human-readable PF rule name
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Employee contribution percentage
	/// </summary>
	public decimal EmployeePercentage { get; set; }

	/// <summary>
	/// Employer contribution percentage
	/// </summary>
	public decimal EmployerPercentage { get; set; }
	public PfContributionBasis ContributionBasis { get; set; }
	public decimal? WageCeiling { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public bool IsActive { get; set; }
	public int Version { get; set; }
}
