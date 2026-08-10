using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicCaptureConfiguration(
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	IRepository<AdmissionCrmAdmissionYearSetup, Guid> admissionYearRepository,
	IRepository<AdmissionCrmInquirySource, Guid> inquirySourceRepository,
	IRepository<AdmissionCrmInquiryType, Guid> inquiryTypeRepository) : ApplicationService, ITransientDependency
{
	public async Task EnsurePublicFormChannelEnabledAsync(AdmissionInquiryFormTemplate template)
	{
		var configuration = await GetCycleConfigurationOrDefaultAsync(template.AcademicYearId);
		var channelSetting = AdmissionCrmChannelSettingsSerializer.GetChannelSetting(
			configuration.ChannelSettingsJson, template.Channel);
		if (!channelSetting.IsEnabled)
		{
			throw new BusinessException("AdmissionCrm:PublicFormNotFound").WithData("TemplateId", template.Id);
		}

		var setup = await GetAdmissionYearSetupForCaptureAsync(template.AcademicYearId);
		await ResolvePublicInquirySourceAsync(
			template.AcademicYearId, template.Channel, channelSetting.DefaultInquirySourceId, setup is not null);
		await ResolveDefaultInquiryTypeAsync(template.AcademicYearId, setup is not null);
	}

	public async Task<AdmissionCrmCycleConfiguration> GetCycleConfigurationOrDefaultAsync(Guid academicYearId)
	{
		var queryable = await cycleRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(configuration =>
			configuration.TenantId == CurrentTenant.Id && configuration.AcademicYearId == academicYearId));
		return existing ?? new AdmissionCrmCycleConfiguration(
			GuidGenerator.Create(), CurrentTenant.Id, academicYearId, "Default Admission Year");
	}

	public async Task<AdmissionCrmAdmissionYearSetup?> GetAdmissionYearSetupForCaptureAsync(Guid academicYearId)
	{
		var setups = await admissionYearRepository.GetListAsync(setup =>
			setup.TenantId == CurrentTenant.Id && setup.AcademicYearId == academicYearId);
		if (setups.Count == 0)
		{
			return null;
		}

		var published = setups.SingleOrDefault(setup => setup.Status == AdmissionCrmAdmissionYearSetupStatus.Published);
		if (published is null)
		{
			var latest = setups.OrderByDescending(setup => setup.Version).First();
			throw new BusinessException("AdmissionCrm:AdmissionYearNotOpen")
				.WithData("AcademicYearId", academicYearId)
				.WithData("Status", latest.Status)
				.WithData("Version", latest.Version);
		}

		published.EnsureInquiryCaptureAllowed(Clock.Now);
		if (string.IsNullOrWhiteSpace(published.PublishedSnapshotJson))
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSnapshotMissing")
				.WithData("SetupId", published.Id).WithData("Version", published.Version);
		}

		return published;
	}

	public async Task<AdmissionCrmInquirySource?> ResolvePublicInquirySourceAsync(
		Guid academicYearId,
		AdmissionInquiryChannel channel,
		Guid? inquirySourceId,
		bool required)
	{
		if (!inquirySourceId.HasValue)
		{
			if (required)
			{
				throw new BusinessException("AdmissionCrm:InquirySourceRequired")
					.WithData("AcademicYearId", academicYearId).WithData("Channel", channel);
			}

			return null;
		}

		var source = await inquirySourceRepository.FindAsync(inquirySourceId.Value);
		if (source is null || source.TenantId != CurrentTenant.Id || source.AcademicYearId != academicYearId ||
			!source.IsActive || !source.Supports(channel))
		{
			throw new BusinessException("AdmissionCrm:InquirySourceUnavailable")
				.WithData("InquirySourceId", inquirySourceId.Value)
				.WithData("AcademicYearId", academicYearId)
				.WithData("Channel", channel);
		}

		return source;
	}

	public async Task<AdmissionCrmInquiryType?> ResolveDefaultInquiryTypeAsync(Guid academicYearId, bool required)
	{
		var queryable = await inquiryTypeRepository.GetQueryableAsync();
		var inquiryType = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(type => type.TenantId == CurrentTenant.Id && type.AcademicYearId == academicYearId && type.IsActive)
			.OrderBy(type => type.DisplayOrder).ThenBy(type => type.DisplayName));
		if (inquiryType is null && required)
		{
			throw new BusinessException("AdmissionCrm:InquiryTypeRequired").WithData("AcademicYearId", academicYearId);
		}

		return inquiryType;
	}

	public static AdmissionCrmRouteType? ResolveDefaultPublicRoute(AdmissionCrmAdmissionYearSetup? setup)
	{
		if (setup is null)
		{
			return null;
		}

		var routeTypes = JsonSerializer.Deserialize<HashSet<AdmissionCrmRouteType>>(
			setup.RouteTypesJson, AdmissionCrmPublicFormat.JsonOptions) ?? [];
		if (routeTypes.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearRoutesRequired")
				.WithData("AcademicYearId", setup.AcademicYearId).WithData("SetupVersion", setup.Version);
		}

		return routeTypes.Contains(AdmissionCrmRouteType.Fresh)
			? AdmissionCrmRouteType.Fresh
			: routeTypes.OrderBy(route => (int) route).First();
	}

	public static void ValidateAdmissionYearScope(
		AdmissionCrmAdmissionYearSetup setup,
		Guid? targetStandardId,
		AdmissionCrmRouteType? routeType)
	{
		if (!targetStandardId.HasValue)
		{
			throw new BusinessException("AdmissionCrm:TargetStandardRequired")
				.WithData("AcademicYearId", setup.AcademicYearId).WithData("SetupVersion", setup.Version);
		}

		var standardIds = JsonSerializer.Deserialize<HashSet<Guid>>(
			setup.TargetStandardIdsJson, AdmissionCrmPublicFormat.JsonOptions) ?? [];
		if (!standardIds.Contains(targetStandardId.Value))
		{
			throw new BusinessException("AdmissionCrm:StandardNotOpenForAdmission")
				.WithData("AcademicYearId", setup.AcademicYearId)
				.WithData("TargetStandardId", targetStandardId.Value)
				.WithData("SetupVersion", setup.Version);
		}

		var routeTypes = JsonSerializer.Deserialize<HashSet<AdmissionCrmRouteType>>(
			setup.RouteTypesJson, AdmissionCrmPublicFormat.JsonOptions) ?? [];
		var effectiveRoute = routeType ?? AdmissionCrmRouteType.Fresh;
		if (!routeTypes.Contains(effectiveRoute))
		{
			throw new BusinessException("AdmissionCrm:RouteNotOpenForAdmission")
				.WithData("AcademicYearId", setup.AcademicYearId)
				.WithData("RouteType", effectiveRoute)
				.WithData("SetupVersion", setup.Version);
		}
	}

	public static string BuildLegacyAdmissionYearSnapshot(AdmissionCrmCycleConfiguration configuration) =>
		JsonSerializer.Serialize(
			new
			{
				schemaVersion = 0,
				source = "legacy-cycle-configuration",
				configuration.AcademicYearId,
				configuration.EntranceExamRequired,
				FinanceVerificationRequired = false,
				PlacementTiming = AdmissionCrmPlacementTiming.StandardBeforeActivation
			},
			AdmissionCrmPublicFormat.JsonOptions);
}
