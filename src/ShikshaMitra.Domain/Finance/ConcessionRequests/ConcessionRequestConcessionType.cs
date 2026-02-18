using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Step 2 — links a ConcessionRequest to selected ConcessionTypes.
/// </summary>
public class ConcessionRequestConcessionType : Entity<Guid>
{
	public Guid ConcessionRequestId { get; private set; }
	public Guid ConcessionTypeId { get; private set; }

	// Navigation
	public virtual ICollection<ConcessionRequestFeeHeadMapping> FeeHeadMappings { get; private set; }

	protected ConcessionRequestConcessionType()
	{
		FeeHeadMappings = new List<ConcessionRequestFeeHeadMapping>();
	}

	public ConcessionRequestConcessionType(
		Guid id,
		Guid concessionRequestId,
		Guid concessionTypeId) : base(id)
	{
		ConcessionRequestId = concessionRequestId;
		ConcessionTypeId = concessionTypeId;
		FeeHeadMappings = new List<ConcessionRequestFeeHeadMapping>();
	}
}
