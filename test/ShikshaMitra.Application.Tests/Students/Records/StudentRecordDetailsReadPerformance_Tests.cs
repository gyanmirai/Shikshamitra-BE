using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Records_details_reuses_canonical_section_data_after_guards_and_keeps_fresh_write_mask_and_schema_views(bool frozenSchema) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(structuredCondition: true, inherited: frozenSchema);
		var picture = frozenSchema ? null : "legacy/records-profile.png";
		var divisionId = await PrepareOperationalDetailsReadFixtureAsync(fixture.Student.Id, picture);
		var service = GetRequiredService<IStudentRecordDetailsAppService>();
		var measured = await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await service.GetAsync(fixture.Student.Id);
			return (Result: result, MedicalReads: counter.ReadCommands.Count(sql => sql.Contains("FROM \"StudentMedicalRecords\"", StringComparison.Ordinal)));
		});
		JsonSerializer.Serialize(measured.Result).ShouldBe(JsonSerializer.Serialize(await service.GetAsync(fixture.Student.Id)));
		var original = measured.Result.FormSources.Single();
		var general = measured.Result.StructuredSections.Single(x => x.Section == "general").Data!.Value;
		general.GetProperty("divisionId").GetGuid().ShouldBe(divisionId);
		general.GetProperty("profilePictureUrl").GetString().ShouldBe(picture);
		original.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		measured.Result.StructuredSections.Single(x => x.Section == "medical").Data!.Value.GetProperty("bloodGroup").GetString().ShouldBe("B+");
		if (frozenSchema) original.CurrentValues["INHERITED_NOTE"].GetString().ShouldBe("Science and drawing");
		var forms = GetRequiredService<IStudentRecordFormAppService>();
		(await forms.SaveAsync(fixture.Student.Id, InlineFormEdit(await forms.GetSnapshotAsync(fixture.Student.Id),
			("CANONICAL_BLOOD", JsonValue.Create("O+"))))).Status.ShouldBe("Applied");
		var changed = await service.GetAsync(fixture.Student.Id);
		changed.FormSources.Single().CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("O+");
		changed.FormSources.Single().OriginalValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		changed.StructuredSections.Single(x => x.Section == "medical").Data!.Value.GetProperty("bloodGroup").GetString().ShouldBe("O+");
		var mask = StudentRecordsPolicy.Starter(); mask.Access.Masks = [new() { FieldGroup = "Medical", AllowedRoleNames = ["other-role"] }];
		await SetInlineFormPolicyAsync(mask, 2);
		var restricted = await service.GetAsync(fixture.Student.Id);
		restricted.StructuredSections.Single(x => x.Section == "medical").IsMasked.ShouldBeTrue();
		restricted.FormSources.Single().CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("Restricted");
		await SetInlineFormPolicyAsync(StudentRecordsPolicy.Starter(), 3);
		var unmasked = await service.GetAsync(fixture.Student.Id);
		unmasked.FormSources.Single().CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("O+");
		unmasked.FormSources.Single().OriginalValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		original.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		Console.WriteLine($"Records Details medical owner material reads={measured.MedicalReads}, frozen schema={frozenSchema}.");
		measured.MedicalReads.ShouldBe(1, "Condition evaluation, full retained form and structured view share canonical data after independent public read guards.");
	});

	[Fact]
	public Task Records_details_and_canonical_readers_reject_denied_actor_scope_and_group_after_a_prior_read() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(structuredCondition: true);
		await PrepareOperationalDetailsReadFixtureAsync(fixture.Student.Id, "legacy/guard-profile.png");
		var service = GetRequiredService<IStudentRecordDetailsAppService>();
		(await service.GetAsync(fixture.Student.Id)).StudentId.ShouldBe(fixture.Student.Id);
		await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), false);
		try { await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(fixture.Student.Id)); }
		finally { await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), true); }
		var staffId = Guid.NewGuid(); await EnsureTestIdentityUserAsync(staffId, "unassigned-details-" + staffId.ToString("N"));
		using (_principalAccessor.Change(ProfilePrincipal(staffId)))
			(await Should.ThrowAsync<BusinessException>(() => service.GetAsync(fixture.Student.Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		var mask = StudentRecordsPolicy.Starter(); mask.Access.Masks = [new() { FieldGroup = "Medical", AllowedRoleNames = ["other-role"] }];
		await SetInlineFormPolicyAsync(mask, 2);
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentMedicalRecordsAppService>().GetMedicalRecordsAsync(fixture.Student.Id)))
			.Code.ShouldBe("Students:Records:RestrictedFields");
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(fixture.Student.Id));
	});

	private Task<Guid> PrepareOperationalDetailsReadFixtureAsync(Guid studentId, string? profilePictureUrl) => WithUnitOfWorkAsync(async () =>
	{
		var student = (await _studentRepository.GetRegistrationAggregateAsync(studentId))!;
		var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
			await GetRequiredService<DivisionManager>().CreateAsync(student.StandardId!.Value, "Details read division", 30), true);
		student.UpdateAcademicPlacement(student.AcademicYearId!.Value, student.AcademicBoardId!.Value, student.AcademicMediumId,
			student.AcademicMediumStageId!.Value, student.StandardId.Value, division.Id);
		student.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.Entry(student.AdditionalDetail!).Property(x => x.ProfilePictureUrl).CurrentValue = profilePictureUrl;
		await _studentRepository.UpdateAsync(student, true);
		var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == studentId);
		enrollment.AssignDivision(division.Id); await _studentEnrollmentRepository.UpdateAsync(enrollment, true);
		return division.Id;
	});
}
