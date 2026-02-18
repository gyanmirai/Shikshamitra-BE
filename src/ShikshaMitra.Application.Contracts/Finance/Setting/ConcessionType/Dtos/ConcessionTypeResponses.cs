using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Setting.ConcessionType;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType.Dtos;

public class ConcessionTypeResponse
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	// public ConcessionBasedOn BasedOn { get; set; }
	// public decimal Amount { get; set; }
	// public List<FeeHeadRef> ApplicableFeeHeads { get; set; }
	public bool IsDocumentRequired { get; set; }
	public List<ConcessionDocumentDto> Documents { get; set; }
	public string? Description { get; set; }
}

public class ConcessionTypePaginatedResponse
{
	public long TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<ConcessionTypeListItem> Items { get; set; }
}

public class ConcessionTypeListItem
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	// public string BasedOn { get; set; } // Enum as string per spec "FIXED"/"PERCENTAGE"
	// public decimal Amount { get; set; }
	public bool IsDocumentRequired { get; set; }
	// public List<FeeHeadRef> ApplicableFeeHeads { get; set; }
}

public class MutationResponse
{
	public Guid? Id { get; set; }
	public string Message { get; set; }
}
