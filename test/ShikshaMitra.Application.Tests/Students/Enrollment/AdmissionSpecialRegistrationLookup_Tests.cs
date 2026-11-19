using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionTrack.Management)]
	[InlineData(AdmissionTrack.Staff)]
	public Task Special_registration_lookup_filters_before_count_and_paging(AdmissionTrack track) => RunCertificationScenarioAsync(async () =>
	{
		var source = await CreateSpecialRegistrationOfferingAsync(track, AdmissionEntryMode.Both);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(source.Id);
			stored.UpdateOperationalDetails("ZZZ-BOTH", true, null, null);
			await _offeringRepository.UpdateAsync(stored, true);
			for (var index = 0; index < 26; index++)
			{
				var standard = await _standardRepository.InsertAsync(
					await _standardManager.CreateAsync(source.StageId, $"Lookup class {index}", index + 20, null), true);
				var mode = index == 25 ? AdmissionEntryMode.DirectRegistration : AdmissionEntryMode.Inquiry;
				var row = new AdmissionOffering(Guid.NewGuid(), TestTenantId, source.AdmissionCycleId, null,
					source.BoardId, source.MediumId, source.StageId, standard.Id,
					index == 25 ? "ZZZZ-DIRECT" : $"AAA-{index:D2}", true, null, null, track: track, entryMode: mode);
				row.ConfigureReferences(source.WorkflowDefinitionId, mode == AdmissionEntryMode.Inquiry ? source.InquiryFormDefinitionId : null, source.ApplicationFormDefinitionId);
				await _offeringRepository.InsertAsync(row, true);
			}
		});
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var input = new AdmissionOfferingListInput
		{
			AdmissionCycleId = source.AdmissionCycleId, Track = track, IsActive = true,
			AvailabilityStatus = AdmissionOfferingAvailabilityStatus.Open, Sorting = "Code asc", MaxResultCount = 1
		};
		var generalList = await service.GetListAsync(input);
		generalList.TotalCount.ShouldBe(27);
		generalList.Items.Single().EntryMode.ShouldBe(AdmissionEntryMode.Inquiry);
		input.ForSpecialRegistration = true;
		var first = await service.GetListAsync(input);
		first.TotalCount.ShouldBe(2);
		first.Items.Single().Id.ShouldBe(source.Id);
		first.Items.Single().EntryMode.ShouldBe(AdmissionEntryMode.Both);
		first.Items.Single().BoardId.ShouldBe(source.BoardId);
		first.Items.Single().MediumId.ShouldBe(source.MediumId);
		first.Items.Single().StageId.ShouldBe(source.StageId);
		first.Items.Single().StandardId.ShouldBe(source.StandardId);
		first.Items.Single().StandardName.ShouldNotBeNullOrWhiteSpace();
		first.Items.Single().ConfigurationStatus.ShouldBe(AdmissionOfferingConfigurationStatus.Ready);
		input.SkipCount = 1;
		(await service.GetListAsync(input)).Items.Single().EntryMode.ShouldBe(AdmissionEntryMode.DirectRegistration);
		input.Search = "AAA";
		(await service.GetListAsync(input)).TotalCount.ShouldBe(0);
		input.Search = null;
		input.SkipCount = 0;
		(await service.GetListAsync(input)).TotalCount.ShouldBe(2);
		using (_currentTenant.Change(Guid.NewGuid()))
			(await service.GetListAsync(input)).TotalCount.ShouldBe(0);
		var userId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(userId, $"lookup-{userId:N}");
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.UserName, $"lookup-{userId:N}")], "Test"))))
			(await service.GetListAsync(input)).TotalCount.ShouldBe(0);
	});

	[Theory]
	[InlineData("2026-10-11T18:29:00Z", 0)] // Before midnight IST, opening tomorrow.
	[InlineData("2026-10-11T18:30:00Z", 1)] // Midnight IST, still yesterday UTC.
	[InlineData("2026-10-12T18:29:00Z", 1)] // Last minute of the closing day IST.
	[InlineData("2026-10-12T18:30:00Z", 0)] // Closed at the next institution day.
	public Task Special_registration_lookup_matches_institution_day_availability(string utcNow, int expectedCount) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.UpdateOperationalDetails(stored.Code, false,
				new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero),
				new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero));
			await _offeringRepository.UpdateAsync(stored, true);
		});
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(DateTime.Parse(utcNow, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal));
		var reader = GetRequiredService<AdmissionOfferingReadService>();
		reader.LazyServiceProvider = new AbpLazyServiceProvider(new OfferingLookupClockProvider(ServiceProvider, clock));
		var result = await WithUnitOfWorkAsync(() => reader.GetListAsync(new AdmissionOfferingListInput
		{
			AdmissionCycleId = offering.AdmissionCycleId, ForSpecialRegistration = true, IsActive = true,
			AvailabilityStatus = AdmissionOfferingAvailabilityStatus.Open
		}));
		result.TotalCount.ShouldBe(expectedCount);
		result.Items.ShouldAllBe(item => item.AvailabilityStatus == AdmissionOfferingAvailabilityStatus.Open);
	});

	private sealed class OfferingLookupClockProvider(IServiceProvider provider, IClock clock) : IServiceProvider
	{
		public object? GetService(Type serviceType) => serviceType == typeof(IClock) ? clock : provider.GetService(serviceType);
	}
}
