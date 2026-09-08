using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeStructureAppService(
    IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
    IStandardRepository standardRepository,
    IAcademicYearRepository academicYearRepository,
	IRepository<PaymentTerm, Guid> paymentTermRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<ConcessionPolicy, Guid> concessionPolicyRepository,
	IRepository<RefundPolicy, Guid> refundPolicyRepository,
	IRepository<CollectionPolicy, Guid> collectionPolicyRepository,
	IRepository<ProrationPolicy, Guid> prorationPolicyRepository,
	IRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository
) : ApplicationService, IFeeStructureAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.ManageAttachment)]
    public async Task<FeeStructureCreateResponse> CreateAsync(Guid feeStructureMetadataId, StandardFeeStructureSaveRequest input)
    {
		// validation to ensure no duplicate fee structure name exists for the same standard
		var existingStructure = await feeStructureRepository.AnyAsync(x => x.StandardId == input.StandardId && x.Name == input.StandardFeeStructures.Name);

		if (existingStructure)
		{
			throw new BusinessException("A fee structure with the same name already exists for this standard. Please choose a different name.");
		}

		var feeStructure = new FeeStructure(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            feeStructureMetadataId,
            input.StandardId,
            input.StandardFeeStructures?.Name ?? string.Empty
        );

		var collectionAttachment = await ResolveCollectionPolicyAttachmentAsync(
			feeStructureMetadataId,
			input.StandardFeeStructures?.CollectionPolicyId,
			input.StandardFeeStructures?.CollectionPolicyVersionId,
			input.StandardFeeStructures?.FeeHeads.Select(x => x.FeeHeadId).ToList() ?? []);
		feeStructure.AttachCollectionPolicy(collectionAttachment.PolicyId, collectionAttachment.VersionId);
		var concessionAttachment = await ResolveConcessionPolicyAsync(
			feeStructureMetadataId,
			input.StandardFeeStructures?.ConcessionPolicyId,
			input.StandardFeeStructures?.ConcessionFeeHeadScope ?? ConcessionFeeHeadScope.EntireFeeStructure,
			input.StandardFeeStructures?.ConcessionApplicableFeeHeadIds ?? [],
			input.StandardFeeStructures?.FeeHeads.Select(x => x.FeeHeadId).ToList() ?? []);
		feeStructure.AttachConcessionPolicy(
			concessionAttachment.PolicyId, concessionAttachment.VersionId,
			concessionAttachment.FeeHeadScope, concessionAttachment.ApplicableFeeHeadIds);
		var refundAttachment = await ResolveRefundPolicyAsync(
			feeStructureMetadataId,
			input.StandardFeeStructures?.RefundPolicyId,
			input.StandardFeeStructures?.RefundPolicyVersionId,
			input.StandardFeeStructures?.FeeHeads.Select(x => x.FeeHeadId).ToList() ?? []);
		if (refundAttachment.PolicyId.HasValue)
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.ManageAttachment);
		feeStructure.AttachRefundPolicy(refundAttachment.PolicyId, refundAttachment.VersionId);
		var prorationAttachment = await ResolveProrationPolicyAsync(
			feeStructureMetadataId,
			input.StandardFeeStructures?.ProrationPolicyId,
			input.StandardFeeStructures?.ProrationPolicyVersionId,
			input.StandardFeeStructures?.FeeHeads ?? []);
		if (prorationAttachment.PolicyId.HasValue)
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.ManageAttachment);
		feeStructure.AttachProrationPolicy(prorationAttachment.PolicyId, prorationAttachment.VersionId);

        if (input.StandardFeeStructures != null)
        {
            var structureRequest = input.StandardFeeStructures;
            
            foreach (var feeHead in structureRequest.FeeHeads)
            {
                var detail = new FeeStructureDetail(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    feeStructure.Id,
                    feeHead.FeeHeadId,
                    feeHead.Amount,
                    feeHead.IsOneTime,
                    feeHead.IsOptional,
					feeHead.PayableMonth,
					feeHead.PayableYear
				);

                // Attach Payment Term if provided
                if (feeHead.PaymentTermId.HasValue && feeHead.PaymentTermVersionId.HasValue)
                {
					await EnsureAssignablePaymentTermAsync(
						feeHead.PaymentTermId.Value,
						feeHead.PaymentTermVersionId.Value);
                    if (feeHead.InstallmentOverrides != null && feeHead.InstallmentOverrides.Count > 0)
                    {
                        var overrides = feeHead.InstallmentOverrides.Select(o => new FeeStructureDetailScheduleOverride(
                            GuidGenerator.Create(),
                            CurrentTenant.Id,
                            detail.Id,
                            o.SourcePaymentTermInstallmentId,
                            o.Sequence,
                            o.Name,
                            o.CoverageStartDate,
                            o.CoverageEndDate,
                            o.DemandDate,
                            o.DueDate,
                            o.AllocationPercentage
                        )).ToList();

                        detail.AttachPaymentTermWithOverrides(
                            feeHead.PaymentTermId.Value,
                            feeHead.PaymentTermVersionId.Value,
                            overrides);
                    }
                    else
                    {
                        detail.AttachPaymentTerm(
                            feeHead.PaymentTermId.Value,
                            feeHead.PaymentTermVersionId.Value);
                    }
                }

                feeStructure.AddDetail(detail);
            }
        }

        await feeStructureRepository.InsertAsync(feeStructure);

        return new FeeStructureCreateResponse
        {
            Id = feeStructure.Id,
			Name = feeStructure.Name,
			CollectionPolicyId = feeStructure.CollectionPolicyId,
			CollectionPolicyVersionId = feeStructure.CollectionPolicyVersionId,
			ConcessionPolicyId = feeStructure.ConcessionPolicyId,
			ConcessionPolicyVersionId = feeStructure.ConcessionPolicyVersionId,
			RefundPolicyId = feeStructure.RefundPolicyId,
			RefundPolicyVersionId = feeStructure.RefundPolicyVersionId,
			ProrationPolicyId = feeStructure.ProrationPolicyId,
			ProrationPolicyVersionId = feeStructure.ProrationPolicyVersionId
        };
    }

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Edit)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.ManageAttachment)]
    public async Task<FeeStructureCreateResponse> UpdateAsync(Guid id, StandardFeeStructureRequest input)
    {
        var feeStructure = await feeStructureRepository.GetAsync(id);

        feeStructure.EnsureNotLocked();
        feeStructure.ClearDetails();

        if (input.FeeHeads != null)
        {
            foreach (var feeHead in input.FeeHeads)
            {
                var detail = new FeeStructureDetail(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    feeStructure.Id,
                    feeHead.FeeHeadId,
                    feeHead.Amount,
                    feeHead.IsOneTime,
                    feeHead.IsOptional,
					feeHead.PayableMonth,
					feeHead.PayableYear
				);

                // Attach Payment Term if provided
                if (feeHead.PaymentTermId.HasValue && feeHead.PaymentTermVersionId.HasValue)
                {
					await EnsureAssignablePaymentTermAsync(
						feeHead.PaymentTermId.Value,
						feeHead.PaymentTermVersionId.Value);
                    if (feeHead.InstallmentOverrides != null && feeHead.InstallmentOverrides.Count > 0)
                    {
                        var overrides = feeHead.InstallmentOverrides.Select(o => new FeeStructureDetailScheduleOverride(
                            GuidGenerator.Create(),
                            CurrentTenant.Id,
                            detail.Id,
                            o.SourcePaymentTermInstallmentId,
                            o.Sequence,
                            o.Name,
                            o.CoverageStartDate,
                            o.CoverageEndDate,
                            o.DemandDate,
                            o.DueDate,
                            o.AllocationPercentage
                        )).ToList();

                        detail.AttachPaymentTermWithOverrides(
                            feeHead.PaymentTermId.Value,
                            feeHead.PaymentTermVersionId.Value,
                            overrides);
                    }
                    else
                    {
                        detail.AttachPaymentTerm(
                            feeHead.PaymentTermId.Value,
                            feeHead.PaymentTermVersionId.Value);
                    }
                }

                feeStructure.AddDetail(detail);
            }
        }

		feeStructure.ChangeName(input.Name);
		var collectionAttachment = await ResolveCollectionPolicyAttachmentAsync(
			feeStructure.FeeStructureMetadataId,
			input.CollectionPolicyId,
			input.CollectionPolicyVersionId,
			(input.FeeHeads ?? []).Select(x => x.FeeHeadId).ToList());
		feeStructure.AttachCollectionPolicy(collectionAttachment.PolicyId, collectionAttachment.VersionId);
		var concessionAttachment = await ResolveConcessionPolicyAsync(
			feeStructure.FeeStructureMetadataId,
			input.ConcessionPolicyId,
			input.ConcessionFeeHeadScope,
			input.ConcessionApplicableFeeHeadIds,
			input.FeeHeads.Select(x => x.FeeHeadId).ToList());
		feeStructure.AttachConcessionPolicy(
			concessionAttachment.PolicyId, concessionAttachment.VersionId,
			concessionAttachment.FeeHeadScope, concessionAttachment.ApplicableFeeHeadIds);
		var refundAttachment = await ResolveRefundPolicyAsync(
			feeStructure.FeeStructureMetadataId,
			input.RefundPolicyId,
			input.RefundPolicyVersionId,
			input.FeeHeads.Select(x => x.FeeHeadId).ToList());
		if (feeStructure.RefundPolicyId != refundAttachment.PolicyId ||
			feeStructure.RefundPolicyVersionId != refundAttachment.VersionId)
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.ManageAttachment);
		feeStructure.AttachRefundPolicy(refundAttachment.PolicyId, refundAttachment.VersionId);
		var prorationAttachment = await ResolveProrationPolicyAsync(
			feeStructure.FeeStructureMetadataId,
			input.ProrationPolicyId,
			input.ProrationPolicyVersionId,
			input.FeeHeads);
		if (feeStructure.ProrationPolicyId != prorationAttachment.PolicyId ||
			feeStructure.ProrationPolicyVersionId != prorationAttachment.VersionId)
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.ManageAttachment);
		feeStructure.AttachProrationPolicy(prorationAttachment.PolicyId, prorationAttachment.VersionId);

		// validation to ensure no duplicate fee structure name exists for the same standard
		var existingStructure = await feeStructureRepository.AnyAsync(x => x.StandardId == feeStructure.StandardId && x.Name == feeStructure.Name && x.Id != feeStructure.Id);

		if (existingStructure)
		{
			throw new BusinessException("A fee structure with the same name already exists for this standard. Please choose a different name.");
		}

		await feeStructureRepository.UpdateAsync(feeStructure);
		return new FeeStructureCreateResponse
		{
			Id = feeStructure.Id,
			Name = feeStructure.Name,
			CollectionPolicyId = feeStructure.CollectionPolicyId,
			CollectionPolicyVersionId = feeStructure.CollectionPolicyVersionId,
			ConcessionPolicyId = feeStructure.ConcessionPolicyId,
			ConcessionPolicyVersionId = feeStructure.ConcessionPolicyVersionId,
			RefundPolicyId = feeStructure.RefundPolicyId,
			RefundPolicyVersionId = feeStructure.RefundPolicyVersionId,
			ProrationPolicyId = feeStructure.ProrationPolicyId,
			ProrationPolicyVersionId = feeStructure.ProrationPolicyVersionId
		};
    }

    public async Task DeleteAsync(Guid id)
    {
        var feeStructure = await feeStructureRepository.GetAsync(id);

        feeStructure.EnsureNotLocked();

        await feeStructureRepository.DeleteAsync(feeStructure);
    }

    public async Task<StandardFeeStructureResponse> GetByStandardAsync(Guid standardId)
    {
        var standard = await standardRepository.GetAsync(standardId);

        var feeStructures = await feeStructureRepository.GetListAsync(x => x.StandardId == standardId);

        // Preload payment terms for display
        var allTermIds = feeStructures
            .SelectMany(fs => fs.FeeStructureDetails)
            .Where(d => d.PaymentTermId.HasValue)
            .Select(d => d.PaymentTermId!.Value)
            .Distinct()
            .ToList();

        var terms = allTermIds.Count > 0
            ? (await paymentTermRepository.GetListAsync(t => allTermIds.Contains(t.Id)))
                .ToDictionary(t => t.Id)
            : new Dictionary<Guid, PaymentTerm>();

        return new StandardFeeStructureResponse
        {
            StandardId = standard.Id,
            StandardName = standard.Name,
            StandardFeeStructures = feeStructures.ConvertAll(fs => new StandardFeeStructureResponseItem
            {
                Id = fs.Id,
                Name = fs.Name,
				Status = fs.IsLocked ? FeeStructureStatus.Locked : FeeStructureStatus.Unlocked,
				CollectionPolicyId = fs.CollectionPolicyId,
				CollectionPolicyVersionId = fs.CollectionPolicyVersionId,
				ConcessionPolicyId = fs.ConcessionPolicyId,
				ConcessionPolicyVersionId = fs.ConcessionPolicyVersionId,
				RefundPolicyId = fs.RefundPolicyId,
				RefundPolicyVersionId = fs.RefundPolicyVersionId,
				ProrationPolicyId = fs.ProrationPolicyId,
				ProrationPolicyVersionId = fs.ProrationPolicyVersionId,
				ConcessionFeeHeadScope = fs.ConcessionFeeHeadScope,
				ConcessionApplicableFeeHeadIds = [.. fs.GetConcessionApplicableFeeHeadIds()],
				FeeHeads = fs.FeeStructureDetails.Select(d =>
                {
                    var response = new FeeHeadAssignmentResponse
                    {
                        FeeHeadId = d.FeeHeadId,
                        FeeHeadName = d.FeeHead?.Name,
                        CategoryName = d.FeeHead?.FeeCategory?.Name,
                        Amount = d.Amount,
                        IsOneTime = d.IsOneTime,
                        IsOptional = d.IsOptional,
						PayableMonth = d.IsOneTime ? d.PayableMonth : null,
						PayableYear = d.IsOneTime ? d.PayableYear : null,
                        PaymentTermId = d.PaymentTermId,
                        PaymentTermVersionId = d.PaymentTermVersionId,
                        ScheduleMode = d.ScheduleMode
                    };

                    // Populate Payment Term display fields
                    if (d.PaymentTermId.HasValue && terms.TryGetValue(d.PaymentTermId.Value, out var term))
                    {
                        response.PaymentTermName = term.Name;
                        var currentVersion = term.GetCurrentVersion();
                        response.PaymentTermVersionNumber = currentVersion?.VersionNumber;

                        // Check if a newer version is available
                        if (d.PaymentTermVersionId.HasValue && currentVersion != null &&
                            d.PaymentTermVersionId.Value != currentVersion.Id)
                        {
                            response.HasNewVersionAvailable = true;
                        }
                    }

                    return response;
                }).ToList()
            })
        };
    }

    public async Task LockAsync(Guid id)
    {
        var feeStructure = await feeStructureRepository.GetAsync(id);

        feeStructure.Lock();

        await feeStructureRepository.UpdateAsync(feeStructure);
    }

    public async Task<List<AcademicYearMonthItem>> GetMonthsByAcademicYearAsync(Guid academicYearId)
    {
        var academicYear = await academicYearRepository.GetAsync(academicYearId);

        var result = new List<AcademicYearMonthItem>();
        var cursor = new DateTime(academicYear.StartDate.Year, academicYear.StartDate.Month, 1);
        var endMonth = new DateTime(academicYear.EndDate.Year, academicYear.EndDate.Month, 1);

        while (cursor <= endMonth)
        {
            result.Add(new AcademicYearMonthItem
            {
                Month = (MonthEnum)(cursor.Month - 1),
                Year = cursor.Year
            });
            cursor = cursor.AddMonths(1);
        }

        return result;
    }

	private async Task EnsureAssignablePaymentTermAsync(Guid paymentTermId, Guid versionId)
	{
		var term = await paymentTermRepository.FindAsync(paymentTermId);
		if (term == null)
		{
			throw new BusinessException(PaymentTermErrorCodes.NotFound)
				.WithData("PaymentTermId", paymentTermId);
		}

		var published = term.GetPublishedVersion();
		if (term.Status != PaymentTermStatus.Active || published == null || published.Id != versionId)
		{
			throw new BusinessException(PaymentTermErrorCodes.VersionNotPublished)
				.WithData("PaymentTermId", paymentTermId)
				.WithData("VersionId", versionId);
		}
	}

	private async Task<(Guid? PolicyId, Guid? VersionId, ConcessionFeeHeadScope FeeHeadScope, IReadOnlyList<Guid> ApplicableFeeHeadIds)> ResolveConcessionPolicyAsync(
		Guid feeStructureMetadataId,
		Guid? policyId,
		ConcessionFeeHeadScope feeHeadScope,
		IReadOnlyCollection<Guid> requestedApplicableFeeHeadIds,
		IReadOnlyCollection<Guid> structureFeeHeadIds)
	{
		if (!policyId.HasValue) throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotAttached);
		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructureMetadataId);
		if (!metadata.AcademicYearId.HasValue)
			throw new BusinessException(ConcessionPolicyErrorCodes.AcademicYearMismatch);
		var policy = await concessionPolicyRepository.FindAsync(policyId.Value)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotFound);
		if (policy.Status != ConcessionPolicyStatus.Active)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotActive);
		if (policy.AcademicYearId != metadata.AcademicYearId.Value)
			throw new BusinessException(ConcessionPolicyErrorCodes.AcademicYearMismatch);
		if (!Enum.IsDefined(feeHeadScope))
			throw new BusinessException(ConcessionPolicyErrorCodes.InvalidCalculationRule);

		var structureIds = structureFeeHeadIds.Distinct().ToList();
		var heads = await feeHeadRepository.GetListAsync(x => structureIds.Contains(x.Id));
		if (heads.Count != structureIds.Count || heads.Any(x =>
			x.AcademicYearId != metadata.AcademicYearId.Value || !x.IsActive))
			throw new BusinessException(ConcessionPolicyErrorCodes.FeeHeadRequired);
		var eligibleIds = heads.Where(x => x.IsConcessionApplied).Select(x => x.Id).ToHashSet();
		IReadOnlyList<Guid> effectiveIds;
		if (feeHeadScope == ConcessionFeeHeadScope.SelectedFeeHeads)
		{
			var requestedIds = requestedApplicableFeeHeadIds.Distinct().ToList();
			if (requestedIds.Count == 0 || requestedIds.Any(x => !eligibleIds.Contains(x)))
				throw new BusinessException(ConcessionPolicyErrorCodes.FeeHeadRequired);
			effectiveIds = requestedIds;
		}
		else
		{
			effectiveIds = eligibleIds.Order().ToList();
		}
		if (effectiveIds.Count == 0)
			throw new BusinessException(ConcessionPolicyErrorCodes.FeeHeadRequired);
		var versions = await financePolicyVersionRepository.GetListAsync(x =>
			x.PolicyType == "concession" && x.PolicyId == policy.Id && x.VersionNumber == policy.Version);
		var version = versions.SingleOrDefault()
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		return (policy.Id, version.Id, feeHeadScope, effectiveIds);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ManageAttachment)]
	public async Task<CollectionPolicyAttachmentImpactResponse> ValidateCollectionPolicyAttachmentAsync(
		CollectionPolicyAttachmentImpactRequest input)
	{
		var attachment = await ResolveCollectionPolicyAttachmentAsync(
			input.FeeStructureMetadataId, input.CollectionPolicyId, null, input.FeeHeadIds);
		return attachment.Impact;
	}

	private async Task<(Guid? PolicyId, Guid? VersionId)> ResolveRefundPolicyAsync(
		Guid feeStructureMetadataId,
		Guid? policyId,
		Guid? requestedVersionId,
		IReadOnlyCollection<Guid> structureFeeHeadIds)
	{
		if (!policyId.HasValue)
		{
			if (requestedVersionId.HasValue)
				throw new BusinessException(RefundPolicyErrorCodes.PolicyVersionNotFound);
			return (null, null);
		}

		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructureMetadataId);
		if (!metadata.AcademicYearId.HasValue)
			throw new BusinessException(RefundPolicyErrorCodes.AcademicYearMismatch);
		var policy = await refundPolicyRepository.FindAsync(policyId.Value)
			?? throw new BusinessException(RefundPolicyErrorCodes.PolicyNotFound);
		if (policy.Status != RefundPolicyStatus.Active)
			throw new BusinessException(RefundPolicyErrorCodes.PolicyNotActive);
		if (policy.AcademicYearId != metadata.AcademicYearId.Value)
			throw new BusinessException(RefundPolicyErrorCodes.AcademicYearMismatch);

		var structureIds = structureFeeHeadIds.Distinct().ToHashSet();
		if (policy.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads &&
			!policy.GetApplicableFeeHeadIds().Any(structureIds.Contains))
			throw new BusinessException(RefundPolicyErrorCodes.InvalidFeeHeadReference);

		var versions = await financePolicyVersionRepository.GetListAsync(x =>
			x.PolicyType == "refund" && x.PolicyId == policy.Id && x.VersionNumber == policy.Version);
		var version = versions.SingleOrDefault()
			?? throw new BusinessException(RefundPolicyErrorCodes.PolicyVersionNotFound);
		if (requestedVersionId.HasValue && requestedVersionId.Value != version.Id)
			throw new BusinessException(RefundPolicyErrorCodes.PolicyVersionNotFound);
		return (policy.Id, version.Id);
	}

	private async Task<(Guid? PolicyId, Guid? VersionId)> ResolveProrationPolicyAsync(
		Guid feeStructureMetadataId,
		Guid? policyId,
		Guid? requestedVersionId,
		IReadOnlyCollection<FeeHeadAssignmentRequest> structureDetails)
	{
		if (!policyId.HasValue)
		{
			if (requestedVersionId.HasValue)
				throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionNotFound);
			return (null, null);
		}

		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructureMetadataId);
		if (!metadata.AcademicYearId.HasValue)
			throw new BusinessException(ProrationPolicyErrorCodes.AcademicYearMismatch);
		var policy = await prorationPolicyRepository.FindAsync(policyId.Value)
			?? throw new BusinessException(ProrationPolicyErrorCodes.PolicyNotFound);
		if (policy.Status != ProrationPolicyStatus.Active)
			throw new BusinessException(ProrationPolicyErrorCodes.PolicyNotActive);
		if (policy.AcademicYearId != metadata.AcademicYearId.Value)
			throw new BusinessException(ProrationPolicyErrorCodes.AcademicYearMismatch);

		var nonFullRuleHeads = policy.Rules
			.Where(x => x.RuleType != ProrationRuleType.FullCharge)
			.Select(x => x.FeeHeadId)
			.ToHashSet();
		if (structureDetails.Any(x => x.IsOneTime && nonFullRuleHeads.Contains(x.FeeHeadId)))
			throw new BusinessException(ProrationPolicyErrorCodes.OneTimeFeeCannotBeProrated);

		var versions = await financePolicyVersionRepository.GetListAsync(x =>
			x.PolicyType == "proration" && x.PolicyId == policy.Id && x.VersionNumber == policy.Version);
		var version = versions.SingleOrDefault()
			?? throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionNotFound);
		if (requestedVersionId.HasValue && requestedVersionId.Value != version.Id)
			throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionNotFound);
		return (policy.Id, version.Id);
	}

	private async Task<(Guid PolicyId, Guid VersionId, CollectionPolicyAttachmentImpactResponse Impact)>
		ResolveCollectionPolicyAttachmentAsync(
			Guid feeStructureMetadataId,
			Guid? collectionPolicyId,
			Guid? requestedVersionId,
			IReadOnlyCollection<Guid> structureFeeHeadIds)
	{
		if (!collectionPolicyId.HasValue)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyNotAttached);
		}

		var metadata = await feeStructureMetadataRepository.FindAsync(feeStructureMetadataId);
		if (metadata?.AcademicYearId is not { } academicYearId)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.AcademicYearRequired);
		}

		var policy = await collectionPolicyRepository.FindAsync(collectionPolicyId.Value)
			?? throw new BusinessException(CollectionPolicyErrorCodes.PolicyNotFound);
		if (policy.Status != CollectionPolicyStatus.Active)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyNotActive);
		}
		if (policy.AcademicYearId != academicYearId)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.AcademicYearMismatch);
		}

		var distinctStructureHeads = structureFeeHeadIds.Distinct().ToList();
		var heads = await feeHeadRepository.GetListAsync(x => distinctStructureHeads.Contains(x.Id));
		if (heads.Count != distinctStructureHeads.Count || heads.Any(x => !x.IsActive || x.AcademicYearId != academicYearId))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.InvalidFeeHeadReference);
		}

		var versions = await financePolicyVersionRepository.GetListAsync(x =>
			x.PolicyType == "collection" && x.PolicyId == policy.Id && x.VersionNumber == policy.Version);
		var version = versions.SingleOrDefault()
			?? throw new BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		if (requestedVersionId.HasValue && requestedVersionId.Value != version.Id)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		}

		var structureSet = distinctStructureHeads.ToHashSet();
		var lateFeeSet = policy.GetLateFeeApplyOnFeeHeads().ToHashSet();
		var waiverSet = policy.GetWaiverApplyOnFeeHeads().ToHashSet();
		var coveredLateFee = structureSet.Intersect(lateFeeSet).Order().ToList();
		var coveredWaiver = structureSet.Intersect(waiverSet).Order().ToList();
		var impact = new CollectionPolicyAttachmentImpactResponse
		{
			PolicyId = policy.Id,
			PolicyVersionId = version.Id,
			PolicyName = policy.PolicyName,
			PolicyCode = policy.PolicyCode,
			CoveredLateFeeHeadIds = coveredLateFee,
			StructureHeadsWithoutLateFee = structureSet.Except(lateFeeSet).Order().ToList(),
			PolicyLateFeeHeadsNotInStructure = lateFeeSet.Except(structureSet).Order().ToList(),
			CoveredWaiverHeadIds = coveredWaiver,
			StructureHeadsWithoutWaiver = structureSet.Except(waiverSet).Order().ToList(),
			PolicyWaiverHeadsNotInStructure = waiverSet.Except(structureSet).Order().ToList(),
			IsAttachable = true
		};
		if (policy.EnableLateFee && coveredLateFee.Count == 0)
		{
			impact.Warnings.Add(
				"Late fee is enabled, but none of its selected Fee Heads are in this fee structure. No late fee will be charged for this structure.");
		}
		if (policy.EnableWaiver && coveredWaiver.Count == 0)
		{
			impact.Warnings.Add(
				"Waiver is enabled, but none of its selected Fee Heads are in this fee structure. No waiver will be available for this structure.");
		}
		if (impact.PolicyLateFeeHeadsNotInStructure.Count > 0)
		{
			impact.Warnings.Add("Some policy late-fee heads are not present in this fee structure.");
		}
		if (impact.PolicyWaiverHeadsNotInStructure.Count > 0)
		{
			impact.Warnings.Add("Some policy waiver heads are not present in this fee structure.");
		}
		return (policy.Id, version.Id, impact);
	}
}
