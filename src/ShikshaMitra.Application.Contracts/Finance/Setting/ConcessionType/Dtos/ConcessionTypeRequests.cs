using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.Setting.ConcessionType;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType.Dtos;

public class ConcessionTypeCreateRequest
{
	[Required]
	public string Name { get; set; }

	// [Required]
	// public ConcessionBasedOn BasedOn { get; set; }

	// [Required]
	// public decimal Amount { get; set; }

	// [Required]
	// public List<FeeHeadRef> ApplicableFeeHeads { get; set; }

	[Required]
	public bool IsDocumentRequired { get; set; }

	[Required]
	public List<ConcessionDocumentDto> Documents { get; set; }

	public string? Description { get; set; }
}

public class ConcessionTypeUpdateRequest : ConcessionTypeCreateRequest
{
}

public class FeeHeadRef
{
	[Required]
	public Guid Id { get; set; }

	public string? Name { get; set; } // Optional in request, usually ignored or used for validation
}

public class ConcessionDocumentDto
{
	[Required]
	public string DocumentName { get; set; }

	public Guid? Id { get; set; }

	[Required]
	public long DocumentSize { get; set; }

	[Required]
	public List<ConcessionDocumentFormat> AcceptedFormat { get; set; }
}
