using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Step 3 — maps selected fee heads per concession type for a concession request.
/// </summary>
public class ConcessionRequestFeeHeadMapping : Entity<Guid>
{
	public Guid ConcessionRequestId { get; private set; }
	public Guid ConcessionTypeId { get; private set; }
	public Guid FeeHeadId { get; private set; }

	protected ConcessionRequestFeeHeadMapping() { }

	public ConcessionRequestFeeHeadMapping(
		Guid id,
		Guid concessionRequestId,
		Guid concessionTypeId,
		Guid feeHeadId) : base(id)
	{
		ConcessionRequestId = concessionRequestId;
		ConcessionTypeId = concessionTypeId;
		FeeHeadId = feeHeadId;
	}
}
