using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Records_approvable_queue_projects_classifier_metadata_and_preserves_total_page_role_history_and_mask_results(bool contactMasked) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var requester = Guid.NewGuid(); await EnsureTestIdentityUserAsync(requester, "queue-requester-" + requester.ToString("N"));
		const string reviewerRole = "QueueProjectionReviewer";
		const string otherRole = "OtherQueueProjectionReviewer";
		await WithUnitOfWorkAsync(async () =>
		{
			var roles = GetRequiredService<IdentityRoleManager>();
			foreach (var name in new[] { reviewerRole, otherRole })
				if (!await roles.RoleExistsAsync(name)) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), name, TestTenantId))).Succeeded.ShouldBeTrue();
			var users = GetRequiredService<IdentityUserManager>();
			(await users.AddToRoleAsync((await users.FindByIdAsync(TestUserId.ToString()))!, reviewerRole)).Succeeded.ShouldBeTrue();
		});
		var communication = await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "communication");
		var medical = await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "medical");
		var policy = StudentRecordsPolicy.Starter();
		foreach (var action in new[] { "Contact", "Medical" })
		{
			policy.Changes[action].Mode = "Approval";
			policy.Changes[action].Stages = [new() { RoleNames = [reviewerRole] }];
		}
		var current = StudentRecordsPolicy.FromJson(policy.ToJson());
		if (contactMasked) current.Access.Masks.Add(new() { FieldGroup = "Contact", AllowedRoleNames = [otherRole] });
		await PublishDirectoryPolicyAsync(current);
		var seeded = await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentRecordChange, Guid>>();
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var sequence = 0;
			async Task<StudentRecordChange> Add(string section, Guid actor, string role, int approvalStages = 1)
			{
				var before = (section == "communication" ? communication : medical).Data.GetRawText();
				var proposed = JsonNode.Parse(before)!.AsObject();
				proposed[section == "communication" ? "emergencyContactName" : "bloodGroup"] = section == "communication" ? "Reviewed guardian" : "O+";
				var action = section == "communication" ? "Contact" : "Medical";
				var saved = StudentRecordsPolicy.FromJson(policy.ToJson());
				var stages = Enumerable.Range(0, approvalStages).Select(_ => new StudentRecordsApprovalStage { RoleNames = [role] }).ToList();
				saved.Changes[action].Stages = stages; saved.Changes[action].Mode = approvalStages == 0 ? "Direct" : "Approval";
				var item = new StudentRecordChange(Guid.NewGuid(), TestTenantId, fixture.Student.Id, fixture.Student.AcademicYearId!.Value,
					actor, section, action, "queue-read-" + Guid.NewGuid().ToString("N"), "Historical pending review", before,
					proposed.ToJsonString(), saved.ToJson(), null, JsonSerializer.Serialize(stages, StudentRecordPayloadService.JsonOptions),
					approvalStages, DateTimeOffset.UtcNow.AddDays(-1));
				await repository.InsertAsync(item, true);
				db.Entry(item).Property(x => x.CreationTime).CurrentValue = DateTime.UtcNow.Date.AddDays(-2).AddMinutes(sequence++);
				await db.SaveChangesAsync(); return item;
			}
			var contact = await Add("communication", requester, reviewerRole);
			var health = await Add("medical", requester, reviewerRole);
			var own = await Add("communication", TestUserId, reviewerRole);
			var wrong = await Add("medical", requester, otherRole);
			var decided = await Add("medical", requester, reviewerRole, 2);
			decided.Approve(TestUserId); await repository.UpdateAsync(decided, true);
			await GetRequiredService<IRepository<StudentRecordChangeDecision, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
				decided.Id, TestUserId, 0, "Approved", "First approval was already recorded"), true);
			var approved = await Add("medical", requester, reviewerRole, 0);
			return (Contact: contact.Id, Medical: health.Id, Denied: new[] { own.Id, wrong.Id, decided.Id, approved.Id });
		});
		var measured = await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await fixture.Service.GetListAsync(new() { StudentId = fixture.Student.Id, ApprovableOnly = true, MaxResultCount = 1 });
			var classifier = counter.ReadCommands.Where(sql => sql.Contains("FROM \"AppStudentRecordChanges\"", StringComparison.Ordinal) &&
				!sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase) && !sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)).ToArray();
			return (Result: result, Classifier: classifier, StudentSql: counter.ReadCommands.Where(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)).ToArray(),
				TrackedChanges: db.ChangeTracker.Entries<StudentRecordChange>().Count());
		});
		var expected = contactMasked ? new[] { seeded.Medical } : new[] { seeded.Medical, seeded.Contact };
		var all = await fixture.Service.GetListAsync(new() { StudentId = fixture.Student.Id, ApprovableOnly = true, MaxResultCount = 100 });
		all.TotalCount.ShouldBe(expected.Length); all.Items.Select(item => item.Id).ShouldBe(expected);
		all.Items.ShouldAllBe(item => item.CanApprove && !item.PayloadMasked && item.Status == "Pending" && item.Before == null && item.Proposed == null);
		measured.Result.TotalCount.ShouldBe(expected.Length); measured.Result.Items.Single().Id.ShouldBe(expected[0]);
		var next = await fixture.Service.GetListAsync(new() { StudentId = fixture.Student.Id, ApprovableOnly = true, SkipCount = 1, MaxResultCount = 1 });
		next.TotalCount.ShouldBe(expected.Length); next.Items.Select(item => item.Id).ShouldBe(expected.Skip(1));
		foreach (var id in seeded.Denied) (await fixture.Service.GetAsync(id)).CanApprove.ShouldBeFalse();
		(await fixture.Service.GetListAsync(new() { StudentId = fixture.Student.Id, ApprovableOnly = true, Status = "Approved" })).TotalCount.ShouldBe(0);
		measured.Classifier.ShouldNotBeEmpty();
		measured.Classifier.ShouldAllBe(sql => !sql.Contains("\"PayloadJson\"", StringComparison.Ordinal) && !sql.Contains("\"PolicyJson\"", StringComparison.Ordinal),
			"The complete pending classifier needs approval/source metadata, never the unreturned proposed payload or full saved policy.");
		measured.StudentSql.ShouldAllBe(sql => !sql.Contains("\"PhotoBlobName\"", StringComparison.Ordinal), "Candidate identities need only StudentId and current policy year.");
		measured.TrackedChanges.ShouldBeLessThanOrEqualTo(1, "Only the returned page may hydrate complete change aggregates; all-candidate classification must still precede count and paging.");
	});

	[Fact]
	public Task Records_approvable_queue_keeps_recorded_custom_and_legacy_form_source_authority_distinct_for_same_student() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(direct: false, structuredCondition: true);
		await PrepareOperationalDetailsReadFixtureAsync(fixture.Student.Id, null);
		var service = GetRequiredService<IStudentRecordChangeAppService>();
		var before = await service.GetSnapshotAsync(fixture.Student.Id, "custom-details");
		var first = await service.CreateAsync(CustomCorrection(fixture.Student.Id, before, "BLOOD", JsonValue.Create("O+")!));
		var second = await service.CreateAsync(CustomCorrection(fixture.Student.Id, before, "BLOOD", JsonValue.Create("A+")!));
		first.Status.ShouldBe("Pending"); second.Status.ShouldBe("Pending");
		var forms = GetRequiredService<IStudentRecordFormAppService>();
		var formBefore = await forms.GetSnapshotAsync(fixture.Student.Id);
		var form = await WithUnitOfWorkAsync(async () =>
		{
			var policy = StudentRecordsPolicy.Starter();
			var item = new StudentRecordChange(Guid.NewGuid(), TestTenantId, fixture.Student.Id, fixture.Student.AcademicYearId!.Value,
				TestUserId, "registration-form", "Correction", "legacy-form-queue-" + Guid.NewGuid().ToString("N"), "Retained older form approval",
				formBefore.Data.GetRawText(), InlineFormEdit(formBefore, ("CANONICAL_BLOOD", JsonValue.Create("O+"))).Payload.GetRawText(),
				policy.ToJson(), null, JsonSerializer.Serialize(policy.Changes["Correction"].Stages, StudentRecordPayloadService.JsonOptions),
				policy.Changes["Correction"].Stages.Count, DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<StudentRecordChange, Guid>>().InsertAsync(item, true); return item.Id;
		});
		var reviewer = Guid.NewGuid(); await EnsureTestIdentityUserAsync(reviewer, "source-queue-reviewer-" + reviewer.ToString("N"));
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewer), true);
			var roles = GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("admin")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "admin", TestTenantId))).Succeeded.ShouldBeTrue();
			var users = GetRequiredService<IdentityUserManager>();
			(await users.AddToRoleAsync((await users.FindByIdAsync(reviewer.ToString()))!, "admin")).Succeeded.ShouldBeTrue();
		});
		using var principal = _principalAccessor.Change(ProfilePrincipal(reviewer));
		var initial = await service.GetListAsync(new() { StudentId = fixture.Student.Id, ApprovableOnly = true });
		initial.TotalCount.ShouldBe(3); initial.Items.ShouldAllBe(item => item.CanApprove && !item.PayloadMasked);
		async Task HideRecordedSource(Guid id) => await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentRecordChange, Guid>>();
			var item = await repository.GetAsync(id);
			var recorded = JsonNode.Parse(item.BeforeJson)!;
			recorded["sources"]![0]!["submissionId"] = Guid.NewGuid().ToString();
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.Entry(item).Property(x => x.BeforeJson).CurrentValue = recorded.ToJsonString(); await db.SaveChangesAsync();
		});
		await HideRecordedSource(first.Id);
		var oneHidden = await service.GetListAsync(new() { StudentId = fixture.Student.Id, ApprovableOnly = true });
		oneHidden.TotalCount.ShouldBe(2);
		oneHidden.Items.Select(item => item.Id).OrderBy(id => id).ShouldBe(new[] { second.Id, form }.OrderBy(id => id));
		(await service.GetAsync(first.Id)).PayloadMasked.ShouldBeTrue();
		await HideRecordedSource(form);
		var twoHidden = await service.GetListAsync(new() { StudentId = fixture.Student.Id, ApprovableOnly = true, MaxResultCount = 1 });
		twoHidden.TotalCount.ShouldBe(1); twoHidden.Items.Single().Id.ShouldBe(second.Id);
		(await service.GetAsync(form)).PayloadMasked.ShouldBeTrue();
	});
}
