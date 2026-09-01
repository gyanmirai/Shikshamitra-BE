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

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeStructureAppService(
    IRepository<FeeStructure, Guid> feeStructureRepository,
    IStandardRepository standardRepository,
    IAcademicYearRepository academicYearRepository,
    IRepository<PaymentTerm, Guid> paymentTermRepository
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
}
