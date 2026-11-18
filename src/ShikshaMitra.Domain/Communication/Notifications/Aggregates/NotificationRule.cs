using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string EventKey { get; private set; } = string.Empty;
	public string ScopeKey { get; private set; } = string.Empty;
	public Guid? AcademicYearId { get; private set; }
	public Guid? BoardId { get; private set; }
	public Guid? MediumId { get; private set; }
	public Guid? ClassId { get; private set; }
	public Guid? DivisionId { get; private set; }
	public bool IsEnabled { get; private set; }
	public int Revision { get; private set; }
	public string ConfigurationJson { get; private set; } = "{}";
	protected NotificationRule() { }
	public NotificationRule(Guid id, Guid tenantId, string eventKey, NotificationRuleScope scope) : base(id)
	{
		TenantId = NotificationDataGuard.Identifier(tenantId, nameof(tenantId));
		EventKey = NotificationDataGuard.Text(eventKey, nameof(eventKey), 160);
		ArgumentNullException.ThrowIfNull(scope);
		ScopeKey = scope.Key;
		AcademicYearId = scope.AcademicYearId;
		BoardId = scope.BoardId;
		MediumId = scope.MediumId;
		ClassId = scope.ClassId;
		DivisionId = scope.DivisionId;
		Revision = 1;
	}
	public void Configure(bool enabled, string configurationJson, string expectedConcurrencyStamp)
	{
		NotificationRuntimeGuard.State(!string.IsNullOrEmpty(expectedConcurrencyStamp) &&
			string.Equals(ConcurrencyStamp, expectedConcurrencyStamp, StringComparison.Ordinal));
		NotificationRuntimeGuard.Snapshot(configurationJson);
		if (IsEnabled == enabled && string.Equals(ConfigurationJson, configurationJson, StringComparison.Ordinal)) return;
		var nextRevision = checked(Revision + 1);
		ConfigurationJson = configurationJson; IsEnabled = enabled; Revision = nextRevision;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
}
