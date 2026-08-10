using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionYearValidator(
	IRepository<AdmissionCrmInquirySource, Guid> inquirySourceRepository,
	IRepository<AdmissionCrmInquiryType, Guid> inquiryTypeRepository,
	AdmissionCrmAdmissionYearReferenceReader referenceReader,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmStaffEligibilityService staffEligibility) : ApplicationService, ITransientDependency
{
	private static readonly HashSet<string> StepKeys = ["inquiry", "exam", "registration", "finance", "placement", "activation"];

	internal async Task ValidateAsync(
		Guid academicYearId,
		UpdateAdmissionCrmAdmissionYearSetupDto input,
		bool requirePublishReady = false)
	{
		if (!Enum.IsDefined(input.PlacementTiming))
		{
			throw new BusinessException("AdmissionCrm:InvalidPlacementTiming")
				.WithData("PlacementTiming", input.PlacementTiming);
		}

		if (input.TargetStandardIds.Count != input.TargetStandardIds.Distinct().Count())
		{
			throw new BusinessException("AdmissionCrm:DuplicateAdmissionYearStandard");
		}

		if (input.RouteTypes.Count != input.RouteTypes.Distinct().Count() ||
			input.RouteTypes.Any(route => !Enum.IsDefined(route)))
		{
			throw new BusinessException("AdmissionCrm:InvalidAdmissionYearRoutes");
		}

		if (requirePublishReady && input.TargetStandardIds.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearStandardsRequired");
		}

		if (requirePublishReady && input.RouteTypes.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearRoutesRequired");
		}

		if (requirePublishReady)
		{
			var sourceQuery = await inquirySourceRepository.GetQueryableAsync();
			var hasActiveSource = await AsyncExecuter.AnyAsync(sourceQuery.Where(source =>
				source.TenantId == CurrentTenant.Id &&
				source.AcademicYearId == academicYearId &&
				source.IsActive));
			if (!hasActiveSource)
			{
				throw new BusinessException("AdmissionCrm:ActiveInquirySourceRequired");
			}

			var typeQuery = await inquiryTypeRepository.GetQueryableAsync();
			var hasActiveType = await AsyncExecuter.AnyAsync(typeQuery.Where(type =>
				type.TenantId == CurrentTenant.Id &&
				type.AcademicYearId == academicYearId &&
				type.IsActive));
			if (!hasActiveType)
			{
				throw new BusinessException("AdmissionCrm:ActiveInquiryTypeRequired");
			}
		}

		var standardRows = await referenceReader.GetStandardRowsAsync(academicYearId);
		var academicYearStandardIds = standardRows
			.Select(row => row.StandardId)
			.ToHashSet();
		var invalidStandardIds = input.TargetStandardIds
			.Where(standardId => !academicYearStandardIds.Contains(standardId))
			.ToList();
		if (invalidStandardIds.Count > 0)
		{
			throw new BusinessException("AdmissionCrm:StandardNotInAdmissionYear")
				.WithData("AcademicYearId", academicYearId)
				.WithData("InvalidStandardCount", invalidStandardIds.Count);
		}

		foreach (var standardId in input.TargetStandardIds)
		{
			await scopeAccess.EnsureCanAccessStandardAsync(
				standardId,
				nameof(ValidateAsync));
		}

		if (input.StepOwners.Keys.Any(key => !StepKeys.Contains(key)))
		{
			throw new BusinessException("AdmissionCrm:InvalidAdmissionYearStepOwner");
		}

		var ownerIds = input.StepOwners.Values
			.Append(input.DefaultOwnerUserId)
			.Where(ownerId => ownerId.HasValue)
			.Select(ownerId => ownerId!.Value)
			.Distinct();
		foreach (var ownerId in ownerIds)
		{
			if (!await staffEligibility.IsOwnerEligibleAsync(ownerId, academicYearId))
			{
				throw new BusinessException("AdmissionCrm:IneligibleAdmissionYearOwner")
					.WithData("OwnerUserId", ownerId)
					.WithData("AcademicYearId", academicYearId);
			}
		}
	}
}