using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<(Guid UserId, Guid LinkId)> LinkProfilePortalAsync(Guid studentId, bool parent)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			var user = new IdentityUser(Guid.NewGuid(), $"profile-{Guid.NewGuid():N}", "profile@example.test", TestTenantId);
			user.SetEmailConfirmed(true);
			await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(user, true);
			var link = new StudentPortalAccountLink(Guid.NewGuid(), TestTenantId, studentId, user.Id,
				parent ? "guardian" : "student", parent ? "mother" : "self", Guid.NewGuid());
			await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(link, true);
			return (user.Id, link.Id);
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Mobile_profile_reuses_dynamic_schema_current_values_and_file_redaction(bool parent) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(structuredCondition: true, largeFile: true);
		var expected = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
		var account = await LinkProfilePortalAsync(fixture.Student.Id, parent);
		using var principal = _principalAccessor.Change(ProfilePrincipal(account.UserId));
		var profile = await GetRequiredService<IMobileStudentProfileAppService>().GetAsync(fixture.Student.Id);
		profile.StudentId.ShouldBe(fixture.Student.Id);
		profile.AcademicYear!.Id.ShouldBe(fixture.Student.AcademicYearId!.Value);
		var source = profile.FormSources.Single();
		source.SubmissionId.ShouldBe(expected.SubmissionId);
		source.Definition.Sections.Single().Name.ShouldBe(expected.Definition!.Sections.Single().Name);
		source.CurrentValues["BLOOD"].GetString().ShouldBe("B+");
		source.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		source.CurrentValues["TRANSPORT"].GetBoolean().ShouldBeTrue();
		source.CurrentValues["ZERO"].GetDecimal().ShouldBe(0);
		source.CurrentValues["UPLOAD"].GetString().ShouldBe("Attachment recorded");
		source.Definition.Sections.SelectMany(s => s.Fields).ShouldAllBe(f => f.DefaultValue == null);
		var json = JsonSerializer.Serialize(profile);
		json.ShouldNotContain("OriginalValues"); json.ShouldNotContain("EditableFieldCodes");
		json.ShouldNotContain("base64"); json.ShouldNotContain(new string('A', 100));
	});

	[Fact]
	public Task Mobile_profile_masks_values_on_server_for_portal_users() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(structuredCondition: true);
		var policy = StudentRecordsPolicy.Starter();
		policy.Access.Masks = [new() { FieldGroup = "Medical", AllowedRoleNames = ["school-nurse"] }];
		await SetInlineFormPolicyAsync(policy, 2);
		var account = await LinkProfilePortalAsync(fixture.Student.Id, true);
		using var principal = _principalAccessor.Change(ProfilePrincipal(account.UserId));
		var source = (await GetRequiredService<IMobileStudentProfileAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
		source.FormMaskedFieldCodes.ShouldContain("CANONICAL_BLOOD");
		source.CurrentValues["CANONICAL_BLOOD"].ValueKind.ShouldBe(JsonValueKind.Null);
		source.CurrentValues["BLOOD"].ValueKind.ShouldBe(JsonValueKind.Null);
	});

	[Fact]
	public Task Mobile_profile_denies_unlinked_foreign_tenant_revoked_link_and_invalid_year() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var account = await LinkProfilePortalAsync(fixture.Student.Id, true);
		using var principal = _principalAccessor.Change(ProfilePrincipal(account.UserId));
		var service = GetRequiredService<IMobileStudentProfileAppService>();
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(Guid.NewGuid()));
		await Should.ThrowAsync<BusinessException>(() => service.GetAsync(fixture.Student.Id, Guid.NewGuid()));
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(fixture.Student.Id));
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(account.LinkId, true));
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(fixture.Student.Id));
	});
}
