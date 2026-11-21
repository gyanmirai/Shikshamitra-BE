using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

public class FeeStructureSummaryDto
{
 public Guid Id { get; set; }
 public string Name { get; set; } = string.Empty;
 public FeeStructureStatus Status { get; set; }
 public string ConcurrencyStamp { get; set; } = string.Empty;
 public int RevisionNumber { get; set; }
}

public class FeeStructureCatalogueDto
{
 public List<FeeStructurePolicyOptionDto> Policies { get; set; } = [];
 public List<FeeStructureTermOptionDto> Terms { get; set; } = [];
}
