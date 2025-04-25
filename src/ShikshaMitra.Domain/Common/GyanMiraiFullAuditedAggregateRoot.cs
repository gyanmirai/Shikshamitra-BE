using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Common;

public class GyanMiraiFullAuditedAggregateRoot<TKey> : FullAuditedAggregateRoot<TKey>
{
	public Guid YearId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }

	protected GyanMiraiFullAuditedAggregateRoot()
	{

	}

	protected GyanMiraiFullAuditedAggregateRoot(TKey id)
	: base(id)
	{

	}
}
