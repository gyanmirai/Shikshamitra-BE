using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeCategoryCopyPreviewRequest
{
    public Guid SourceAcademicYearId { get; set; }
    public Guid DestinationAcademicYearId { get; set; }
    public List<Guid> CategoryIds { get; set; } = new();
}

public class FeeCategoryCopyCommitRequest : FeeCategoryCopyPreviewRequest
{
    public List<FeeCategoryCopyResolution> Resolutions { get; set; } = new();
}

public class FeeCategoryCopyResolution
{
    public Guid SourceCategoryId { get; set; }
    public string Action { get; set; } = "Copy";
    public string? NewName { get; set; }
}

public class FeeCategoryCopyPreviewResponse
{
    public List<FeeCategoryCopyPreviewItem> Items { get; set; } = new();
	public int NewCount => Items.Count(x => x.Classification == "New");
	public int IdenticalCount => Items.Count(x => x.Classification == "Identical");
	public int ConflictCount => Items.Count(x => x.Classification == "Conflict");
	public int UnresolvedCount => ConflictCount;
}

public class FeeCategoryCopyPreviewItem
{
    public Guid SourceCategoryId { get; set; }
    public string Name { get; set; }
    public string Classification { get; set; }
    public int FeeHeadCount { get; set; }
	public bool IsActive { get; set; }
	public List<FeeHeadCopyPreviewItem> FeeHeads { get; set; } = new();
}

public class FeeHeadCopyPreviewItem
{
	public Guid SourceFeeHeadId { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public string Module { get; set; }
	public bool IsConcessionApplied { get; set; }
	public bool IsActive { get; set; }
	public string Classification { get; set; }
}

public class FeeCategoryCopyResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
	public int Unresolved { get; set; }
    public List<FeeCategoryResponse> Categories { get; set; } = new();
}
