using System;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiPolicy_Tests
{
	[Theory]
	[InlineData("IN_PLATFORM", false, null)]
	[InlineData("EXTERNAL", true, "External provider")]
	[InlineData("EXTERNAL", false, " ")]
	[InlineData("UNKNOWN", false, null)]
	public void Invalid_assignments_are_rejected(string mode, bool hasPolicy, string? reason) =>
		Should.Throw<BusinessException>(() => EsiManagement.Validate(mode, hasPolicy ? Guid.NewGuid() : null, reason))
			.Code.ShouldBe(HRErrorCodes.EsiPolicySelectionInvalid);
	[Fact]
	public void Default_policy_is_retained_and_archiving_keeps_identity()
	{
		var defaultPolicy = new EsiPolicy(Guid.NewGuid(), Guid.NewGuid(), "Standard ESIC", "STANDARD_ESIC", true);
		Should.Throw<BusinessException>(defaultPolicy.Archive).Code.ShouldBe(HRErrorCodes.EsiDefaultPolicyRequired);
		var policy = new EsiPolicy(Guid.NewGuid(), defaultPolicy.TenantId, "External staff", "OTHER");
		var id = policy.Id;
		policy.Archive();
		policy.Id.ShouldBe(id);
		policy.IsArchived.ShouldBeTrue();
	}
	[Fact]
	public void Published_template_and_effective_revision_assignments_are_immutable()
	{
		var id = Guid.NewGuid();
		var version = new SalaryStructureTemplateVersion(Guid.NewGuid(), null, Guid.NewGuid(), 1, []);
		version.SetEsiAssignment(EsiManagement.InPlatform, id, null);
		version.Publish(DateTime.UtcNow);
		Should.Throw<BusinessException>(() => version.SetEsiAssignment(EsiManagement.External, null, "External provider"));
		var revision = new EmployeeCompensationRevision(Guid.NewGuid(), null, Guid.NewGuid(), 1, new(2026, 10, 1));
		revision.SetEsiAssignment(EsiManagement.External, null, "External provider");
		revision.MakeEffective(Guid.NewGuid(), DateTime.UtcNow);
		Should.Throw<BusinessException>(() => revision.SetEsiAssignment(EsiManagement.InPlatform, id, null));
	}
}
