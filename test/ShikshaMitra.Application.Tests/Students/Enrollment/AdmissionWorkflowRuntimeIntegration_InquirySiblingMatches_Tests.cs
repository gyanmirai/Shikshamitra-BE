using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Inquiry_list_reuses_visible_siblings_and_skips_matching_for_closed_rows() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, first, second) = await CreatePendingFamilyConfirmationFixtureAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var input = new AdmissionInquiryListInput { AdmissionOfferingId = offering.Id, MaxResultCount = 25 };
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await service.GetListAsync(input);
			result.Items.Where(row => row.Id == first.Id || row.Id == second.Id).ShouldAllBe(row => row.HasPossibleSiblingMatch);
			result.TotalCount.ShouldBe(2);
			var familyApplicantReads = capture.ReadCommands.Count(sql =>
				sql.Contains("FROM \"AppAdmissionApplicants\" AS", StringComparison.Ordinal) &&
				sql.Contains(".\"FamilyId\" IN (", StringComparison.Ordinal));
			Console.WriteLine($"Pending inquiry page: {capture.ReadCount} reads, {familyApplicantReads} applicant reads by family.");
			familyApplicantReads.ShouldBe(2, "The page needs one linked-sibling read and one candidate snapshot.");
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			foreach (var id in new[] { first.Id, second.Id })
			{
				var inquiry = await repository.GetAsync(id);
				inquiry.SetStatus(AdmissionInquiryStatus.NotInterested);
				await repository.UpdateAsync(inquiry, true);
			}
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await service.GetListAsync(input);
			result.TotalCount.ShouldBe(2);
			result.Items.ShouldAllBe(row => !row.HasPossibleSiblingMatch);
			Console.WriteLine($"Closed inquiry page: {capture.ReadCount} reads.");
			capture.ReadCommands.ShouldNotContain(sql => sql.Contains("FROM \"AppAdmissionGuardians\"", StringComparison.Ordinal));
		});
	});

	[Fact]
	public Task Inquiry_sibling_matches_exclude_explicit_guardian_contact_but_keep_saved_parent_contacts() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, first, second) = await CreatePendingFamilyConfirmationFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(second.ApplicantId!.Value);
			AdmissionPrimaryContactSelection.Capture(applicant, "guardian", "Main Guardian", "9876543210", "amit@example.com", true, TestUserId, DateTime.UtcNow, false);
			AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Own Father", "9876543211", "other-parent@example.com");
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var rows = (await service.GetListAsync(new() { AdmissionOfferingId = offering.Id, MaxResultCount = 100 })).Items;
		rows.Where(row => row.Id == first.Id || row.Id == second.Id).ShouldAllBe(row => !row.HasPossibleSiblingMatch);
		using (var none = await ReadInquirySiblingMatchesAsync(second.Id)) none.RootElement.GetProperty("Matches").GetArrayLength().ShouldBe(0);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(second.ApplicantId!.Value);
			AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Own Father", "9876543210", "other-parent@example.com");
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		using var matched = await ReadInquirySiblingMatchesAsync(second.Id);
		matched.RootElement.GetProperty("Matches").EnumerateArray().Single().GetProperty("ApplicantId").GetGuid().ShouldBe(first.ApplicantId!.Value);
	});

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public Task Inquiry_sibling_matches_exclude_duplicate_child_identity_and_preserve_distinct_birth_dates(bool missingBirthDate, bool differentBirthDate) => RunCertificationScenarioAsync(async () =>
	{
		var (offering, first, second) = await CreatePendingFamilyConfirmationFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var original = await _applicantRepository.GetAsync(first.ApplicantId!.Value);
			original.UpdateProfile(original.FirstName, original.MiddleName, original.LastName, new DateTime(2016, 4, 12), original.Gender);
			await _applicantRepository.UpdateAsync(original, true);
			var duplicate = await _applicantRepository.GetAsync(second.ApplicantId!.Value);
			duplicate.UpdateProfile(original.FirstName, original.MiddleName, original.LastName,
				missingBirthDate ? null : differentBirthDate ? original.DateOfBirth!.Value.AddYears(1) : original.DateOfBirth, original.Gender);
			await _applicantRepository.UpdateAsync(duplicate, true);
			var inquiry = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(second.Id);
			inquiry.UpdateDetails("  " + first.ApplicantName.ToUpperInvariant() + "  ", inquiry.NormalizedPhone, inquiry.Email, inquiry.Source, inquiry.SourceDetail, inquiry.PreferredLanguageCode);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().UpdateAsync(inquiry, true);
		});
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var rows = (await service.GetListAsync(new() { AdmissionOfferingId = offering.Id, MaxResultCount = 100 })).Items;
		rows.Where(row => row.Id == first.Id || row.Id == second.Id).ShouldAllBe(row => row.HasPossibleSiblingMatch == differentBirthDate);
		using var matches = await ReadInquirySiblingMatchesAsync(second.Id);
		matches.RootElement.GetProperty("Matches").GetArrayLength().ShouldBe(differentBirthDate ? 1 : 0);
		var current = await service.GetAsync(second.Id);
		if (differentBirthDate)
		{
			var confirmed = await service.ConfirmFamilyAsync(second.Id, new() { FamilyLinkOnly = true, ExistingFamilyId = first.FamilyId!.Value,
				ExistingParentApplicantId = first.ApplicantId!.Value, ConcurrencyStamp = current.ConcurrencyStamp });
			confirmed.FamilyId.ShouldBe(first.FamilyId);
			return;
		}
		(await Should.ThrowAsync<Volo.Abp.BusinessException>(() => service.ConfirmFamilyAsync(second.Id, new()
		{
			FamilyLinkOnly = true, ExistingFamilyId = first.FamilyId!.Value, ExistingParentApplicantId = first.ApplicantId!.Value,
			ConcurrencyStamp = current.ConcurrencyStamp
		}))).Code.ShouldBe(AdmissionErrorCodes.InquiryDuplicateStudent);
		(await service.GetAsync(second.Id)).FamilyId.ShouldBe(second.FamilyId);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>().CountAsync(item => item.InquiryId == second.Id && item.ActivityType == "SiblingFamilyConfirmed"))).ShouldBe(0);
	});

	[Fact]
	public Task Inquiry_sibling_matches_require_each_rows_contact_and_support_normalized_email_only_match() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, first, second) = await CreatePendingFamilyConfirmationFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(second.ApplicantId!.Value);
			AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Other Father", "9876543211", " AMIT@EXAMPLE.COM ");
			await _applicantRepository.UpdateAsync(applicant, true);
			var inquiry = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(second.Id);
			inquiry.UpdateDetails(inquiry.ApplicantName, "9876543211", "unrelated@example.com", inquiry.Source, inquiry.SourceDetail, inquiry.PreferredLanguageCode);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().UpdateAsync(inquiry, true);
			var family = await _familyRepository.GetAsync(second.FamilyId!.Value);
			var guardian = await _guardianRepository.GetAsync(family.PrimaryGuardianId!.Value);
			guardian.UpdateContact("Other Father", null, "9876543211", "unrelated@example.com"); await _guardianRepository.UpdateAsync(guardian, true);
		});
		using (var matches = await ReadInquirySiblingMatchesAsync(second.Id))
		{
			var match = matches.RootElement.GetProperty("Matches").EnumerateArray().Single();
			match.GetProperty("ApplicantId").GetGuid().ShouldBe(first.ApplicantId!.Value);
			match.GetProperty("MatchByEmail").GetBoolean().ShouldBeTrue(); match.GetProperty("MatchByMobile").GetBoolean().ShouldBeFalse();
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(second.ApplicantId!.Value);
			AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Other Father", "9876543211", "no-match@example.com");
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var rows = (await service.GetListAsync(new() { AdmissionOfferingId = offering.Id, MaxResultCount = 100 })).Items;
		rows.Where(row => row.Id == first.Id || row.Id == second.Id).ShouldAllBe(row => !row.HasPossibleSiblingMatch);
		using var none = await ReadInquirySiblingMatchesAsync(second.Id);
		none.RootElement.GetProperty("Matches").GetArrayLength().ShouldBe(0);
	});

	[Fact]
	public Task Inquiry_sibling_matches_hide_linked_student_outside_scope_even_inside_a_confirmed_family() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		var (foreignOffering, _, _) = await CreatePendingFamilyConfirmationFixtureAsync(separateStandard: true);
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		await service.ConfirmFamilyAsync(target.Id, new() { FamilyLinkOnly = true, ExistingFamilyId = source.FamilyId!.Value,
			ExistingParentApplicantId = source.ApplicantId!.Value, ConcurrencyStamp = target.ConcurrencyStamp });
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await CreateExistingStudentAsync(foreignOffering, "Restricted", "Sibling");
			student.UpdateFamilyInfo(new ParentGuardianDetails("Hidden Father", null, null, "amit@example.com", "9876543210", null, null, null, null, null, null, null, null), null, null, null);
			await _studentRepository.UpdateAsync(student, true);
			var applicant = await _applicantRepository.GetAsync(source.ApplicantId!.Value);
			applicant.LinkStudent(student.Id); await _applicantRepository.UpdateAsync(applicant, true);
		});
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, "sibling-student-scope-" + actor.ToString("N"));
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(offering.AdmissionCycleId));
		await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: actor,
			academicYearId: cycle.AcademicYearId, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
			educationMediumStageId: offering.StageId, standardId: offering.StandardId), true));
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
		{
			using var result = await ReadInquirySiblingMatchesAsync(target.Id);
			result.RootElement.GetProperty("Matches").GetArrayLength().ShouldBe(0);
			result.RootElement.GetProperty("Inquiry").GetProperty("HasLinkedSiblings").GetBoolean().ShouldBeFalse();
			var rows = (await service.GetListAsync(new() { AdmissionOfferingId = offering.Id, MaxResultCount = 100 })).Items;
			rows.Single(row => row.Id == target.Id).HasLinkedSiblings.ShouldBeFalse();
			rows.Single(row => row.Id == source.Id).HasPossibleSiblingMatch.ShouldBeFalse();
		}
	});

	[Fact]
	public Task Inquiry_sibling_matches_support_staff_capture_and_do_not_relax_interested_confirmation() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var input = ManagedInquiryInput(offering); input.ParentRelationship = "father";
		var first = await service.CreateAsync(input);
		input.ApplicantFirstName = "Other child"; input.ApplicantName = "Other child";
		input.FormValues.Single(value => value.FieldCode == "APPLICANT_FIRST_NAME").ValueJson = "\"Other child\"";
		input.CreateSeparateFamily = true; input.DuplicateOverrideReason = "Separate captured inquiry for sibling review.";
		var second = await service.CreateAsync(input);
		using var before = await ReadInquirySiblingMatchesAsync(first.Id);
		before.RootElement.GetProperty("Matches").EnumerateArray().ShouldContain(item => item.GetProperty("ApplicantId").GetGuid() == second.ApplicantId);
		second = await service.UpdateAsync(second.Id, InquiryStatusUpdate(second, AdmissionInquiryStatus.Interested));
		(await Should.ThrowAsync<Volo.Abp.BusinessException>(() => service.ConfirmFamilyAsync(second.Id, new()
		{
			FamilyLinkOnly = true, ExistingFamilyId = first.FamilyId!.Value, ExistingParentApplicantId = first.ApplicantId!.Value,
			ConcurrencyStamp = second.ConcurrencyStamp
		}))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
	});

	[Fact]
	public Task Inquiry_sibling_matches_hide_foreign_scope_and_tenant_and_never_return_self() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, first, second) = await CreatePendingFamilyConfirmationFixtureAsync();
		var (_, foreign, _) = await CreatePendingFamilyConfirmationFixtureAsync(separateStandard: true);
		var actor = Guid.NewGuid(); await EnsureTestIdentityUserAsync(actor, "sibling-row-" + actor.ToString("N"));
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(offering.AdmissionCycleId));
		await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: actor,
			academicYearId: cycle.AcademicYearId, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
			educationMediumStageId: offering.StageId, standardId: offering.StandardId), true));
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
		{
			using var result = await ReadInquirySiblingMatchesAsync(first.Id);
			var ids = result.RootElement.GetProperty("Matches").EnumerateArray().Select(item => item.GetProperty("ApplicantId").GetGuid()).ToArray();
			ids.ShouldBe(new[] { second.ApplicantId!.Value });
			await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => ReadInquirySiblingMatchesAsync(foreign.Id));
		}
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => ReadInquirySiblingMatchesAsync(first.Id));
	});

	[Fact]
	public Task Inquiry_sibling_matches_are_symmetric_before_and_after_family_confirmation() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, first, second) = await CreatePendingFamilyConfirmationFixtureAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(second.ApplicantId!.Value);
			AdmissionSiblingParentSelection.CaptureParent(applicant, "mother", "Own Mother", "9876543210", "own-mother@example.com");
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var rows = (await service.GetListAsync(new() { AdmissionOfferingId = offering.Id, MaxResultCount = 100 })).Items;
		foreach (var id in new[] { first.Id, second.Id })
		{
			var row = rows.Single(item => item.Id == id);
			(typeof(AdmissionInquiryDto).GetProperty("HasPossibleSiblingMatch")?.GetValue(row) as bool?).ShouldBe(true);
			row.HasLinkedSiblings.ShouldBeFalse();
			using var matches = await ReadInquirySiblingMatchesAsync(id);
			matches.RootElement.GetProperty("Inquiry").GetProperty("ConcurrencyStamp").GetString().ShouldNotBeNullOrWhiteSpace();
			var other = matches.RootElement.GetProperty("Matches").EnumerateArray().Single();
			other.GetProperty("ApplicantId").GetGuid().ShouldBe(id == first.Id ? second.ApplicantId!.Value : first.ApplicantId!.Value);
			other.GetProperty("CanLinkFamily").GetBoolean().ShouldBeTrue();
		}
		await service.ConfirmFamilyAsync(second.Id, new() { FamilyLinkOnly = true, ExistingFamilyId = first.FamilyId!.Value,
			ExistingParentApplicantId = first.ApplicantId!.Value, ConcurrencyStamp = second.ConcurrencyStamp });
		var latestSecond = await service.GetAsync(second.Id);
		await service.ConfirmFamilyAsync(first.Id, new() { FamilyLinkOnly = true, ExistingFamilyId = latestSecond.FamilyId!.Value,
			ExistingParentApplicantId = second.ApplicantId!.Value, ConcurrencyStamp = first.ConcurrencyStamp });
		rows = (await service.GetListAsync(new() { AdmissionOfferingId = offering.Id, MaxResultCount = 100 })).Items;
		string? familyParents = null;
		foreach (var id in new[] { first.Id, second.Id })
		{
			var row = rows.Single(item => item.Id == id);
			row.HasLinkedSiblings.ShouldBeTrue();
			(typeof(AdmissionInquiryDto).GetProperty("HasPossibleSiblingMatch")?.GetValue(row) as bool?).ShouldBe(false);
			row.SiblingFamilyConfirmed.ShouldBeFalse("Family membership must not attest parent-account authority.");
			using var matches = await ReadInquirySiblingMatchesAsync(id);
			matches.RootElement.GetProperty("Matches").GetArrayLength().ShouldBe(1);
			var parents = matches.RootElement.GetProperty("Matches")[0].GetProperty("Parents");
			parents.EnumerateArray().ShouldContain(parent => parent.GetProperty("Name").GetString() == "Own Mother");
			if (familyParents == null) familyParents = parents.GetRawText(); else parents.GetRawText().ShouldBe(familyParents);
		}
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>().CountAsync(item =>
			(item.InquiryId == first.Id || item.InquiryId == second.Id) && item.ActivityType == "SiblingFamilyConfirmed"))).ShouldBe(1);
	});

	private async Task<JsonDocument> ReadInquirySiblingMatchesAsync(Guid id)
	{
		var method = typeof(IAdmissionInquiryAppService).GetMethod("GetSiblingMatchesAsync").ShouldNotBeNull();
		try
		{
			var task = (Task)method.Invoke(GetRequiredService<IAdmissionInquiryAppService>(), [id])!;
			await task;
			return JsonDocument.Parse(JsonSerializer.Serialize(task.GetType().GetProperty("Result")!.GetValue(task)));
		}
		catch (TargetInvocationException error) when (error.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw;
		}
	}
}
