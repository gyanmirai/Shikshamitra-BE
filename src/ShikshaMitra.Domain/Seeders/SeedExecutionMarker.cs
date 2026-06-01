using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Seeders;

public class SeedExecutionMarker : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string SeederName { get; private set; } = string.Empty;
	public string Version { get; private set; } = string.Empty;
	public string Profile { get; private set; } = string.Empty;
	public SeedExecutionStatus Status { get; private set; }
	public DateTime StartedAt { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public long? ElapsedMilliseconds { get; private set; }
	public string? Error { get; private set; }

	private SeedExecutionMarker()
	{
	}

	public SeedExecutionMarker(Guid id, Guid? tenantId, string seederName, string version, string profile)
		: base(id)
	{
		TenantId = tenantId;
		SeederName = Check.NotNullOrWhiteSpace(seederName, nameof(seederName), maxLength: 128);
		Version = Check.NotNullOrWhiteSpace(version, nameof(version), maxLength: 64);
		Profile = Check.NotNullOrWhiteSpace(profile, nameof(profile), maxLength: 32);
		MarkStarted();
	}

	public void MarkStarted()
	{
		Status = SeedExecutionStatus.Running;
		StartedAt = DateTime.UtcNow;
		CompletedAt = null;
		ElapsedMilliseconds = null;
		Error = null;
	}

	public void MarkCompleted(long elapsedMilliseconds)
	{
		Status = SeedExecutionStatus.Completed;
		CompletedAt = DateTime.UtcNow;
		ElapsedMilliseconds = elapsedMilliseconds;
		Error = null;
	}

	public void MarkFailed(long elapsedMilliseconds, string error)
	{
		Status = SeedExecutionStatus.Failed;
		CompletedAt = DateTime.UtcNow;
		ElapsedMilliseconds = elapsedMilliseconds;
		Error = error.Length <= 4000 ? error : error[..4000];
	}
}
