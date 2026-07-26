using System;
using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Reports;

public class HrReportSchedule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid OwnerUserId { get; private set; }
	public Guid? TemplateId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string Frequency { get; private set; } = "monthly";
	public string DeliveryMode { get; private set; } = "inApp";
	public string Status { get; private set; } = "Active";
	public string RecipientUserIdsJson { get; private set; } = "[]";
	public string DefinitionJson { get; private set; } = "{}";
	public DateTime NextRunAt { get; private set; }
	public DateTime? LastRunAt { get; private set; }

	private HrReportSchedule()
	{
	}

	public HrReportSchedule(
		Guid id,
		Guid? tenantId,
		Guid ownerUserId,
		string name,
		Guid? templateId,
		string frequency,
		string deliveryMode,
		IReadOnlyCollection<Guid> recipientUserIds,
		string definitionJson,
		DateTime nextRunAt) : base(id)
	{
		TenantId = tenantId;
		OwnerUserId = ownerUserId;
		Update(name, templateId, frequency, deliveryMode, recipientUserIds, definitionJson, nextRunAt);
	}

	public void Update(
		string name,
		Guid? templateId,
		string frequency,
		string deliveryMode,
		IReadOnlyCollection<Guid> recipientUserIds,
		string definitionJson,
		DateTime nextRunAt)
	{
		Name = name.Trim();
		TemplateId = templateId;
		Frequency = frequency;
		DeliveryMode = deliveryMode;
		RecipientUserIdsJson = JsonSerializer.Serialize(recipientUserIds);
		DefinitionJson = definitionJson;
		NextRunAt = nextRunAt;
	}

	public void Pause() => Status = "Paused";

	public void Resume(DateTime nextRunAt)
	{
		Status = "Active";
		NextRunAt = nextRunAt;
	}

	public void MarkRun(DateTime runAt, DateTime nextRunAt)
	{
		LastRunAt = runAt;
		NextRunAt = nextRunAt;
	}

	public void DeferRetry(DateTime nextRunAt)
	{
		NextRunAt = nextRunAt;
	}

	public IReadOnlyList<Guid> GetRecipientUserIds()
	{
		try
		{
			return JsonSerializer.Deserialize<List<Guid>>(RecipientUserIdsJson) ?? [];
		}
		catch
		{
			return [];
		}
	}
}
