using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string SchoolName { get; private set; } = string.Empty;
	public string SchoolAddress { get; private set; } = string.Empty;
	public bool AutomaticEmail { get; private set; }
	public Guid PresentationVersionId { get; private set; }
	public string PresentationJson { get; private set; } = "{}";
	public SalaryDocumentPresentation GetPresentation() => System.Text.Json.JsonSerializer.Deserialize<SalaryDocumentPresentation>(PresentationJson) ?? new();
	public Guid LastRequestId { get; private set; }

	private PayslipSettings() { }
	public PayslipSettings(Guid tenantId) : base(tenantId) { TenantId = tenantId; }

	public void Configure(string name, string address, bool automaticEmail, Guid requestId, SalaryDocumentPresentation? presentation = null)
	{
		if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 250 ||
			string.IsNullOrWhiteSpace(address) || address.Trim().Length > 1000 || requestId == Guid.Empty)
			throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		presentation ??= new();
		presentation.Validate(TenantId);
		PresentationVersionId = Guid.NewGuid();
		PresentationJson = System.Text.Json.JsonSerializer.Serialize(presentation);
		SchoolName = name.Trim();
		SchoolAddress = address.Trim();
		AutomaticEmail = automaticEmail;
		LastRequestId = requestId;
	}
}
