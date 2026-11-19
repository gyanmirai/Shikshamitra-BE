using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
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
	public Task Records_duplicate_list_reuses_only_same_row_authorized_students_and_preserves_complete_masked_page(bool contactMasked) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await DuplicateReadFixtureAsync(4);
		var oracles = new Dictionary<Guid, StudentRecordDuplicateDto>();
		foreach (var row in fixture.Rows)
			oracles[row.Id] = await fixture.Service.CreateAsync(new() { StudentId = row.StudentId, OtherStudentId = row.OtherStudentId, Reason = "Existing reviewed duplicate evidence" });
		if (contactMasked)
		{
			var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks.Add(new() { FieldGroup = "Contact", AllowedRoleNames = [] });
			await PublishDirectoryPolicyAsync(policy);
			foreach (var expected in oracles.Values)
			{
				expected.StudentName = "Restricted student"; expected.OtherStudentName = "Restricted student";
				expected.Reason = "Restricted"; expected.ReviewReason = "Restricted"; expected.CanReview = false;
			}
		}
		async Task<(List<StudentRecordDuplicateDto> Items, long Total, int Reads)> Measure(int size) => await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			var page = await fixture.Service.GetListAsync(new() { MaxResultCount = size });
			return (page.Items.ToList(), page.TotalCount, counter.ReadCommands.Count(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal) && sql.Contains("\"PhotoBlobName\"", StringComparison.Ordinal)));
		});
		var one = await Measure(1); var four = await Measure(4);
		one.Total.ShouldBe(4); four.Total.ShouldBe(4);
		four.Items.Select(item => item.Id).ShouldBe(fixture.Rows.AsEnumerable().Reverse().Select(row => row.Id));
		foreach (var actual in four.Items) JsonSerializer.Serialize(actual).ShouldBe(JsonSerializer.Serialize(oracles[actual.Id]));
		JsonSerializer.Serialize(one.Items.Single()).ShouldBe(JsonSerializer.Serialize(four.Items[0]));
		var next = await fixture.Service.GetListAsync(new() { SkipCount = 1, MaxResultCount = 1 });
		next.TotalCount.ShouldBe(4); JsonSerializer.Serialize(next.Items.Single()).ShouldBe(JsonSerializer.Serialize(four.Items[1]));
		Console.WriteLine($"Duplicate list Student material reads: page1={one.Reads}, page4={four.Reads}, Contact masked={contactMasked}.");
		one.Reads.ShouldBe(2, "Each duplicate row still authorizes both students; names reuse the Student already returned by that row's readable guard.");
		four.Reads.ShouldBe(8, "Every row must run its own current authority checks, including the shared student's repeated checks across rows.");
	});

	[Fact]
	public Task Records_duplicate_list_keeps_current_actor_scope_tenant_deleted_postwrite_and_second_student_short_circuit_behavior() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await DuplicateReadFixtureAsync(1); var row = fixture.Rows.Single();
		(await fixture.Service.GetListAsync(new())).Items.Single().CanReview.ShouldBeTrue();
		await SetPortalFixturePropertyAsync<Student>(row.StudentId, nameof(Student.FirstName), "Fresh authorized duplicate name");
		(await fixture.Service.GetListAsync(new())).Items.Single().StudentName.ShouldStartWith("Fresh authorized duplicate name");
		await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), false);
		try { await Should.ThrowAsync<AbpAuthorizationException>(() => fixture.Service.GetListAsync(new())); }
		finally { await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), true); }
		var staff = Guid.NewGuid(); await EnsureTestIdentityUserAsync(staff, "duplicate-read-unassigned-" + staff.ToString("N"));
		using (_principalAccessor.Change(ProfilePrincipal(staff))) (await fixture.Service.GetListAsync(new())).TotalCount.ShouldBe(0);
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<AbpAuthorizationException>(() => fixture.Service.GetListAsync(new()));
		await SetPortalFixturePropertyAsync<Student>(row.OtherStudentId, nameof(Student.TenantId), Guid.NewGuid());
		try { (await fixture.Service.GetListAsync(new())).TotalCount.ShouldBe(0); }
		finally { await SetPortalFixturePropertyAsync<Student>(row.OtherStudentId, nameof(Student.TenantId), TestTenantId); }
		await SetPortalFixturePropertyAsync<Student>(row.OtherStudentId, nameof(Student.IsDeleted), true);
		try
		{
			(await fixture.Service.GetListAsync(new())).TotalCount.ShouldBe(0);
			await Should.ThrowAsync<EntityNotFoundException>(() => fixture.Service.CreateAsync(new()
			{ StudentId = row.OtherStudentId, OtherStudentId = row.StudentId, Reason = "Missing student remains unavailable" }));
		}
		finally { await SetPortalFixturePropertyAsync<Student>(row.OtherStudentId, nameof(Student.IsDeleted), false); }
		// The original short-circuit does not run second-student eligibility when the
		// first student's content is masked; it still runs that student's scope guard.
		await WithUnitOfWorkAsync(async () =>
		{
			var cases = GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
			foreach (var item in await cases.GetListAsync(item => item.StudentId == row.OtherStudentId)) await cases.DeleteAsync(item, true);
		});
		var mask = StudentRecordsPolicy.Starter(); mask.Access.Masks.Add(new() { FieldGroup = "Contact", AllowedRoleNames = [] });
		await PublishDirectoryPolicyAsync(mask);
		var hidden = (await fixture.Service.GetListAsync(new())).Items.Single();
		hidden.StudentName.ShouldBe("Restricted student"); hidden.OtherStudentName.ShouldBe("Restricted student"); hidden.CanReview.ShouldBeFalse();
		await SetInlineFormPolicyAsync(StudentRecordsPolicy.Starter(), 2);
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.GetListAsync(new()))).Code.ShouldBe("Students:Records:OnboardingIncomplete");
	});

	private async Task<(IStudentRecordDuplicateAppService Service, List<StudentRecordDuplicateReview> Rows)> DuplicateReadFixtureAsync(int count)
	{
		await EnsureTestIdentityUserAsync(TestUserId, "duplicate-read-actor");
		var requester = Guid.NewGuid(); await EnsureTestIdentityUserAsync(requester, "duplicate-read-requester-" + requester.ToString("N"));
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var shared = await CreateExistingStudentAsync(offering, "Shared duplicate", "Student"); await GrantCompletedRecordAsync(shared);
		var result = new List<StudentRecordDuplicateReview>();
		for (var index = 0; index < count; index++)
		{
			var other = await CreateExistingStudentAsync(offering, "Duplicate " + index, "Student"); await GrantCompletedRecordAsync(other);
			var item = new StudentRecordDuplicateReview(Guid.NewGuid(), TestTenantId, shared.Id, other.Id, requester, "Private guardian evidence " + index);
			if (index == 1) item.Resolve(TestUserId, true, "Both independent school records were reviewed");
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<StudentRecordDuplicateReview, Guid>>().InsertAsync(item, true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.Entry(item).Property(row => row.CreationTime).CurrentValue = DateTime.UtcNow.Date.AddDays(-2).AddMinutes(index);
				await db.SaveChangesAsync();
			});
			result.Add(item);
		}
		return (GetRequiredService<IStudentRecordDuplicateAppService>(), result);
	}
}
