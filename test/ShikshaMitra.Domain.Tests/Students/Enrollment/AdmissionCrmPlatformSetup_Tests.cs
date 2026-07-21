using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmPlatformSetup_Tests
{
	[Fact]
	public void Support_session_is_tenant_bound_and_expires()
	{
		var now = DateTime.UtcNow;
		var tenantId = Guid.NewGuid();
		var session = new AdmissionCrmPlatformSupportSession(Guid.NewGuid(), Guid.NewGuid(), tenantId, "Configure the new admission cycle.", "SUP-42", now.AddMinutes(30));

		session.TargetTenantId.ShouldBe(tenantId);
		session.IsActive(now.AddMinutes(29)).ShouldBeTrue();
		session.IsActive(now.AddMinutes(31)).ShouldBeFalse();
		session.End(now.AddMinutes(5));
		session.IsActive(now.AddMinutes(6)).ShouldBeFalse();
	}

	[Fact]
	public void Published_platform_preset_is_immutable_by_lifecycle()
	{
		var preset = new AdmissionCrmPlatformPreset(Guid.NewGuid(), "STANDARD", "Standard enrollment", 1, "{}", "[]", null);
		preset.Publish(DateTime.UtcNow);
		preset.Status.ShouldBe(AdmissionCrmPlatformPresetStatus.Published);
		Should.Throw<BusinessException>(() => preset.Publish(DateTime.UtcNow)).Code.ShouldBe("AdmissionCrm:PlatformPresetNotDraft");
		preset.Retire();
		preset.Status.ShouldBe(AdmissionCrmPlatformPresetStatus.Retired);
	}

	[Fact]
	public void Access_policy_uses_stable_setting_and_section_keys()
	{
		var policy = new AdmissionCrmSettingsAccessPolicy(Guid.NewGuid(), Guid.NewGuid(), "communications", "delivery", AdmissionCrmSettingsAccessMode.ReadOnly);
		policy.SettingsKey.ShouldBe("communications");
		policy.SectionKey.ShouldBe("delivery");
		policy.AccessMode.ShouldBe(AdmissionCrmSettingsAccessMode.ReadOnly);
	}
}
