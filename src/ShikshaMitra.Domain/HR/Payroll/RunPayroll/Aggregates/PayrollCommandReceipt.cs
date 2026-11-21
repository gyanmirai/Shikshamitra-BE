using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollCommandReceipt : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ActorId { get; private set; }
	public string Command { get; private set; } = "";
	public Guid Key { get; private set; }
	public string Fingerprint { get; private set; } = "";
	public Guid ResultId { get; private set; }
	public string ResultJson { get; private set; } = "";

	private PayrollCommandReceipt() { }
	public PayrollCommandReceipt(Guid id, Guid tenantId, Guid actorId, string command, Guid key,
		string fingerprint, Guid resultId, string resultJson) : base(id)
	{
		TenantId = tenantId;
		ActorId = actorId;
		Command = command;
		Key = key;
		Fingerprint = fingerprint;
		ResultId = resultId;
		ResultJson = resultJson;
	}
}
