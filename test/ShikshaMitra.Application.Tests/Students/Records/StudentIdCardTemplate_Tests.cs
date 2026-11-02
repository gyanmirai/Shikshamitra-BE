using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
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
	[Fact]
	public Task Icard_templates_legacy_issued_snapshot_does_not_reveal_newly_recognized_cohort_fields_or_rewrite_frozen_JSON() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var student = fixture.Students.Single(); var hidden = Guid.NewGuid();
		var oldTemplate = IcardDesign("Legacy frozen card", fixture.Year, hidden);
		oldTemplate.SelectionRestricted = true;
		var raw = JsonSerializer.Serialize(oldTemplate, new JsonSerializerOptions(JsonSerializerDefaults.Web));
		var id = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<StudentIdCard, Guid>>().InsertAsync(new StudentIdCard(id, TestTenantId, student.Id,
			"LEGACY-ICARD-0001", "legacy-cohort-key:" + student.Id.ToString("N"), "{\"StudentName\":\"Frozen synthetic student\"}", raw, DateTime.UtcNow, TestUserId, fixture.Year), true));
		var history = (await fixture.Service.GetHistoryAsync(student.Id)).Single();
		history.TemplateSnapshot.Selection.ShouldBeNull("Previously unknown JSON fields must not become a disclosure when the DTO is expanded.");
		history.TemplateSnapshot.SelectionRestricted.ShouldBeFalse();
		JsonSerializer.Serialize(history).ShouldNotContain(hidden.ToString());
		var replay = (await fixture.Service.IssueAsync(new() { StudentIds = [student.Id], IdempotencyKey = "legacy-cohort-key" })).Single();
		replay.Id.ShouldBe(id); replay.TemplateSnapshot.Selection.ShouldBeNull(); replay.CardNumber.ShouldBe("LEGACY-ICARD-0001");
		(await fixture.Service.ReprintAsync(id, new() { Reason = "Retain original legacy design" })).Content.Take(4).ToArray().ShouldBe("%PDF"u8.ToArray());
		(await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<StudentIdCard, Guid>>().GetAsync(id)).TemplateSnapshotJson)).ShouldBe(raw);
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Icard_templates_hidden_class_or_division_filters_are_restricted_even_without_selected_students(bool siblingDivision) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var student = fixture.Students.Single();
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, "icard-narrow-" + actor.ToString("N"));
		var other = await WithUnitOfWorkAsync(async () =>
		{
			var allowedDivision = new Division(Guid.NewGuid(), TestTenantId, "Allowed I-card division", student.StandardId!.Value, 30, "Allowed I-card room");
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(allowedDivision, true);
			var standardId = student.StandardId!.Value;
			if (!siblingDivision)
			{
				var standard = new Standard(Guid.NewGuid(), TestTenantId, "Other I-card class", 8, student.AcademicMediumStageId!.Value, null);
				await _standardRepository.InsertAsync(standard, true); standardId = standard.Id;
			}
			var division = new Division(Guid.NewGuid(), TestTenantId, "Other I-card division", standardId, 30, "Other I-card room");
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: actor, academicYearId: fixture.Year,
				educationBoardId: student.AcademicBoardId, educationMediumId: student.AcademicMediumId, educationMediumStageId: student.AcademicMediumStageId,
				standardId: student.StandardId, divisionId: allowedDivision.Id), true);
			return (Standard: standardId, Division: division.Id, AllowedDivision: allowedDivision.Id);
		});
		var input = IcardDesign("Out-of-scope filters", fixture.Year);
		if (siblingDivision) input.Selection!.DivisionIds = [other.Division]; else input.Selection!.StandardIds = [other.Standard];
		var saved = await fixture.Service.CreateTemplateAsync(input); var before = await IcardRawTemplateAsync(saved.Id!.Value);
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
		{
			var actorScope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
			actorScope.IsAdmin.ShouldBeFalse(); actorScope.HasTenantWideScope().ShouldBeFalse();
			actorScope.DivisionIds.ShouldBe([other.AllowedDivision]);
			actorScope.WildcardBoardAcademicYearIds.ShouldBeEmpty(); actorScope.WildcardMediumBoardIds.ShouldBeEmpty();
			actorScope.WildcardMediumStageMediumIds.ShouldBeEmpty(); actorScope.WildcardStandardMediumStageIds.ShouldBeEmpty();
			actorScope.WildcardDivisionStandardIds.ShouldBeEmpty();
			var hidden = await fixture.Service.GetTemplateByIdAsync(saved.Id.Value);
			hidden.SelectionRestricted.ShouldBeTrue("An academic-year assignment does not authorize a sibling class or division filter."); hidden.Selection.ShouldBeNull();
			await Should.ThrowAsync<BusinessException>(() => fixture.Service.CreateTemplateAsync(input));
			await Should.ThrowAsync<BusinessException>(() => fixture.Service.DeleteTemplateAsync(saved.Id.Value));
			(await IcardRawTemplateAsync(saved.Id.Value)).ShouldBe(before);
		}
	});
	[Fact]
	public Task Icard_templates_transfer_only_same_year_and_reject_stale_updates_without_partial_changes() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(2); var service = fixture.Service; var first = fixture.Students[0]; var second = fixture.Students[1];
		var next = await NextRecordYearAsync(fixture.Year);
		await WithUnitOfWorkAsync(async () =>
		{
			var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, first.Id, next.Year, next.Standard, next.Division);
			enrollment.Deactivate(); await _studentEnrollmentRepository.InsertAsync(enrollment, true);
		});
		var source = await service.CreateTemplateAsync(IcardDesign("Original", fixture.Year, first.Id, second.Id));
		var historical = await service.CreateTemplateAsync(IcardDesign("Other year", next.Year, first.Id));
		var target = await service.CreateTemplateAsync(IcardDesign("Transferred", fixture.Year, first.Id));
		(await service.GetTemplateByIdAsync(source.Id!.Value)).Selection!.StudentIds.ShouldBe([second.Id]);
		(await service.GetTemplateByIdAsync(target.Id!.Value)).Selection!.StudentIds.ShouldBe([first.Id]);
		JsonSerializer.Serialize(await service.GetTemplateByIdAsync(historical.Id!.Value)).ShouldBe(JsonSerializer.Serialize(historical));
		source.Name = "Stale overwrite";
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateTemplateAsync(source.Id.Value, source))).Code.ShouldBe("Students:Records:IdTemplateConflict");
		(await service.GetTemplateByIdAsync(source.Id.Value)).Selection!.StudentIds.ShouldBe([second.Id]);
		(await service.GetTemplateByIdAsync(target.Id.Value)).Selection!.StudentIds.ShouldBe([first.Id]);
		var fresh = await service.GetTemplateByIdAsync(target.Id.Value); fresh.Selection!.StudentIds.Clear();
		(await service.UpdateTemplateAsync(target.Id.Value, fresh)).Selection!.StudentIds.ShouldBeEmpty();
		(await service.GetTemplateByIdAsync(historical.Id.Value)).Selection!.StudentIds.ShouldBe([first.Id]);
	});

	[Fact]
	public Task Icard_templates_restrict_hidden_members_but_design_only_omission_preserves_them_and_denies_destructive_writes() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var service = fixture.Service; var student = fixture.Students.Single();
		var saved = await service.CreateTemplateAsync(IcardDesign("Restricted design", fixture.Year, student.Id));
		var staff = Guid.NewGuid(); await EnsureTestIdentityUserAsync(staff, "icard-no-scope-" + staff.ToString("N"));
		using (_principalAccessor.Change(ProfilePrincipal(staff)))
		{
			var metadata = (await service.GetTemplatesAsync()).Single();
			metadata.Id.ShouldBe(saved.Id); metadata.Name.ShouldBe(saved.Name); metadata.SelectionRestricted.ShouldBeTrue(); metadata.Selection.ShouldBeNull();
			JsonSerializer.Serialize(metadata).ShouldNotContain(student.Id.ToString());
			var detail = await service.GetTemplateByIdAsync(saved.Id!.Value); detail.Selection.ShouldBeNull(); detail.SelectionRestricted.ShouldBeTrue();
			detail.Name = "Design-only revision";
			var revised = await service.UpdateTemplateAsync(saved.Id.Value, detail); revised.Selection.ShouldBeNull(); revised.SelectionRestricted.ShouldBeTrue();
			var before = await IcardRawTemplateAsync(saved.Id.Value);
			revised.Selection = new() { AcademicYearId = fixture.Year };
			await Should.ThrowAsync<BusinessException>(() => service.UpdateTemplateAsync(saved.Id.Value, revised));
			await Should.ThrowAsync<BusinessException>(() => service.DeleteTemplateAsync(saved.Id.Value));
			(await IcardRawTemplateAsync(saved.Id.Value)).ShouldBe(before);
		}
		var allowed = await service.GetTemplateByIdAsync(saved.Id!.Value); allowed.Name.ShouldBe("Design-only revision");
		allowed.Selection!.StudentIds.ShouldBe([student.Id]); allowed.SelectionRestricted.ShouldBeFalse();
		var mask = StudentRecordsPolicy.Starter(); mask.Access.Masks.Add(new() { FieldGroup = "Identity", AllowedRoleNames = [] });
		await PublishDirectoryPolicyAsync(mask);
		(await service.GetTemplateByIdAsync(saved.Id.Value)).Selection.ShouldBeNull();
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(new() { TemplateId = saved.Id, StudentIds = [student.Id] })))
			.Code.ShouldBe("Students:Records:DocumentFieldsRestricted");
	});

	[Theory]
	[InlineData("over100")]
	[InlineData("emptyId")]
	[InlineData("duplicate")]
	[InlineData("missingYear")]
	public Task Icard_templates_reject_invalid_saved_selection_without_creating_a_design(string invalid) => RunCertificationScenarioAsync(async () =>
	{
		var service = GetRequiredService<IStudentIdCardAppService>(); var input = IcardDesign("Invalid selection", Guid.NewGuid(), Guid.NewGuid());
		if (invalid == "over100") input.Selection!.StudentIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();
		if (invalid == "emptyId") input.Selection!.StudentIds = [Guid.Empty];
		if (invalid == "duplicate") input.Selection!.StudentIds.Add(input.Selection.StudentIds[0]);
		if (invalid == "missingYear") input.Selection!.AcademicYearId = null;
		(await Should.ThrowAsync<BusinessException>(() => service.CreateTemplateAsync(input))).Code.ShouldBe("Students:Records:InvalidIdTemplate");
		(await service.GetTemplatesAsync()).ShouldBeEmpty();
	});

	[Fact]
	public Task Icard_templates_selected_design_preview_is_readonly_and_issued_snapshots_survive_update_delete_and_reprint() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var service = fixture.Service; var student = fixture.Students.Single();
		var oldest = await service.CreateTemplateAsync(IcardDesign("Legacy oldest"));
		var selected = await service.CreateTemplateAsync(IcardDesign("Selected frozen design", fixture.Year, student.Id));
		var before = await IcardRawTemplateAsync(selected.Id!.Value);
		var preview = (await service.PreviewAsync(new() { TemplateId = selected.Id, StudentIds = [student.Id] })).Single();
		preview.TemplateSnapshot.Name.ShouldBe("Selected frozen design"); preview.TemplateSnapshot.Selection.ShouldBeNull();
		(await IcardRawTemplateAsync(selected.Id.Value)).ShouldBe(before);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentIdCard, Guid>>().CountAsync())).ShouldBe(0);
		(await service.PreviewAsync(new() { StudentIds = [student.Id] })).Single().TemplateSnapshot.Name.ShouldBe(oldest.Name);
		var input = new IssueStudentIdCardsDto { TemplateId = selected.Id, StudentIds = [student.Id], IdempotencyKey = "selected-design-frozen" };
		var issued = (await service.IssueAsync(input)).Single(); issued.TemplateSnapshot.Selection.ShouldBeNull();
		(await service.IssueAsync(input)).Single().Id.ShouldBe(issued.Id);
		selected.Name = "Changed live design"; selected.Selection = new() { AcademicYearId = fixture.Year };
		await service.UpdateTemplateAsync(selected.Id.Value, selected); await service.DeleteTemplateAsync(selected.Id.Value);
		var replayAfterDeletion = (await service.IssueAsync(input)).Single(); replayAfterDeletion.Id.ShouldBe(issued.Id);
		replayAfterDeletion.TemplateSnapshot.Name.ShouldBe("Selected frozen design");
		var history = (await service.GetHistoryAsync(student.Id)).Single(); history.TemplateSnapshot.Name.ShouldBe("Selected frozen design");
		history.CardNumber.ShouldBe(issued.CardNumber); history.StudentSnapshot.ShouldBe(issued.StudentSnapshot);
		(await service.ReprintAsync(issued.Id!.Value, new() { Reason = "Frozen issued card reprint" })).Content.Take(4).ToArray().ShouldBe("%PDF"u8.ToArray());
		(await service.GetHistoryAsync(student.Id)).Single().TemplateSnapshot.Name.ShouldBe("Selected frozen design");
		await Should.ThrowAsync<EntityNotFoundException>(() => service.PreviewAsync(new() { TemplateId = selected.Id, StudentIds = [student.Id] }));
	});

	[Fact]
	public Task Icard_templates_catalogue_batches_current_scope_and_member_years_without_student_graph_hydration() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(8); var service = fixture.Service;
		await service.CreateTemplateAsync(IcardDesign("Catalogue 0", fixture.Year, fixture.Students[0].Id));
		async Task<(List<StudentIdCardTemplateDto> Rows, int Reads, int Students)> Measure() => await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			var rows = await service.GetTemplatesAsync();
			counter.ReadCommands.Where(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)).ShouldAllBe(sql => !sql.Contains("\"PhotoBlobName\"", StringComparison.Ordinal));
			return (rows, counter.ReadCommands.Count, db.ChangeTracker.Entries<Student>().Count());
		});
		var one = await Measure();
		for (var index = 1; index < fixture.Students.Count; index++) await service.CreateTemplateAsync(IcardDesign("Catalogue " + index, fixture.Year, fixture.Students[index].Id));
		var eight = await Measure(); eight.Rows.Count.ShouldBe(8);
		eight.Rows.SelectMany(t => t.Selection!.StudentIds).Order().ShouldBe(fixture.Students.Select(s => s.Id).Order());
		eight.Rows.ShouldAllBe(t => !t.SelectionRestricted); eight.Students.ShouldBe(0);
		Console.WriteLine($"I-card catalogue same-year templates1/8 SQL reads {one.Reads}/{eight.Reads}; Student aggregate tracking {eight.Students}.");
		eight.Reads.ShouldBeLessThanOrEqualTo(one.Reads, "Scope, member/year projections, roles and same-year policy are shared only inside this read operation.");
		var designOnly = eight.Rows[0]; designOnly.Name = "Narrow target edit"; designOnly.Selection = null;
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync(); db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			(await service.UpdateTemplateAsync(designOnly.Id!.Value, designOnly)).Name.ShouldBe("Narrow target edit");
			var reads = counter.ReadCommands.Where(sql => sql.Contains("FROM \"AppStudentIdCardTemplates\"", StringComparison.Ordinal)).ToList();
			reads.Count.ShouldBe(1); reads[0].ShouldContain("LIMIT");
		});
	});

	[Fact]
	public Task Icard_templates_foreign_missing_year_and_changed_student_authority_cannot_transfer_assignments() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1); var service = fixture.Service; var student = fixture.Students.Single();
		var saved = await service.CreateTemplateAsync(IcardDesign("Authority source", fixture.Year, student.Id)); var before = await IcardRawTemplateAsync(saved.Id!.Value);
		var wrongYear = await NextRecordYearAsync(fixture.Year);
		(await Should.ThrowAsync<BusinessException>(() => service.CreateTemplateAsync(IcardDesign("Wrong year", wrongYear.Year, student.Id))))
			.Code.ShouldBe("Students:Records:InvalidIdStudentSelection");
		await SetPortalFixturePropertyAsync<Student>(student.Id, nameof(Student.TenantId), Guid.NewGuid());
		try { await Should.ThrowAsync<EntityNotFoundException>(() => service.CreateTemplateAsync(IcardDesign("Foreign student", fixture.Year, student.Id))); }
		finally { await SetPortalFixturePropertyAsync<Student>(student.Id, nameof(Student.TenantId), TestTenantId); }
		await SetPortalFixturePropertyAsync<Student>(student.Id, nameof(Student.IsDeleted), true);
		try
		{
			(await service.GetTemplateByIdAsync(saved.Id.Value)).SelectionRestricted.ShouldBeTrue();
			await Should.ThrowAsync<EntityNotFoundException>(() => service.DeleteTemplateAsync(saved.Id.Value));
		}
		finally { await SetPortalFixturePropertyAsync<Student>(student.Id, nameof(Student.IsDeleted), false); }
		await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), false);
		try
		{
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetTemplatesAsync());
			var missingTemplate = Guid.NewGuid();
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.PreviewAsync(new() { TemplateId = missingTemplate, StudentIds = [student.Id] }));
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.IssueAsync(new() { TemplateId = missingTemplate, StudentIds = [student.Id], IdempotencyKey = "inactive-missing-template" }));
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.ExportPdfAsync(new() { TemplateId = missingTemplate, StudentIds = [student.Id], CardsPerPage = 8 }));
		}
		finally { await SetPortalFixturePropertyAsync<IdentityUser>(TestUserId, nameof(IdentityUser.IsActive), true); }
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetTemplateByIdAsync(saved.Id.Value));
		(await IcardRawTemplateAsync(saved.Id.Value)).ShouldBe(before);
	});

	private async Task<(IStudentIdCardAppService Service, List<Student> Students, Guid Year)> IcardLibraryFixtureAsync(int count)
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id); var students = new List<Student>();
		for (var index = 0; index < count; index++)
		{
			var student = await CreateExistingStudentAsync(offering, "IcardLibrary" + index, "Student"); await GrantCompletedRecordAsync(student); students.Add(student);
		}
		return (GetRequiredService<IStudentIdCardAppService>(), students, students[0].AcademicYearId!.Value);
	}
	private static StudentIdCardTemplateDto IcardDesign(string name, Guid? year = null, params Guid[] students) => new()
	{
		Name = name, SchoolName = "Synthetic I-card school", ShowPhoto = false,
		Selection = year.HasValue ? new() { AcademicYearId = year, StudentIds = students.ToList() } : null
	};
	private Task<string> IcardRawTemplateAsync(Guid id) => WithUnitOfWorkAsync(async () =>
	{
		var row = await GetRequiredService<IRepository<StudentIdCardTemplate, Guid>>().GetAsync(id);
		return row.ConfigurationJson + "|" + row.ConcurrencyStamp;
	});
}
