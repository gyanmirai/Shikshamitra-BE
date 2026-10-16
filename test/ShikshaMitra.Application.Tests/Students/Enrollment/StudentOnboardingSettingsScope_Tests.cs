using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_settings_reads_preserve_school_defaults_and_deny_unassigned_years() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
			var repository = GetRequiredService<IRepository<StudentOnboardingSettings, Guid>>();
			var school = await repository.FindAsync(item => item.AcademicYearId == null) ??
				await repository.InsertAsync(new StudentOnboardingSettings(Guid.NewGuid(), TestTenantId, null, 1, "{}"), true);
			var settings = await repository.InsertAsync(new StudentOnboardingSettings(Guid.NewGuid(), TestTenantId, year, 1, "{}"), true);
			var scopedUserId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(scopedUserId, $"onboarding-assigned-{scopedUserId:N}");
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: scopedUserId,
				academicYearId: year, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
				educationMediumStageId: offering.StageId, standardId: offering.StandardId), true);
			var userId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(userId, $"onboarding-no-scope-{userId:N}");
			using var principal = _principalAccessor.Change(ProfilePrincipal(userId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			(await service.GetSettingsAsync(null)).Select(item => item.Id).ShouldContain(school.Id);
			(await service.GetSettingsAsync(null)).Select(item => item.Id).ShouldNotContain(settings.Id);
			(await Should.ThrowAsync<BusinessException>(() => service.GetSettingsAsync(year))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			(await Should.ThrowAsync<BusinessException>(() => service.GetPoliciesAsync(year))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			(await Should.ThrowAsync<BusinessException>(() => service.GetIdentifierSeriesAsync(year))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			(await Should.ThrowAsync<BusinessException>(() => service.GetAvailableOfferingsAsync(year))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			(await service.GetAvailableOfferingsAsync(null)).ShouldBeEmpty();
			using (_principalAccessor.Change(ProfilePrincipal(scopedUserId)))
			{
				var inherited = (await service.GetSettingsAsync(year)).Select(item => item.Id).ToArray();
				inherited.ShouldContain(school.Id);
				inherited.ShouldContain(settings.Id);
				(await Should.ThrowAsync<BusinessException>(() => service.GetSettingsAsync(Guid.NewGuid()))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			}
		});

	[Theory]
	[InlineData("division")]
	[InlineData("class")]
	[InlineData("year")]
	[InlineData("tenant")]
	public Task Onboarding_policy_and_series_scope_filters_reads_and_protects_shared_writes(string level) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var other = await CreateOnboardingSettingsSiblingOfferingAsync(offering);
			var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var policies = new[]
			{
				await service.CreatePolicyAsync(OnboardingScopePolicy(year)),
				await service.CreatePolicyAsync(OnboardingScopePolicy(year, offering)),
				await service.CreatePolicyAsync(OnboardingScopePolicy(year, other))
			};
			var series = new[]
			{
				await service.CreateIdentifierSeriesAsync(OnboardingScopeSeries(year)),
				await service.CreateIdentifierSeriesAsync(OnboardingScopeSeries(year, offering.StandardId)),
				await service.CreateIdentifierSeriesAsync(OnboardingScopeSeries(year, other.StandardId))
			};
			var userId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(userId, $"onboarding-settings-{userId:N}");
			await WithUnitOfWorkAsync(async () =>
			{
				var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
					await GetRequiredService<DivisionManager>().CreateAsync(offering.StandardId, "Settings scope", 30), true);
				var narrow = level is "division" or "class";
				await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: userId,
					academicYearId: level == "tenant" ? null : year, educationBoardId: narrow ? offering.BoardId : null,
					educationMediumId: narrow ? offering.MediumId : null, educationMediumStageId: narrow ? offering.StageId : null,
					standardId: narrow ? offering.StandardId : null, divisionId: level == "division" ? division.Id : null), true);
			});
			using var principal = _principalAccessor.Change(ProfilePrincipal(userId));
			var readablePolicies = (await service.GetPoliciesAsync(year)).Select(item => item.Id).ToArray();
			var readableSeries = (await service.GetIdentifierSeriesAsync(year)).Select(item => item.Id).ToArray();
			var yearWide = level is "year" or "tenant";
			var availableOfferings = (await service.GetAvailableOfferingsAsync(year)).Select(item => item.Id).ToArray();
			availableOfferings.ShouldContain(offering.Id);
			availableOfferings.Contains(other.Id).ShouldBe(yearWide);
			for (var index = 0; index < policies.Length; index++)
			{
				readablePolicies.Contains(policies[index].Id).ShouldBe(index < 2 || yearWide);
				readableSeries.Contains(series[index].Id).ShouldBe(index < 2 || yearWide);
				var policy = policies[index];
				var update = OnboardingScopePolicy(year, index == 1 ? offering : index == 2 ? other : null);
				update.Name = "Scoped update";
				update.ConcurrencyStamp = policy.ConcurrencyStamp;
				if (yearWide || level == "class" && index == 1)
				{
					(await service.UpdatePolicyAsync(policy.Id, update)).Name.ShouldBe("Scoped update");
					(await service.DeactivateIdentifierSeriesAsync(series[index].Id, series[index].ConcurrencyStamp)).IsActive.ShouldBeFalse();
				}
				else
				{
					(await Should.ThrowAsync<BusinessException>(() => service.UpdatePolicyAsync(policy.Id, update))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
					(await Should.ThrowAsync<BusinessException>(() => service.DeactivateIdentifierSeriesAsync(series[index].Id, series[index].ConcurrencyStamp)))
						.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
				}
			}
			if (level == "division")
			{
				(await Should.ThrowAsync<BusinessException>(() => service.CreatePolicyAsync(OnboardingScopePolicy(year, offering))))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
				(await Should.ThrowAsync<BusinessException>(() => service.CreateIdentifierSeriesAsync(OnboardingScopeSeries(year, offering.StandardId))))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			}
		});

	[Fact]
	public Task Onboarding_configuration_creation_requires_the_target_class_and_matching_year() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var other = await CreateOnboardingSettingsSiblingOfferingAsync(offering);
			var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
			var otherYear = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
				new AcademicYear(Guid.NewGuid(), TestTenantId, "Other settings year", new DateTime(2035, 4, 1), new DateTime(2036, 3, 31)), true);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			// Even a school administrator cannot attach a class from a different academic year.
			(await Should.ThrowAsync<BusinessException>(() => service.CreateIdentifierSeriesAsync(OnboardingScopeSeries(otherYear.Id, offering.StandardId))))
				.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			var userId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(userId, $"onboarding-class-{userId:N}");
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: userId,
				academicYearId: year, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
				educationMediumStageId: offering.StageId, standardId: offering.StandardId), true);
			using var principal = _principalAccessor.Change(ProfilePrincipal(userId));
			(await service.CreatePolicyAsync(OnboardingScopePolicy(year, offering))).StandardId.ShouldBe(offering.StandardId);
			(await service.CreateIdentifierSeriesAsync(OnboardingScopeSeries(year, offering.StandardId))).StandardId.ShouldBe(offering.StandardId);
			foreach (var target in new AdmissionOffering?[] { null, other })
			{
				(await Should.ThrowAsync<BusinessException>(() => service.CreatePolicyAsync(OnboardingScopePolicy(year, target))))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
				(await Should.ThrowAsync<BusinessException>(() => service.CreateIdentifierSeriesAsync(OnboardingScopeSeries(year, target?.StandardId))))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			}
			(await service.GetSettingsAsync(year)).ShouldNotBeNull();
			(await Should.ThrowAsync<BusinessException>(() => service.GetSettingsAsync(otherYear.Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		});

	private Task<AdmissionOffering> CreateOnboardingSettingsSiblingOfferingAsync(AdmissionOffering source) => WithUnitOfWorkAsync(async () =>
	{
		var standard = await _standardRepository.InsertAsync(await _standardManager.CreateAsync(source.StageId,
			$"Settings {Guid.NewGuid():N}"[..28], 11, null), true);
		var offering = new AdmissionOffering(Guid.NewGuid(), TestTenantId, source.AdmissionCycleId, null,
			source.BoardId, source.MediumId, source.StageId, standard.Id, "OTHER_SETTINGS", true, null, null);
		offering.ConfigureReferences(source.WorkflowDefinitionId, source.InquiryFormDefinitionId, source.ApplicationFormDefinitionId);
		return await _offeringRepository.InsertAsync(offering, true);
	});

	private static UpsertStudentOnboardingPolicyDto OnboardingScopePolicy(Guid year, AdmissionOffering? offering = null) => new()
	{
		AcademicYearId = year, OfferingId = offering?.Id, StandardId = offering?.StandardId, Name = "Scope policy"
	};

	private static CreateStudentIdentifierSeriesDto OnboardingScopeSeries(Guid year, Guid? standardId = null) => new()
	{
		AcademicYearId = year, StandardId = standardId, Code = $"SCOPE-{Guid.NewGuid():N}", Name = "Scope series",
		EffectiveFrom = DateTime.UtcNow.AddDays(-1)
	};
}
