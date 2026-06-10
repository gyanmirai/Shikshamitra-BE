using System;

namespace ShikshaMitra.HR.Reports;

internal sealed class PolicyReportProjection
{
	public string RecordType { get; init; } = string.Empty;
	public string Entity { get; init; } = string.Empty;
	public string Action { get; init; } = string.Empty;
	public string Target { get; init; } = string.Empty;
	public DateTime? EffectiveFrom { get; init; }
	public DateTime? EffectiveTo { get; init; }
	public string? IsActive { get; init; }
	public DateTime? OccurredAt { get; init; }
	public string? Summary { get; init; }
	public DateTime SortDate { get; init; }
}
