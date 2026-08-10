using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmQuickInquiryOptionsWorkflow(
	IRepository<AdmissionCrmInquirySource, Guid> inquirySourceRepository,
	IRepository<AdmissionCrmInquiryType, Guid> inquiryTypeRepository,
	IRepository<AdmissionCrmAdmissionYearSetup, Guid> admissionYearRepository,
	IRepository<AdmissionCrmRouteDefinition, Guid> routeDefinitionRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmLeadAccess access,
	AdmissionCrmLeadConfigurationReader configurationReader) : ApplicationService, ITransientDependency
{
	[Authorize]
	public async Task<AdmissionCrmQuickInquiryOptionsDto> GetQuickInquiryOptionsAsync(Guid academicYearId)
	{
		await access.EnsureAcademicYearExistsAsync(academicYearId);
		var canAdd = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add);
		var canView = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View);
		if (!canAdd && !canView)
		{
			throw new AbpAuthorizationException(
				"Admission CRM Add or View permission is required to read quick inquiry options.");
		}

		await scopeAccess.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetQuickInquiryOptionsAsync));
		var configuration = await configurationReader.GetCycleConfigurationOrDefaultAsync(academicYearId);
		var admissionYearSetup = (await admissionYearRepository.GetListAsync(setup =>
				setup.TenantId == CurrentTenant.Id &&
				setup.AcademicYearId == academicYearId))
			.SingleOrDefault(setup =>
				setup.Status == AdmissionCrmAdmissionYearSetupStatus.Published);
		var channels = AdmissionCrmChannelSettingsSerializer
			.DeserializeSettings(configuration.ChannelSettingsJson)
			.Where(setting => setting.IsEnabled)
			.OrderBy(setting => setting.DisplayOrder)
			.ThenBy(setting => setting.Channel)
			.Select(setting => new AdmissionCrmQuickInquiryChannelOptionDto
			{
				Channel = setting.Channel,
				Label = GetInquiryChannelLabel(setting.Channel),
				DisplayOrder = setting.DisplayOrder
			})
			.ToList();
		var sourceQueryable = await inquirySourceRepository.GetQueryableAsync();
		var sources = (await AsyncExecuter.ToListAsync(sourceQueryable.Where(source =>
				source.TenantId == CurrentTenant.Id &&
				source.AcademicYearId == academicYearId &&
				source.IsActive)))
			.OrderBy(source => source.DisplayOrder)
			.ThenBy(source => source.DisplayName)
			.Select(source => new AdmissionCrmQuickInquirySourceOptionDto
			{
				InquirySourceId = source.Id,
				Name = source.DisplayName,
				AllowedChannels = source.GetAllowedChannels(),
				IsDefault = channels.Any(channel =>
					AdmissionCrmChannelSettingsSerializer
						.GetChannelSetting(configuration.ChannelSettingsJson, channel.Channel)
						.DefaultInquirySourceId == source.Id)
			})
			.ToList();
		var inquiryTypeQueryable = await inquiryTypeRepository.GetQueryableAsync();
		var inquiryTypes = (await AsyncExecuter.ToListAsync(inquiryTypeQueryable.Where(type =>
				type.TenantId == CurrentTenant.Id &&
				type.AcademicYearId == academicYearId &&
				type.IsActive)))
			.OrderBy(type => type.DisplayOrder)
			.ThenBy(type => type.DisplayName)
			.Select((type, index) => new AdmissionCrmQuickInquiryTypeOptionDto
			{
				InquiryTypeId = type.Id,
				Name = type.DisplayName,
				IsDefault = index == 0
			})
			.ToList();

		var now = Clock.Now.ToUniversalTime();
		var routeQueryable = await routeDefinitionRepository.GetQueryableAsync();
		var routeDefinitions = await AsyncExecuter.ToListAsync(routeQueryable.Where(route =>
			route.TenantId == CurrentTenant.Id &&
			route.AcademicYearId == academicYearId &&
			route.Status == AdmissionCrmRouteDefinitionStatus.Published &&
			route.EffectiveFrom <= now &&
			(!route.EffectiveUntil.HasValue || now < route.EffectiveUntil.Value)));
		var allowedRouteTypes = admissionYearSetup is null
			? routeDefinitions.Select(route => route.RouteType).Distinct().ToList()
			: JsonSerializer.Deserialize<List<AdmissionCrmRouteType>>(
				admissionYearSetup.RouteTypesJson,
				AdmissionCrmLeadFormat.JsonOptions) ?? [];
		if (allowedRouteTypes.Count == 0 && admissionYearSetup is null)
		{
			allowedRouteTypes.Add(AdmissionCrmRouteType.Fresh);
		}
		var routes = allowedRouteTypes
			.Distinct()
			.OrderBy(routeType => routeType)
			.Select(routeType =>
			{
				var definition = routeDefinitions
					.Where(route => route.RouteType == routeType)
					.OrderByDescending(route => route.Version)
					.ThenBy(route => route.Name)
					.FirstOrDefault();
				return new AdmissionCrmQuickInquiryRouteOptionDto
				{
					RouteDefinitionId = definition?.Id,
					Name = definition?.Name ?? GetAdmissionRouteLabel(routeType),
					RouteType = routeType,
					Version = definition?.Version
				};
			})
			.ToList();
		var defaultChannel = channels.FirstOrDefault(option =>
				option.Channel == AdmissionInquiryChannel.QuickCallEntry)
			?? channels.FirstOrDefault(option =>
				option.Channel == AdmissionInquiryChannel.ManualBackOffice)
			?? channels.FirstOrDefault();
		var defaultRoute = routes.FirstOrDefault(option =>
			option.RouteType == AdmissionCrmRouteType.Fresh);
		if (defaultChannel != null)
		{
			defaultChannel.IsDefault = true;
		}
		if (defaultRoute != null)
		{
			defaultRoute.IsDefault = true;
		}

		return new AdmissionCrmQuickInquiryOptionsDto
		{
			AcademicYearId = academicYearId,
			CycleName = configuration.CycleName,
			IsAdmissionCycleActive = admissionYearSetup is null
				? configuration.IsActive
				: now.Date >= admissionYearSetup.InquiryOpenDate &&
					now.Date <= admissionYearSetup.InquiryCloseDate,
			RegistrationFormRequiredBeforeExam = configuration.RegistrationFormRequiredBeforeExam,
			EntranceExamRequired =
				admissionYearSetup?.EntranceExamRequired ?? configuration.EntranceExamRequired,
			JourneyLocked = configuration.JourneyLocked,
			Channels = channels,
			Sources = sources,
			InquiryTypes = inquiryTypes,
			Routes = routes
		};
	}

	internal static string GetInquiryChannelLabel(AdmissionInquiryChannel channel) => channel switch
	{
		AdmissionInquiryChannel.ManualBackOffice => "Manual entry",
		AdmissionInquiryChannel.QuickCallEntry => "Quick inquiry",
		AdmissionInquiryChannel.WalkInReception => "Walk-in",
		AdmissionInquiryChannel.SchoolWebsiteForm => "School website",
		AdmissionInquiryChannel.WhatsAppInquiry => "WhatsApp",
		AdmissionInquiryChannel.StaffSharedLink => "Staff shared link",
		AdmissionInquiryChannel.QrCodeScan => "QR code",
		AdmissionInquiryChannel.CampaignLink => "Campaign link",
		_ => channel.ToString()
	};

	private static string GetAdmissionRouteLabel(AdmissionCrmRouteType routeType) => routeType switch
	{
		AdmissionCrmRouteType.Fresh => "Fresh admission",
		AdmissionCrmRouteType.SiblingPriority => "Sibling priority",
		AdmissionCrmRouteType.AlumniPreference => "Alumni preference",
		AdmissionCrmRouteType.StaffChild => "Staff child",
		AdmissionCrmRouteType.RteEws => "RTE / EWS",
		AdmissionCrmRouteType.Transfer => "Transfer",
		AdmissionCrmRouteType.Lateral => "Lateral entry",
		AdmissionCrmRouteType.MidYear => "Mid-year admission",
		AdmissionCrmRouteType.ReturningStudent => "Returning student",
		AdmissionCrmRouteType.Interstate => "Interstate transfer",
		AdmissionCrmRouteType.OverseasReturn => "Overseas return",
		AdmissionCrmRouteType.Direct => "Direct registration",
		AdmissionCrmRouteType.ManagementApproved => "Management approved",
		AdmissionCrmRouteType.SchoolDefined => "School-defined",
		_ => routeType.ToString()
	};
}