using System;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalaryTemplatePreviewProof_Tests
{
	[Fact]
	public void Proof_binds_identity_inputs_results_and_expiry_but_allows_completed_retry()
	{
		var tenant = Substitute.For<ICurrentTenant>();
		var user = Substitute.For<ICurrentUser>();
		var clock = Substitute.For<IClock>();
		var tenantId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		tenant.Id.Returns(tenantId);
		user.Id.Returns(userId);
		var now = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
		clock.Now.Returns(now);
		var service = new SalaryTemplatePreviewProofService(new EphemeralDataProtectionProvider(), tenant, user, clock);
		var inputs = new { Value = 60m, Month = "2026-09" };
		var result = new SalaryTemplateCalculationDto { GrossSalary = 20000m, GeneratedAt = now };
		var preview = service.Issue(inputs, result);
		preview.ExpiresAt.ShouldBe(now.AddMinutes(30));
		service.Validate(preview.PreviewToken, inputs, result);
		Should.Throw<BusinessException>(() => service.Validate("", inputs));
		Should.Throw<BusinessException>(() => service.Validate(preview.PreviewToken + "modified", inputs));
		Should.Throw<BusinessException>(() => service.Validate(preview.PreviewToken, new { Value = 10m, Month = "2026-09" }));
		tenant.Id.Returns(Guid.NewGuid());
		Should.Throw<BusinessException>(() => service.Validate(preview.PreviewToken, inputs));
		tenant.Id.Returns(tenantId);
		user.Id.Returns(Guid.NewGuid());
		Should.Throw<BusinessException>(() => service.Validate(preview.PreviewToken, inputs));
		user.Id.Returns(userId);
		result.GeneratedAt = now.AddMinutes(1);
		service.Validate(preview.PreviewToken, inputs, result);
		result.GrossSalary++;
		Should.Throw<BusinessException>(() => service.Validate(preview.PreviewToken, inputs, result)).Code
			.ShouldBe("HR:Payroll:SalaryTemplatePreviewChanged");
		clock.Now.Returns(preview.ExpiresAt);
		Should.Throw<BusinessException>(() => service.Validate(preview.PreviewToken, inputs)).Code
			.ShouldBe("HR:Payroll:SalaryTemplatePreviewExpired");
		service.Validate(preview.PreviewToken, inputs, replay: true);
	}
}
