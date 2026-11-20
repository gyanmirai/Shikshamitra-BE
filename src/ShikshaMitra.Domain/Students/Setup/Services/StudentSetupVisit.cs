using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.Students.Setup;

public static class StudentSetupVisit
{
	public const string Property = "StudentConfigurationFirstVisitV1";
	public sealed record Entry(int Version, Guid ActorId, DateTime VisitedAt);

	public static Entry? Read(Tenant tenant)
	{
		if (!tenant.ExtraProperties.TryGetValue(Property, out var value)) return null;
		if (value is not string json || json.Length > 1024) throw new BusinessException(StudentSetupKeys.InvalidStoredState);
		try
		{
			var entry = JsonSerializer.Deserialize<Entry>(json);
			if (entry?.Version == 1 && entry.ActorId != Guid.Empty && entry.VisitedAt != default) return entry;
		}
		catch (JsonException) { }
		throw new BusinessException(StudentSetupKeys.InvalidStoredState);
	}

	public static Entry Record(Tenant tenant, Guid actorId, DateTime now)
	{
		var existing = Read(tenant);
		if (existing != null) return existing;
		if (actorId == Guid.Empty || now == default) throw new BusinessException(StudentSetupKeys.Invalid);
		var entry = new Entry(1, actorId, now);
		tenant.SetProperty(Property, JsonSerializer.Serialize(entry));
		return entry;
	}
}
