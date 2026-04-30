using ShikshaMitra.Finance.Setting;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

public class FeeStructureCreateRequest
{
	[Required]
	public string Name { get; set; }
	[Required]
	public Guid BoardId { get; set; }
	[Required]
	public Guid MediumId { get; set; }
	[Required]
	public Guid ReceiptBookId { get; set; }
}

public class FeeStructureUpdateRequest
{
	[Required]
	public string Name { get; set; }
	[Required]
	public Guid BoardId { get; set; }
	[Required]
	public Guid MediumId { get; set; }
	[Required]
	public Guid ReceiptBookId { get; set; }
}

public class FeeStructureStatusUpdateRequest
{
	public FeeStructureMetadataStatus Status { get; set; }
}

public class StandardFeeStructureSaveRequest
{
	[Required]
	public Guid StandardId { get; set; }
	public List<StandardFeeStructureRequest> StandardFeeStructures { get; set; }
}

public class StandardFeeStructureRequest
{
	public string Name { get; set; }
	public List<FeeHeadAssignmentRequest> FeeHeads { get; set; }
}

public class FeeHeadAssignmentRequest
{
	[Required]
	public Guid FeeHeadId { get; set; }
	public double Amount { get; set; }
	public bool IsOneTime { get; set; }
	public bool IsOptional { get; set; }
}
