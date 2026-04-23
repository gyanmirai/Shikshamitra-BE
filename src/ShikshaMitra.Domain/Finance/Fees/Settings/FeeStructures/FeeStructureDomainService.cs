using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

using FeeStructureEntity = ShikshaMitra.Finance.Fees.Settings.FeeStructures.FeeStructure;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures;

/// <summary>
/// Domain-level input for a single fee head assignment within a structure plan.
/// </summary>
public record FeeHeadEntry(Guid FeeHeadId, double Amount, bool IsOneTime, bool IsOptional);

/// <summary>
/// Domain-level input for a per-standard fee structure to be created.
/// </summary>
public record StandardFeeStructurePlan(string Name, IReadOnlyList<FeeHeadEntry> FeeHeads);

/// <summary>
/// Domain-level query result carrying all data needed to render a standard's fee structures.
/// </summary>
public record StandardFeeStructureQueryResult(
    Standard Standard,
    IReadOnlyList<FeeStructureEntity> Structures,
    IReadOnlyDictionary<Guid, FeeHead> FeeHeadDict,
    IReadOnlyDictionary<Guid, string> CategoryDict);

public class FeeStructureDomainService(
    IRepository<FeeStructureEntity, Guid> feeStructureRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IRepository<FeeCategory, Guid> feeCategoryRepository,
    IRepository<Standard, Guid> standardRepository,
    IGuidGenerator guidGenerator) : DomainService
{
    /// <summary>
    /// Replaces all fee structures for the given standard under a fee structure metadata entry.
    /// Validates that the standard is compatible with the metadata, then deletes existing
    /// structures and creates the new ones — all operations go through the aggregate root.
    /// </summary>
    public async Task SaveStandardFeeStructuresAsync(
        FeeStructureMetadata metadata,
        Guid standardId,
        Guid? tenantId,
        IReadOnlyList<StandardFeeStructurePlan> plans)
    {
        var standard = await standardRepository.GetAsync(standardId);

        // Domain invariant: standard must belong to same board and medium as the metadata.
        metadata.EnsureStandardIsCompatible(standard);

        // Delete existing FeeStructure aggregates for this standard.
        // EF cascade delete handles FeeStructureDetail rows automatically.
        var existing = await feeStructureRepository.GetListAsync(
            x => x.FeeStructureMetadataId == metadata.Id && x.StandardId == standard.Id);

        if (existing.Count > 0)
            await feeStructureRepository.DeleteManyAsync(existing, autoSave: true);

        if (plans is not { Count: > 0 })
            return;

        foreach (var plan in plans)
        {
            var feeStructure = new FeeStructureEntity(
                guidGenerator.Create(),
                tenantId,
                metadata.Id,
                standard.Id,
                metadata.AcademicYearId!.Value,
                metadata.AcademicBoardId!.Value,
                metadata.AcademicMediumId!.Value,
                metadata.ReceiptBookId!.Value,
                plan.Name);

            foreach (var fh in plan.FeeHeads)
            {
                feeStructure.AddDetail(new FeeStructureDetail(
                    guidGenerator.Create(),
                    tenantId,
                    feeStructure.Id,
                    fh.FeeHeadId,
                    fh.Amount,
                    fh.IsOneTime,
                    fh.IsOptional));
            }

            await feeStructureRepository.InsertAsync(feeStructure);
        }
    }

    /// <summary>
    /// Fetches all fee structures for a given standard together with the fee head and
    /// category lookup data required to render them. Returns a result record so the
    /// caller (application service) only needs to map to DTOs.
    /// </summary>
    public async Task<StandardFeeStructureQueryResult> GetStandardFeeStructuresAsync(Guid standardId)
    {
        var standard = await standardRepository.GetAsync(standardId);

        var structures = await feeStructureRepository.GetListAsync(x => x.StandardId == standardId, includeDetails: true);

        if (structures.Count == 0)
            return new StandardFeeStructureQueryResult(
                standard,
                structures,
                new Dictionary<Guid, FeeHead>(),
                new Dictionary<Guid, string>());

        var details = structures.SelectMany(x => x.FeeStructureDetails).ToList();

        var feeHeadIds = details.Select(x => x.FeeHeadId).Distinct().ToList();
        var feeHeads = await feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id));
        var feeHeadDict = feeHeads.ToDictionary(x => x.Id);

        var categoryIds = feeHeads.Select(x => x.FeeCategoryId).Distinct().ToList();
        var categories = await feeCategoryRepository.GetListAsync(x => categoryIds.Contains(x.Id));
        var categoryDict = categories.ToDictionary(x => x.Id, x => x.Name);

        return new StandardFeeStructureQueryResult(standard, structures, feeHeadDict, categoryDict);
    }
}
