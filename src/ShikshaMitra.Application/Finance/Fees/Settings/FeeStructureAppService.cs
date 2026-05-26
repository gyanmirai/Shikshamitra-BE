using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Collections;
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
using ShikshaMitra.Finance.Setting;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeStructureAppService(
    IRepository<FeeStructure, Guid> feeStructureRepository,
    IStandardRepository standardRepository,
    IAcademicYearRepository academicYearRepository
) : ApplicationService, IFeeStructureAppService
{
    public async Task<FeeStructureCreateResponse> CreateAsync(Guid feeStructureMetadataId, StandardFeeStructureSaveRequest input)
    {
        var feeStructure = new FeeStructure(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            feeStructureMetadataId,
            input.StandardId,
            input.StandardFeeStructures?.Name ?? string.Empty
        );

        if (input.StandardFeeStructures != null)
        {
            var structureRequest = input.StandardFeeStructures;
            {
                foreach (var feeHead in structureRequest.FeeHeads)
                {
                    feeStructure.AddDetail(new FeeStructureDetail(
                        GuidGenerator.Create(),
                        CurrentTenant.Id,
                        feeStructure.Id,
                        feeHead.FeeHeadId,
                        feeHead.Amount,
                        feeHead.IsOneTime,
                        feeHead.IsOptional,
						feeHead.IsRefundable,
						feeHead.PayableMonth,
						feeHead.PayableYear
					));
                }
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
                feeStructure.AddDetail(new FeeStructureDetail(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    feeStructure.Id,
                    feeHead.FeeHeadId,
                    feeHead.Amount,
                    feeHead.IsOneTime,
                    feeHead.IsOptional,
					feeHead.IsRefundable,
					feeHead.PayableMonth,
					feeHead.PayableYear
				));
            }
        }

		feeStructure.ChangeName(input.Name);

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

        return new StandardFeeStructureResponse
        {
            StandardId = standard.Id,
            StandardName = standard.Name,
            StandardFeeStructures = feeStructures.ConvertAll(fs => new StandardFeeStructureResponseItem
            {
                Id = fs.Id,
                Name = fs.Name,
				Status = fs.IsLocked ? FeeStructureStatus.Locked : FeeStructureStatus.Unlocked,
				FeeHeads = fs.FeeStructureDetails.Select(d => new FeeHeadAssignmentResponse
                {
                    FeeHeadId = d.FeeHeadId,
                    FeeHeadName = d.FeeHead?.Name,
                    CategoryName = d.FeeHead?.FeeCategory?.Name,
                    Amount = d.Amount,
                    IsOneTime = d.IsOneTime,
                    IsOptional = d.IsOptional,
					IsRefundable = d.IsRefundable,
					PayableMonth = d.IsOneTime ? d.PayableMonth : null,
					PayableYear = d.IsOneTime ? d.PayableYear : null
				}).ToList()
            })        };
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
}
