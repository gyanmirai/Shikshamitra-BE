using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeStructureAppService(
    IRepository<FeeStructure, Guid> feeStructureRepository,
    IStandardRepository standardRepository
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
                        feeHead.IsOptional
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
                    feeHead.IsOptional
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
            StandardFeeStructures = feeStructures.Select(fs => new StandardFeeStructureResponseItem
            {
                Id = fs.Id,
                Name = fs.Name,
                FeeHeads = fs.FeeStructureDetails.Select(d => new FeeHeadAssignmentResponse
                {
                    FeeHeadId = d.FeeHeadId,
                    FeeHeadName = d.FeeHead?.Name,
                    CategoryName = d.FeeHead?.FeeCategory?.Name,
                    Amount = d.Amount,
                    IsOneTime = d.IsOneTime,
                    IsOptional = d.IsOptional
                }).ToList()
            }).ToList()
        };
    }

    public async Task LockAsync(Guid id)
    {
        var feeStructure = await feeStructureRepository.GetAsync(id);

        feeStructure.Lock();

        await feeStructureRepository.UpdateAsync(feeStructure);
    }
}
