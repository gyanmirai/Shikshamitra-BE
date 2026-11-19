using System;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

/// <summary>A source attachment and the current snapshot selected for an independent copy.</summary>
public class FeeStructureCopyPolicyDto
{
	public string Family { get; set; } = string.Empty;
	public Guid PolicyId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public Guid? SourceVersionId { get; set; }
	public int? SourceVersionNumber { get; set; }
	public Guid? CurrentVersionId { get; set; }
	public int? CurrentVersionNumber { get; set; }
	public string? Error { get; set; }
}
