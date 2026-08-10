using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadCaptureValidator(
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationMediumStage, Guid> mediumStageRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<AdmissionCrmRouteDefinition, Guid> routeDefinitionRepository,
	IRepository<AdmissionCrmInquiryType, Guid> inquiryTypeRepository,
	IUserScopeAccessService userScopeAccess,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	internal async Task EnsureAcademicPlacementAsync(Guid academicYearId, CreateAdmissionCrmLeadDto input)
	{
		if (!input.TargetStandardId.HasValue) return;

		var standard = await standardRepository.GetAsync(input.TargetStandardId.Value);
		var stage = await mediumStageRepository.GetAsync(standard.EducationMediumStageId);
		var medium = await mediumRepository.GetAsync(stage.EducationMediumId);
		var board = await boardRepository.GetAsync(medium.EducationBoardId);
		if (board.AcademicYearId != academicYearId ||
			(input.AcademicMediumStageId.HasValue && input.AcademicMediumStageId.Value != stage.Id) ||
			(input.AcademicMediumId.HasValue && input.AcademicMediumId.Value != medium.Id) ||
			(input.AcademicBoardId.HasValue && input.AcademicBoardId.Value != medium.EducationBoardId))
		{
			var exception = new BusinessException("AdmissionCrm:AcademicPlacementMismatch")
				.WithData("AcademicYearId", academicYearId)
				.WithData("ResolvedAcademicYearId", board.AcademicYearId)
				.WithData("TargetStandardId", standard.Id);
			if (input.AcademicMediumStageId.HasValue)
				exception.WithData("AcademicMediumStageId", input.AcademicMediumStageId.Value);
			if (input.AcademicMediumId.HasValue)
				exception.WithData("AcademicMediumId", input.AcademicMediumId.Value);
			if (input.AcademicBoardId.HasValue)
				exception.WithData("AcademicBoardId", input.AcademicBoardId.Value);
			throw exception;
		}

		input.DesiredClassName = standard.Name;
	}

	internal async Task<AdmissionCrmRouteDefinition?> ResolveIntendedRouteDefinitionAsync(
		Guid academicYearId,
		CreateAdmissionCrmLeadDto input,
		bool admissionYearSetupPublished)
	{
		if (!input.IntendedRouteDefinitionId.HasValue)
		{
			if (input.IntendedRouteType.HasValue)
			{
				if (admissionYearSetupPublished)
				{
					return null;
				}

				throw new BusinessException("AdmissionCrm:PublishedRouteDefinitionRequired")
					.WithData("IntendedRouteType", input.IntendedRouteType.Value)
					.WithData("AcademicYearId", academicYearId);
			}

			var now = Clock.Now.ToUniversalTime();
			var queryable = await routeDefinitionRepository.GetQueryableAsync();
			var defaults = await AsyncExecuter.ToListAsync(queryable.Where(definition =>
				definition.TenantId == CurrentTenant.Id &&
				definition.AcademicYearId == academicYearId &&
				definition.RouteType == AdmissionCrmRouteType.Fresh &&
				definition.Status == AdmissionCrmRouteDefinitionStatus.Published &&
				definition.EffectiveFrom <= now &&
				(!definition.EffectiveUntil.HasValue || now < definition.EffectiveUntil.Value)));
			if (defaults.Count == 1)
			{
				return defaults[0];
			}

			var hasConfiguredCatalog = await AsyncExecuter.AnyAsync(queryable.Where(definition =>
				definition.TenantId == CurrentTenant.Id &&
				definition.AcademicYearId == academicYearId));
			if (defaults.Count > 1 || hasConfiguredCatalog)
			{
				throw new BusinessException("AdmissionCrm:PublishedRouteDefinitionRequired")
					.WithData("IntendedRouteType", AdmissionCrmRouteType.Fresh)
					.WithData("AcademicYearId", academicYearId)
					.WithData("EffectiveDefinitionCount", defaults.Count);
			}

			return null;
		}

		var definition = await routeDefinitionRepository.FindAsync(input.IntendedRouteDefinitionId.Value);
		if (definition == null ||
			definition.TenantId != CurrentTenant.Id ||
			definition.AcademicYearId != academicYearId ||
			!definition.IsEffectiveAt(Clock.Now))
		{
			throw new BusinessException("AdmissionCrm:PublishedRouteDefinitionRequired")
				.WithData("RouteDefinitionId", input.IntendedRouteDefinitionId.Value)
				.WithData("AcademicYearId", academicYearId);
		}

		if (input.IntendedRouteType.HasValue && input.IntendedRouteType.Value != definition.RouteType)
		{
			throw new BusinessException("AdmissionCrm:IntendedRouteMismatch")
				.WithData("RouteDefinitionId", definition.Id)
				.WithData("IntendedRouteType", input.IntendedRouteType.Value)
				.WithData("ConfiguredRouteType", definition.RouteType);
		}

		return definition;
	}

	internal async Task<AdmissionCrmInquiryType?> ResolveInquiryTypeAsync(
		Guid academicYearId,
		Guid? inquiryTypeId)
	{
		var queryable = await inquiryTypeRepository.GetQueryableAsync();
		if (inquiryTypeId.HasValue)
		{
			var selected = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(type =>
				type.Id == inquiryTypeId.Value &&
				type.TenantId == CurrentTenant.Id &&
				type.AcademicYearId == academicYearId &&
				type.IsActive));
			if (selected is null)
			{
				throw new BusinessException("AdmissionCrm:InquiryTypeNotAvailable")
					.WithData("AcademicYearId", academicYearId)
					.WithData("InquiryTypeId", inquiryTypeId.Value);
			}

			return selected;
		}

		return await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(type =>
				type.TenantId == CurrentTenant.Id &&
				type.AcademicYearId == academicYearId &&
				type.IsActive)
			.OrderBy(type => type.DisplayOrder)
			.ThenBy(type => type.DisplayName));
	}
}

internal static class AdmissionCrmLeadCaptureRules
{
	internal static void EnsureStaffApplicantName(CreateAdmissionCrmLeadDto input)
	{
		if (string.IsNullOrWhiteSpace(input.FirstName))
		{
			throw new BusinessException("AdmissionCrm:FirstNameRequired")
				.WithData("Channel", input.Channel);
		}
	}

	internal static void EnsurePublishedAdmissionYearCaptureFields(CreateAdmissionCrmLeadDto input)
	{
		if (string.IsNullOrWhiteSpace(input.ParentGuardianName))
		{
			throw new BusinessException("AdmissionCrm:ParentGuardianNameRequired");
		}

		if (string.IsNullOrWhiteSpace(input.PrimaryContactNo))
		{
			throw new BusinessException("AdmissionCrm:GuardianMobileRequired");
		}
	}

	internal static void ValidateAdmissionYearScope(
		AdmissionCrmAdmissionYearSetup? setup,
		Guid? targetStandardId,
		AdmissionCrmRouteType? routeType)
	{
		if (setup is null)
		{
			return;
		}

		if (!targetStandardId.HasValue)
		{
			throw new BusinessException("AdmissionCrm:TargetStandardRequired")
				.WithData("AcademicYearId", setup.AcademicYearId)
				.WithData("SetupVersion", setup.Version);
		}

		var standardIds =
			JsonSerializer.Deserialize<HashSet<Guid>>(setup.TargetStandardIdsJson, AdmissionCrmLeadFormat.JsonOptions) ?? [];
		if (targetStandardId.HasValue && !standardIds.Contains(targetStandardId.Value))
		{
			throw new BusinessException("AdmissionCrm:StandardNotOpenForAdmission")
				.WithData("AcademicYearId", setup.AcademicYearId)
				.WithData("TargetStandardId", targetStandardId.Value)
				.WithData("SetupVersion", setup.Version);
		}

		var routeTypes =
			JsonSerializer.Deserialize<HashSet<AdmissionCrmRouteType>>(setup.RouteTypesJson, AdmissionCrmLeadFormat.JsonOptions) ?? [];
		var effectiveRoute = routeType ?? AdmissionCrmRouteType.Fresh;
		if (!routeTypes.Contains(effectiveRoute))
		{
			throw new BusinessException("AdmissionCrm:RouteNotOpenForAdmission")
				.WithData("AcademicYearId", setup.AcademicYearId)
				.WithData("RouteType", effectiveRoute)
				.WithData("SetupVersion", setup.Version);
		}
	}

	internal static string BuildLegacyAdmissionYearSnapshot(
		AdmissionCrmCycleConfiguration configuration)
	{
		return JsonSerializer.Serialize(
			new
			{
				schemaVersion = 0,
				source = "legacy-cycle-configuration",
				configuration.AcademicYearId,
				configuration.EntranceExamRequired,
				FinanceVerificationRequired = false,
				PlacementTiming = AdmissionCrmPlacementTiming.StandardBeforeActivation
			},
			AdmissionCrmLeadFormat.JsonOptions);
	}
}