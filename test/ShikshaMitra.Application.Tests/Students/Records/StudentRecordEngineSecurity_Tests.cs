using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Record_disabled_database_identity_denies_directory_profile_templates_and_reports_despite_valid_admin_claims() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "ActiveActorGuard", "Student"); await GrantCompletedRecordAsync(student);
		var directory = GetRequiredService<IStudentDirectoryAppService>(); var profile = GetRequiredService<IStudentRecordProfileAppService>();
		var noScopeId = Guid.NewGuid(); await EnsureTestIdentityUserAsync(noScopeId, $"actor-noscope-{noScopeId:N}");
		var forgedAdmin = ProfilePrincipal(noScopeId); ((ClaimsIdentity)forgedAdmin.Identity!).AddClaim(new Claim(AbpClaimTypes.Role, "admin"));
		using (_principalAccessor.Change(forgedAdmin))
		{
			(await directory.GetListAsync(new() { StudentId = student.Id })).TotalCount.ShouldBe(0);
			(await Should.ThrowAsync<BusinessException>(() => profile.GetAsync(student.Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
		(await directory.GetListAsync(new() { StudentId = student.Id })).TotalCount.ShouldBe(1);
		var reports = GetRequiredService<IStudentRecordReportAppService>();
		var reportInput = new StudentRecordReportInput { ReportType = "Directory", StudentId = student.Id, ClientKey = "disabled-actor-report" };
		var report = await reports.CreateAsync(reportInput);
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>(); var actor = await users.GetAsync(TestUserId);
			actor.SetIsActive(false); await users.UpdateAsync(actor, true);
		});
		// The authenticated principal and its admin claim remain valid; database deactivation takes precedence.
		await Should.ThrowAsync<AbpAuthorizationException>(() => directory.GetListAsync(new() { StudentId = student.Id }));
		await Should.ThrowAsync<AbpAuthorizationException>(() => directory.GetOptionsAsync(student.AcademicYearId));
		await Should.ThrowAsync<AbpAuthorizationException>(() => profile.GetAsync(student.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IStudentIdCardAppService>().GetTemplateAsync());
		await Should.ThrowAsync<AbpAuthorizationException>(() => GetRequiredService<IStudentCertificateAppService>().GetTemplateAsync(StudentCertificateType.Bonafide));
		await Should.ThrowAsync<AbpAuthorizationException>(() => reports.GetListAsync(new()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => reports.PreviewAsync(reportInput));
		await Should.ThrowAsync<AbpAuthorizationException>(() => reports.CreateAsync(reportInput));
		await Should.ThrowAsync<AbpAuthorizationException>(() => reports.CancelAsync(report.Id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => reports.DownloadAsync(report.Id));
	});

	[Fact]
	public Task Record_photo_review_is_scoped_submitted_draft_survives_expiry_and_issued_ID_keeps_photo_snapshot() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Photo"].Mode = "Approval";
		policy.Changes["Photo"].Stages = [new() { RoleNames = ["RecordsPhotoReviewer"] }]; await PublishDirectoryPolicyAsync(policy);
		var photos = GetRequiredService<IStudentRecordPhotoAppService>();
		var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
		await Should.ThrowAsync<BusinessException>(() => photos.UploadAsync(fixture.Student.Id, new() { FileName = "spoofed.jpg", ContentType = "image/jpeg", Content = png }));
		var draft = await photos.UploadAsync(fixture.Student.Id, new() { FileName = "verified.png", ContentType = "image/png", Content = png });
		var reviewer = Guid.NewGuid(); await EnsureTestIdentityUserAsync(reviewer, $"photo-review-{reviewer:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewer), true);
			var roles = GetRequiredService<IdentityRoleManager>(); (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "RecordsPhotoReviewer", TestTenantId))).Succeeded.ShouldBeTrue();
			var users = GetRequiredService<IdentityUserManager>(); (await users.AddToRoleAsync((await users.FindByIdAsync(reviewer.ToString()))!, "RecordsPhotoReviewer")).Succeeded.ShouldBeTrue();
		});
		using (_principalAccessor.Change(ProfilePrincipal(reviewer))) await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => photos.GetAsync(fixture.Student.Id, draft.Id));
		var snapshot = await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "photo");
		var input = new CreateStudentRecordChangeDto { StudentId = fixture.Student.Id, Section = "photo", ClientKey = Guid.NewGuid().ToString("N"), Reason = "Verified annual photograph", ExpectedSnapshot = snapshot.Snapshot, Payload = JsonSerializer.SerializeToElement(new { photoDraftId = draft.Id }) };
		var request = await fixture.Service.CreateAsync(input); request.Status.ShouldBe("Pending"); request.PhotoPreviewUrl.ShouldNotBeNull();
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentRecordPhotoDraft, Guid>>(); var value = await repository.GetAsync(draft.Id);
			typeof(StudentRecordPhotoDraft).GetProperty(nameof(StudentRecordPhotoDraft.ExpiresAt))!.SetValue(value, DateTimeOffset.UtcNow.AddDays(-1)); await repository.UpdateAsync(value, true);
		});
		(await fixture.Service.CreateAsync(input)).Id.ShouldBe(request.Id);
		using (_principalAccessor.Change(ProfilePrincipal(reviewer)))
		{
			(await photos.GetAsync(fixture.Student.Id, draft.Id)).Content.ShouldBe(png);
			(await fixture.Service.ApproveAsync(request.Id, new() { ConcurrencyStamp = request.ConcurrencyStamp, Reason = "Verified image and school registration" })).Status.ShouldBe("Applied");
		}
		var ids = GetRequiredService<IStudentIdCardAppService>(); var card = (await ids.IssueAsync(new() { StudentIds = [fixture.Student.Id], IdempotencyKey = Guid.NewGuid().ToString("N") })).Single(); card.HasPhoto.ShouldBeTrue();
		card.StudentSnapshot.ShouldNotContainKey("_PhotoBlobName");
		await WithUnitOfWorkAsync(async () => { var student = await _studentRepository.GetAsync(fixture.Student.Id); student.SetPhotoBlobName("record-test/new-photo-not-an-issued-asset.png"); await _studentRepository.UpdateAsync(student, true); });
		(await ids.GetPdfAsync(card.Id!.Value)).Content.Length.ShouldBeGreaterThan(100);
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Record_approval_uses_database_roles_and_conflicting_application_does_not_overwrite(bool stale) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Contact"].Mode = "Approval";
		policy.Changes["Contact"].Stages = [new() { RoleNames = ["RecordsRegistrar"] }];
		await PublishDirectoryPolicyAsync(policy);
		var request = await fixture.Service.CreateAsync(fixture.Input);
		var falseActor = Guid.NewGuid(); var trueActor = Guid.NewGuid();
		foreach (var actor in new[] { falseActor, trueActor })
		{
			await EnsureTestIdentityUserAsync(actor, $"records-reviewer-{actor:N}");
			await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: actor), true));
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var roleManager = GetRequiredService<IdentityRoleManager>();
			if (!await roleManager.RoleExistsAsync("RecordsRegistrar")) (await roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), "RecordsRegistrar", TestTenantId))).Succeeded.ShouldBeTrue();
			var users = GetRequiredService<IdentityUserManager>();
			(await users.AddToRoleAsync((await users.FindByIdAsync(trueActor.ToString()))!, "RecordsRegistrar")).Succeeded.ShouldBeTrue();
		});
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([new(AbpClaimTypes.UserId, falseActor.ToString()), new(AbpClaimTypes.Role, "RecordsRegistrar")], "Test"))))
			(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ApproveAsync(request.Id, new() { ConcurrencyStamp = request.ConcurrencyStamp, Reason = "Forged claim cannot approve" }))).Code.ShouldBe("Students:Records:ApproverRequired");
		if (stale) await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentCommunicationDetail, Guid>>();
			var value = await repository.FindAsync(fixture.Student.Id) ?? await repository.InsertAsync(new StudentCommunicationDetail(fixture.Student.Id, TestTenantId), true);
			value.UpdateCommunicationInformation(value.LocalAddress, value.PermanentAddress, value.EmergencyAddress,
				value.IsPermanentSameAsLocal, value.IsEmergencySameAsLocal, "Newer verified guardian", value.EmergencyContactPhone, value.EmergencyContactRelation);
			await repository.UpdateAsync(value, true);
		});
		using (_principalAccessor.Change(ProfilePrincipal(trueActor)))
		{
			var result = await fixture.Service.ApproveAsync(request.Id, new() { ConcurrencyStamp = request.ConcurrencyStamp, Reason = "Verified school register and guardian" });
			result.Status.ShouldBe(stale ? "Conflict" : "Applied");
			if (stale) result.ExceptionCode.ShouldBe("Students:Records:StaleSnapshot");
			(await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "communication")).Data.GetProperty("emergencyContactName").GetString().ShouldBe(stale ? "Newer verified guardian" : "Verified guardian");
		}
	});
}
