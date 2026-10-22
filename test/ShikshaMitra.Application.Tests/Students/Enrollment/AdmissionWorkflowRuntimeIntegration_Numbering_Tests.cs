using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private static SaveAdmissionApplicationNumberingDto NumberingInput(int version = 0, string prefix = "DDVS") => new()
	{ ExpectedVersion = version, Prefix = prefix, Separator = "/", YearFormat = AdmissionNumberYearFormat.AcademicYear,
		ResetPolicy = AdmissionNumberResetPolicy.AcademicYear, Digits = 5, StartingNumber = 1 };
	[Theory]
	[InlineData(AdmissionTrack.Rte)] [InlineData(AdmissionTrack.Management)] [InlineData(AdmissionTrack.Staff)]
	public Task Numbering_special_registration_uses_school_format_and_replays_without_renumbering(AdmissionTrack track) => RunCertificationScenarioAsync(async () =>
	{
		await GetRequiredService<IAdmissionApplicationNumberingAppService>().SaveAsync(NumberingInput());
		var offering = await CreateSpecialRegistrationOfferingAsync(track, AdmissionEntryMode.DirectRegistration);
		var input = SpecialRegistrationInput(offering.Id);
		var first = await _applicationAppService.StartDirectRegistrationAsync(input);
		first.ApplicationNumber.ShouldStartWith("DDVS/"); first.ApplicationNumber.ShouldEndWith("/00001");
		(await _applicationAppService.StartDirectRegistrationAsync(input)).ApplicationNumber.ShouldBe(first.ApplicationNumber);
	});

	[Fact]
	public Task Numbering_keeps_legacy_numbers_and_registration_replays_and_versions_future_formats() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var legacy = await StartFixtureApplicationAsync((await CreateApplicantAsync("Old number")).Id, offering.Id);
		legacy.ApplicationNumber.ShouldStartWith($"APP-{DateTime.UtcNow.Year}-");
		var settings = GetRequiredService<IAdmissionApplicationNumberingAppService>();
		(await settings.GetAsync()).IsConfigured.ShouldBeFalse();
		await settings.SaveAsync(NumberingInput());
		var applicant = await CreateApplicantAsync("New number");
		var first = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		var legacySequence = long.Parse(legacy.ApplicationNumber.Split('-').Last());
		first.ApplicationNumber.ShouldEndWith($"/{legacySequence + 1:D5}");
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(offering.AdmissionCycleId));
		var year = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(cycle.AcademicYearId));
		first.ApplicationNumber.ShouldBe($"DDVS/{year.StartDate.Year:D4}-{year.EndDate.Year % 100:D2}/{legacySequence + 1:D5}");
		(await _applicationAppService.StartAsync(applicant.Id, offering.Id)).ApplicationNumber.ShouldBe(first.ApplicationNumber);
		await settings.SaveAsync(NumberingInput(1, "SCHOOL"));
		var second = await StartFixtureApplicationAsync((await CreateApplicantAsync("Next number")).Id, offering.Id);
		second.ApplicationNumber.ShouldStartWith("SCHOOL/"); second.ApplicationNumber.ShouldEndWith($"/{legacySequence + 2:D5}");
		(await _applicationAppService.GetAsync(legacy.Id)).ApplicationNumber.ShouldBe(legacy.ApplicationNumber);
		(await _applicationAppService.GetAsync(first.Id)).ApplicationNumber.ShouldBe(first.ApplicationNumber);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionApplicationNumberingRule, Guid>>().GetListAsync())).Count.ShouldBe(2);
	});
	[Fact]
	public Task Numbering_rejects_stale_save_and_sequence_rewind_after_use() => RunCertificationScenarioAsync(async () =>
	{
		var settings = GetRequiredService<IAdmissionApplicationNumberingAppService>();
		await settings.SaveAsync(NumberingInput());
		(await Should.ThrowAsync<BusinessException>(() => settings.SaveAsync(NumberingInput()))).Code.ShouldBe(AdmissionNumberingErrorCodes.Changed);
		var offering = await CreateOptionalIntakeOfferingAsync();
		await StartFixtureApplicationAsync((await CreateApplicantAsync("Lock sequence")).Id, offering.Id);
		(await settings.GetAsync()).SequenceLocked.ShouldBeTrue();
		var changed = NumberingInput(1); changed.StartingNumber = 20;
		(await Should.ThrowAsync<BusinessException>(() => settings.SaveAsync(changed))).Code.ShouldBe(AdmissionNumberingErrorCodes.SequenceLocked);
		(await settings.GetAsync()).ExpectedVersion.ShouldBe(1);
	});
	[Fact]
	public Task Numbering_adopting_existing_format_continues_above_issued_numbers() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var first = await StartFixtureApplicationAsync((await CreateApplicantAsync("Legacy")).Id, offering.Id);
		await GetRequiredService<IAdmissionApplicationNumberingAppService>().SaveAsync(new());
		var second = await StartFixtureApplicationAsync((await CreateApplicantAsync("Adopted")).Id, offering.Id);
		long.Parse(second.ApplicationNumber.Split('-').Last()).ShouldBeGreaterThan(long.Parse(first.ApplicationNumber.Split('-').Last()));
		(await _applicationAppService.GetAsync(first.Id)).ApplicationNumber.ShouldBe(first.ApplicationNumber);
	});
	[Fact]
	public Task Numbering_preview_and_custom_format_continue_legacy_50_without_reserving_or_renumbering() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var old = await StartFixtureApplicationAsync((await CreateApplicantAsync("Legacy carry forward")).Id, offering.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var sequence = await GetRequiredService<IRepository<AdmissionReferenceSequence, Guid>>().FindAsync(x =>
				x.StoredTenantId == TestTenantId && x.Year == DateTime.UtcNow.Year && x.Kind == AdmissionReferenceKind.Application);
			while (sequence!.NextValue <= 50) sequence.TakeNext();
		});
		var service = GetRequiredService<IAdmissionApplicationNumberingAppService>();
		var previewInput = new PreviewAdmissionApplicationNumberingDto { Prefix = "DDVS", Digits = 4 };
		(await service.PreviewAsync(previewInput)).NextNumber.ShouldBe($"DDVS-{DateTime.UtcNow.Year}-0051");
		(await service.PreviewAsync(previewInput)).NextSequence.ShouldBe(51);
		(await service.GetAsync()).SequenceLocked.ShouldBeFalse();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionApplicationNumberCounter, Guid>>().GetCountAsync())).ShouldBe(0);
		await service.SaveAsync(previewInput);
		previewInput.ExpectedVersion = 1;
		(await service.PreviewAsync(previewInput)).NextSequence.ShouldBe(51);
		var next = await StartFixtureApplicationAsync((await CreateApplicantAsync("Custom carry forward")).Id, offering.Id);
		next.ApplicationNumber.ShouldBe($"DDVS-{DateTime.UtcNow.Year}-0051");
		(await service.PreviewAsync(previewInput)).NextSequence.ShouldBe(52);
		(await _applicationAppService.GetAsync(old.Id)).ApplicationNumber.ShouldBe(old.ApplicationNumber);
		previewInput.Prefix = "NEW";
		(await service.PreviewAsync(previewInput)).NextNumber.ShouldBe($"NEW-{DateTime.UtcNow.Year}-0052");
	});
	[Fact]
	public Task Numbering_academic_preview_uses_legacy_numbers_only_in_selected_year_and_requires_year() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var old = await StartFixtureApplicationAsync((await CreateApplicantAsync("Academic legacy")).Id, offering.Id);
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(offering.AdmissionCycleId));
		var service = GetRequiredService<IAdmissionApplicationNumberingAppService>();
		var input = new PreviewAdmissionApplicationNumberingDto { Prefix = "SCHOOL", YearFormat = AdmissionNumberYearFormat.AcademicYear, ResetPolicy = AdmissionNumberResetPolicy.AcademicYear };
		input.ExpectedVersion = 3;
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AdmissionNumberingErrorCodes.Changed);
		input.ExpectedVersion = 0;
		(await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AdmissionNumberingErrorCodes.Invalid);
		input.AcademicYearId = cycle.AcademicYearId;
		(await service.PreviewAsync(input)).NextSequence.ShouldBe(long.Parse(old.ApplicationNumber.Split('-').Last()) + 1);
		var nextYearId = Guid.NewGuid();
		var nextYearRule = new AdmissionApplicationNumberingRule(Guid.NewGuid(), TestTenantId, 1, "SCHOOL", "-", "", AdmissionNumberYearFormat.AcademicYear, AdmissionNumberResetPolicy.AcademicYear, 6, 1);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IAdmissionApplicationNumberingRepository>()
			.PeekNextAsync(nextYearRule, nextYearRule.ScopeKey(2027, nextYearId), "2027-28"))).ShouldBe(1);
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			input.AcademicYearId = null; input.YearFormat = AdmissionNumberYearFormat.None; input.ResetPolicy = AdmissionNumberResetPolicy.Continuous;
			(await service.PreviewAsync(input)).NextSequence.ShouldBe(1);
		}
	});
	[Fact]
	public Task Numbering_counters_are_tenant_isolated_and_reset_only_for_a_new_academic_scope() => RunCertificationScenarioAsync(async () =>
	{
		var repository = GetRequiredService<IAdmissionApplicationNumberingRepository>();
		var year = Guid.NewGuid(); var rule = new AdmissionApplicationNumberingRule(Guid.NewGuid(), TestTenantId, 1, "DDVS", "/", "", AdmissionNumberYearFormat.AcademicYear, AdmissionNumberResetPolicy.AcademicYear, 5, 1);
		(await WithUnitOfWorkAsync(() => repository.TakeNextAsync(rule, rule.ScopeKey(2026, year), "2026-27"))).ShouldBe(1);
		(await WithUnitOfWorkAsync(() => repository.TakeNextAsync(rule, rule.ScopeKey(2027, year), "2026-27"))).ShouldBe(2);
		(await WithUnitOfWorkAsync(() => repository.TakeNextAsync(rule, rule.ScopeKey(2027, Guid.NewGuid()), "2027-28"))).ShouldBe(1);
		using (_currentTenant.Change(Guid.NewGuid()))
			(await WithUnitOfWorkAsync(() => repository.TakeNextAsync(rule, rule.ScopeKey(2026, year), "2026-27"))).ShouldBe(1);
	});
	[Fact]
	public Task Numbering_legacy_carry_respects_calendar_reset_continuous_scope_and_configured_minimum() => RunCertificationScenarioAsync(async () =>
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var legacy = AdmissionReferenceSequence.Create(Guid.NewGuid(), TestTenantId, 2025, AdmissionReferenceKind.Application);
			for (var i = 0; i < 90; i++) legacy.TakeNext();
			await GetRequiredService<IRepository<AdmissionReferenceSequence, Guid>>().InsertAsync(legacy);
		});
		var repository = GetRequiredService<IAdmissionApplicationNumberingRepository>();
		var rule = new AdmissionApplicationNumberingRule(Guid.NewGuid(), TestTenantId, 1, "NEW", "-", "", AdmissionNumberYearFormat.CalendarYear, AdmissionNumberResetPolicy.CalendarYear, 4, 1);
		(await WithUnitOfWorkAsync(() => repository.PeekNextAsync(rule, rule.ScopeKey(2025, Guid.Empty), "2025"))).ShouldBe(91);
		(await WithUnitOfWorkAsync(() => repository.PeekNextAsync(rule, rule.ScopeKey(2026, Guid.Empty), "2026"))).ShouldBe(1);
		var continuous = new AdmissionApplicationNumberingRule(Guid.NewGuid(), TestTenantId, 1, "NEW", "-", "", AdmissionNumberYearFormat.None, AdmissionNumberResetPolicy.Continuous, 4, 1);
		(await WithUnitOfWorkAsync(() => repository.PeekNextAsync(continuous, "continuous", ""))).ShouldBe(91);
		var higher = new AdmissionApplicationNumberingRule(Guid.NewGuid(), TestTenantId, 1, "NEW", "-", "", AdmissionNumberYearFormat.None, AdmissionNumberResetPolicy.Continuous, 4, 100);
		(await WithUnitOfWorkAsync(() => repository.PeekNextAsync(higher, "continuous", ""))).ShouldBe(100);
	});
}
