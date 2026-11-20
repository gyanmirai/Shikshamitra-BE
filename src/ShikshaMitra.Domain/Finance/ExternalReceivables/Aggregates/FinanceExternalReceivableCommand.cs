using System;
using System.Collections.Generic;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalReceivableCommand : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Operation { get; private set; } = string.Empty;
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string CommandFingerprint { get; private set; } = string.Empty;
	public Guid SourceLinkId { get; private set; }
	public Guid ActorId { get; private set; }
	public Guid? ResultEntityId { get; private set; }
	public string? ResultEntityType { get; private set; }
	private static readonly HashSet<string> Operations = ["Post", "BindPolicy", "Approve", "Collect", "RequestWaiver", "ApproveWaiver", "RejectWaiver", "ReverseWaiver"];

	private FinanceExternalReceivableCommand() { }

	public FinanceExternalReceivableCommand(Guid id, Guid tenantId, string operation, string key,
		string fingerprint, Guid sourceLinkId, Guid actorId, Guid? resultEntityId = null, string? resultEntityType = null) : base(id)
	{
		if (id == Guid.Empty || tenantId == Guid.Empty || !Operations.Contains(operation) || string.IsNullOrWhiteSpace(key) ||
			key.Length > 100 || string.IsNullOrWhiteSpace(fingerprint) || fingerprint.Length != 64 ||
			sourceLinkId == Guid.Empty || actorId == Guid.Empty || resultEntityId == Guid.Empty ||
			resultEntityId.HasValue != !string.IsNullOrWhiteSpace(resultEntityType) || resultEntityType?.Length > 32 ||
			operation != "Post" && !resultEntityId.HasValue)
			throw new BusinessException(LibraryFinanceErrorCodes.IdempotencyConflict);
		TenantId = tenantId;
		Operation = operation;
		IdempotencyKey = key;
		CommandFingerprint = fingerprint;
		SourceLinkId = sourceLinkId;
		ActorId = actorId;
		ResultEntityId = resultEntityId;
		ResultEntityType = resultEntityType;
	}
}
