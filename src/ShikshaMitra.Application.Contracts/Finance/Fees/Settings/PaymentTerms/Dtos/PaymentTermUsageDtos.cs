using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;

public class PaymentTermUsageDto
{
	public Guid PaymentTermId { get; set; }
	public Guid? VersionId { get; set; }
	public int FeeHeadCount { get; set; }
	public int FeeStructureCount { get; set; }
	public int ClassCount { get; set; }
	public List<PaymentTermUsageAssignmentDto> Assignments { get; set; } = new();
}

public class PaymentTermUsageAssignmentDto
{
	public Guid ClassId { get; set; }
	public string ClassName { get; set; } = string.Empty;
	public Guid FeeStructureId { get; set; }
	public string FeeStructureName { get; set; } = string.Empty;
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; } = string.Empty;
	public Guid? VersionId { get; set; }
	public int? VersionNumber { get; set; }
}

public class PaymentTermDefaultImpactDto
{
	public Guid? CurrentDefaultId { get; set; }
	public string? CurrentDefaultName { get; set; }
	public int? CurrentDefaultVersionNumber { get; set; }
	public PaymentTermUsageDto? CurrentDefaultUsage { get; set; }
	public bool ReplacesExistingDefault { get; set; }
	public string AssignmentNote { get; set; } = "Existing fee structures keep their assigned term. The new default applies only to new fee head assignments.";
}
