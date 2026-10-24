using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Mobile_profile_without_retained_registration_returns_real_identity_and_empty_forms(bool parent) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var fixture = await Seed(tenantId, parent);
		using var principal = SignIn(fixture.User.Id);
		var profile = await GetRequiredService<IMobileStudentProfileAppService>().GetAsync(fixture.Student.Id, fixture.Year.Id);
		profile.StudentId.ShouldBe(fixture.Student.Id);
		profile.Summary.Name.ShouldBe("Fixture Student");
		profile.Summary.StandardName.ShouldBe("Class 8");
		profile.AcademicYear!.Id.ShouldBe(fixture.Year.Id);
		profile.FormSources.ShouldBeEmpty();
	});
}
