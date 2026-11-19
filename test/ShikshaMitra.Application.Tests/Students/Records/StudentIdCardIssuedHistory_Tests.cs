using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Icard_issued_history_uses_current_saved_placement_but_retains_frozen_card_and_older_card_year() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var student = fixture.Students.Single();
		var issued = (await fixture.Service.IssueAsync(new() { StudentIds = [student.Id], IdempotencyKey = "issued-history-placement" })).Single();
		var next = await NextRecordYearAsync(fixture.Year);
		await WithUnitOfWorkAsync(async () =>
		{
			var current = await _studentRepository.GetAsync(student.Id); var standard = await _standardRepository.GetAsync(next.Standard);
			var stage = standard.EducationMediumStage; var medium = stage.EducationMedium;
			current.UpdateAcademicPlacement(next.Year, medium.EducationBoardId, medium.Id, stage.Id, next.Standard, next.Division);
			current.UpdateGeneralInformation("Current", null, "Student"); await _studentRepository.UpdateAsync(current, true);
		});
		(await fixture.Service.GetIssuedHistoryAsync(HistoryQuery(student))).TotalCount.ShouldBe(0);
		var currentStudent = await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(student.Id));
		var query = HistoryQuery(currentStudent); query.Search = "Current Student";
		var page = await fixture.Service.GetIssuedHistoryAsync(query);
		page.TotalCount.ShouldBe(1); page.Items.Single().Id.ShouldBe(issued.Id!.Value);
		page.Items.Single().StudentName.ShouldBe("Current Student"); page.Items.Single().StandardName.ShouldBe("Class 2");
		var frozen = (await fixture.Service.GetHistoryAsync(student.Id)).Single();
		frozen.StudentSnapshot["StudentName"].ShouldBe("IcardLibrary0 Student");
		frozen.StudentSnapshot["Class"].ShouldBe(issued.StudentSnapshot["Class"]);
		(await fixture.Service.GetPdfAsync(issued.Id.Value)).Content.Take(4).ToArray().ShouldBe("%PDF"u8.ToArray());
	});

	[Fact]
	public Task Icard_issued_history_search_paging_status_and_projection_are_bounded_and_stable() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(2); var first = fixture.Students[0]; var second = fixture.Students[1];
		await SetPortalFixturePropertyAsync<Student>(first.Id, nameof(Student.GrNumber), "HISTORY-GR-77");
		var at = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var ids = new[] { Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000003") };
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentIdCard, Guid>>();
			for (var index = 0; index < ids.Length; index++)
			{
				var card = new StudentIdCard(ids[index], TestTenantId, index < 2 ? first.Id : second.Id, "HISTORY-CARD-" + index,
					"history-paging-" + index, "{invalid frozen JSON is never selected}", "{also invalid}", at, TestUserId, fixture.Year);
				if (index == 1) card.MarkPrinted(); if (index == 2) card.Void();
				await repository.InsertAsync(card, true);
			}
			await repository.InsertAsync(new StudentIdCard(Guid.NewGuid(), Guid.NewGuid(), first.Id, "FOREIGN-HISTORY-CARD", "foreign-history",
				"{}", "{}", at, TestUserId, fixture.Year), true);
		});
		var input = HistoryQuery(first); input.MaxResultCount = 1;
		var measured = await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await fixture.Service.GetIssuedHistoryAsync(input);
			counter.ReadCommands.ShouldAllBe(sql => !sql.Contains("StudentSnapshotJson", StringComparison.Ordinal) && !sql.Contains("TemplateSnapshotJson", StringComparison.Ordinal));
			db.ChangeTracker.Entries<Student>().ShouldBeEmpty(); return result;
		});
		measured.TotalCount.ShouldBe(3); measured.Items.Single().Id.ShouldBe(ids[0]);
		input.SkipCount = 1; (await fixture.Service.GetIssuedHistoryAsync(input)).Items.Single().Id.ShouldBe(ids[1]);
		input.SkipCount = 0; input.MaxResultCount = 100; input.Search = "history-gr-77";
		(await fixture.Service.GetIssuedHistoryAsync(input)).TotalCount.ShouldBe(2);
		input.Search = "history-card-2"; var voided = await fixture.Service.GetIssuedHistoryAsync(input);
		voided.TotalCount.ShouldBe(1); voided.Items.Single().Status.ShouldBe("Voided");
		input.Search = "IcardLibrary1 Student"; (await fixture.Service.GetIssuedHistoryAsync(input)).Items.Single().StudentId.ShouldBe(second.Id);
		input.Search = null; input.Sorting = "status desc";
		(await fixture.Service.GetIssuedHistoryAsync(input)).Items.Select(row => row.Status).ShouldBe(["Voided", "Printed", "Issued"]);
		input.Sorting = "cardNumber desc"; (await fixture.Service.GetIssuedHistoryAsync(input)).Items.Select(row => row.Id).ShouldBe(ids.Reverse());
	});

	[Fact]
	public Task Icard_issued_history_narrow_division_scope_does_not_leak_through_visible_class_ancestors() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var student = fixture.Students.Single();
		await fixture.Service.IssueAsync(new() { StudentIds = [student.Id], IdempotencyKey = "history-narrow" });
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, "history-narrow-" + actor.ToString("N"));
		var division = await WithUnitOfWorkAsync(async () =>
		{
			var sibling = new Division(Guid.NewGuid(), TestTenantId, "History sibling division", student.StandardId!.Value, 30, "History sibling room");
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(sibling, true);
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: actor, academicYearId: fixture.Year,
				educationBoardId: student.AcademicBoardId, educationMediumId: student.AcademicMediumId, educationMediumStageId: student.AcademicMediumStageId,
				standardId: student.StandardId, divisionId: sibling.Id), true); return sibling.Id;
		});
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
		{
			var input = HistoryQuery(student); input.DivisionId = null;
			(await fixture.Service.GetIssuedHistoryAsync(input)).TotalCount.ShouldBe(0);
			input.DivisionId = student.DivisionId ?? Guid.NewGuid();
			await Should.ThrowAsync<BusinessException>(() => fixture.Service.GetIssuedHistoryAsync(input));
			input.DivisionId = division; (await fixture.Service.GetIssuedHistoryAsync(input)).Items.ShouldBeEmpty();
		}
	});

	[Theory]
	[InlineData("current")]
	[InlineData("issued")]
	public Task Icard_issued_history_excludes_identity_masks_before_count_and_search_in_both_policy_years(string maskedYear) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var student = fixture.Students.Single(); var older = await NextRecordYearAsync(fixture.Year);
		await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<StudentIdCard, Guid>>().InsertAsync(new StudentIdCard(Guid.NewGuid(), TestTenantId, student.Id,
			"MASKED-CARD", "history-mask", "{}", "{}", DateTime.UtcNow, TestUserId, older.Year), true));
		var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks.Add(new() { FieldGroup = "Identity", AllowedRoleNames = [] });
		var year = maskedYear == "current" ? fixture.Year : older.Year;
		await WithUnitOfWorkAsync(async () =>
		{
			var config = new StudentRecordsConfiguration(Guid.NewGuid(), TestTenantId, year, policy.ToJson());
			await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().InsertAsync(config, true);
			var version = new StudentRecordsPolicyVersion(Guid.NewGuid(), TestTenantId, config.Id, year, 1, policy.ToJson());
			await GetRequiredService<IRepository<StudentRecordsPolicyVersion, Guid>>().InsertAsync(version, true); config.Publish(version.Id);
			await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().UpdateAsync(config, true);
		});
		(await fixture.Service.GetIssuedHistoryAsync(HistoryQuery(student))).TotalCount.ShouldBe(0);
		var input = HistoryQuery(student); input.Search = "MASKED-CARD";
		(await fixture.Service.GetIssuedHistoryAsync(input)).Items.ShouldBeEmpty();
	});

	[Fact]
	public Task Icard_issued_history_enforces_current_scope_tenant_actor_and_records_eligibility() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var student = fixture.Students.Single();
		await fixture.Service.IssueAsync(new() { StudentIds = [student.Id], IdempotencyKey = "history-authority" });
		var input = HistoryQuery(student);
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, "history-no-scope-" + actor.ToString("N"));
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
			(await Should.ThrowAsync<BusinessException>(() => fixture.Service.GetIssuedHistoryAsync(input))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		await SetPortalFixturePropertyAsync<Student>(student.Id, nameof(Student.TenantId), Guid.NewGuid());
		try { (await fixture.Service.GetIssuedHistoryAsync(input)).TotalCount.ShouldBe(0); }
		finally { await SetPortalFixturePropertyAsync<Student>(student.Id, nameof(Student.TenantId), TestTenantId); }
		await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), false);
		try { await Should.ThrowAsync<AbpAuthorizationException>(() => fixture.Service.GetIssuedHistoryAsync(input)); }
		finally { await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), true); }
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var cases = await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetListAsync(c => c.StudentId == student.Id);
			foreach (var item in cases) db.Entry(item).Property("IsDeleted").CurrentValue = true;
			await db.SaveChangesAsync();
		});
		(await fixture.Service.GetIssuedHistoryAsync(input)).TotalCount.ShouldBe(0);
	});

	[Theory]
	[InlineData("year")][InlineData("board")][InlineData("medium")][InlineData("division")]
	[InlineData("skip")][InlineData("zero-page")][InlineData("large-page")][InlineData("sort")][InlineData("search")]
	public Task Icard_issued_history_rejects_invalid_queries_without_broadening(string invalid) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var input = HistoryQuery(fixture.Students.Single());
		switch (invalid)
		{
			case "year": input.AcademicYearId = Guid.Empty; break;
			case "board": input.BoardId = Guid.NewGuid(); break;
			case "medium": input.MediumId = Guid.NewGuid(); break;
			case "division": input.StandardId = null; input.DivisionId = Guid.NewGuid(); break;
			case "skip": input.SkipCount = -1; break;
			case "zero-page": input.MaxResultCount = 0; break;
			case "large-page": input.MaxResultCount = 101; break;
			case "sort": input.Sorting = "studentSnapshot asc"; break;
			case "search": input.Search = new string('x', 151); break;
		}
		var error = await Record.ExceptionAsync(() => fixture.Service.GetIssuedHistoryAsync(input));
		(error is BusinessException or AbpValidationException).ShouldBeTrue(error?.ToString());
	});

	private static StudentIdCardHistoryQueryDto HistoryQuery(Student student) => new()
	{
		AcademicYearId = student.AcademicYearId!.Value, BoardId = student.AcademicBoardId!.Value,
		MediumId = student.AcademicMediumId, StandardId = student.StandardId, DivisionId = student.DivisionId
	};
}
