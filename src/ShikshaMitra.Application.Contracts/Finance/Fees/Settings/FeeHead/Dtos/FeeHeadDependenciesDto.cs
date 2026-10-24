namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

/// <summary>Current references that must be resolved before removing a master fee head.</summary>
public class FeeHeadDependenciesDto
{
	public int StudentCount { get; set; }
	public int AssignmentCount { get; set; }
	public int FeeStructureCount { get; set; }
	public int PaymentTermCount { get; set; }
	public bool IsBlocked => FeeStructureCount > 0 || AssignmentCount > 0;
}
