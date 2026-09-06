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
	IRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository
) : ApplicationService, IFeeStructureAppService
{
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

		feeStructure.AttachCollectionPolicy(input.StandardFeeStructures?.CollectionPolicyId);
		var concessionAttachment = await ResolveConcessionPolicyAsync(
			feeStructureMetadataId,
			input.StandardFeeStructures?.ConcessionPolicyId,
			input.StandardFeeStructures?.ConcessionFeeHeadScope ?? ConcessionFeeHeadScope.EntireFeeStructure,
			input.StandardFeeStructures?.ConcessionApplicableFeeHeadIds ?? [],
			input.StandardFeeStructures?.FeeHeads.Select(x => x.FeeHeadId).ToList() ?? []);
		feeStructure.AttachConcessionPolicy(
			concessionAttachment.PolicyId, concessionAttachment.VersionId,
			concessionAttachment.FeeHeadScope, concessionAttachment.ApplicableFeeHeadIds);

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
            Name = feeStructure.Name
        };
    }

    public async Task UpdateAsync(Guid id, StandardFeeStructureRequest input)
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
		feeStructure.AttachCollectionPolicy(input.CollectionPolicyId);
		var concessionAttachment = await ResolveConcessionPolicyAsync(
			feeStructure.FeeStructureMetadataId,
			input.ConcessionPolicyId,
			input.ConcessionFeeHeadScope,
			input.ConcessionApplicableFeeHeadIds,
			input.FeeHeads.Select(x => x.FeeHeadId).ToList());
		feeStructure.AttachConcessionPolicy(
			concessionAttachment.PolicyId, concessionAttachment.VersionId,
			concessionAttachment.FeeHeadScope, concessionAttachment.ApplicableFeeHeadIds);

		// validation to ensure no duplicate fee structure name exists for the same standard
		var existingStructure = await feeStructureRepository.AnyAsync(x => x.StandardId == feeStructure.StandardId && x.Name == feeStructure.Name && x.Id != feeStructure.Id);

		if (existingStructure)
		{
			throw new BusinessException("A fee structure with the same name already exists for this standard. Please choose a different name.");
		}

		await feeStructureRepository.UpdateAsync(feeStructure);
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
				ConcessionPolicyId = fs.ConcessionPolicyId,
				ConcessionPolicyVersionId = fs.ConcessionPolicyVersionId,
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
}
