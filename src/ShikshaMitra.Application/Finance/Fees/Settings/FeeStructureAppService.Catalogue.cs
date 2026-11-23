using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService
{
 [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
 public async Task<List<FeeStructureSummaryDto>> GetSummariesAsync(Guid standardId)
 {
  await EnsureStandardScopeAsync(standardId);
  return await AsyncExecuter.ToListAsync((await feeStructureRepository.GetQueryableAsync())
   .Where(x => x.StandardId == standardId).OrderBy(x => x.CreationTime).ThenBy(x => x.Id)
   .Select(x => new FeeStructureSummaryDto { Id = x.Id, Name = x.Name,
    Status = x.IsLocked ? FeeStructureStatus.Locked : FeeStructureStatus.Unlocked,
    ConcurrencyStamp = x.ConcurrencyStamp, RevisionNumber = x.RevisionNumber }));
 }

 [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
 public async Task<FeeStructureCreateResponse> GetDetailAsync(Guid id) => await MapAsync(await GetScopedAsync(id));

 [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
 public async Task<FeeStructureCatalogueDto> GetCatalogueAsync(Guid feeStructureMetadataId, Guid standardId)
 {
  var context = await GetContextAsync(feeStructureMetadataId, standardId);
  var result = new FeeStructureCatalogueDto();
  var versions = (await financePolicyVersionRepository.GetQueryableAsync())
   .Where(v => v.AcademicYearId == context.AcademicYearId && v.Status == "Active");
  // Exact current master/version joins exclude historical snapshots before materialization.
  if (context.Permissions.GetValueOrDefault("collectionRead"))
  {
   var masters = (await collectionPolicyRepository.GetQueryableAsync())
    .Where(p => p.AcademicYearId == context.AcademicYearId && p.Status == CollectionPolicyStatus.Active);
   result.Policies.AddRange(await AsyncExecuter.ToListAsync(
    from p in masters join v in versions.Where(v => v.PolicyType == "collection")
    on new { PolicyId = p.Id, Version = p.Version } equals new { v.PolicyId, Version = v.VersionNumber }
    select new FeeStructurePolicyOptionDto { Id = p.Id, Name = p.PolicyName, Family = "collection",
     VersionId = v.Id, VersionNumber = v.VersionNumber, IsCurrent = true, IsDefault = p.IsDefault }));
  }
  if (context.Permissions.GetValueOrDefault("refundRead"))
  {
   var masters = (await refundPolicyRepository.GetQueryableAsync())
    .Where(p => p.AcademicYearId == context.AcademicYearId && p.Status == RefundPolicyStatus.Active);
   var refunds = await AsyncExecuter.ToListAsync(
    from p in masters join v in versions.Where(v => v.PolicyType == "refund")
    on new { PolicyId = p.Id, Version = p.Version } equals new { v.PolicyId, Version = v.VersionNumber }
    select new { p.Id, p.PolicyName, p.IsDefault, p.FeeHeadScope, p.ApplicableFeeHeadIdsJson,
     VersionId = v.Id, v.VersionNumber });
   var options = refunds.Select(row => new FeeStructurePolicyOptionDto {
    Id = row.Id, Name = row.PolicyName, Family = "refund", VersionId = row.VersionId,
    VersionNumber = row.VersionNumber, IsCurrent = true, IsDefault = row.IsDefault,
    FeeHeadScope = row.FeeHeadScope.ToString(),
    ApplicableFeeHeadIds = row.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads
     ? JsonSerializer.Deserialize<List<Guid>>(row.ApplicableFeeHeadIdsJson) ?? [] : []
   }).ToList();
   var headIds = options.SelectMany(option => option.ApplicableFeeHeadIds!).Distinct().ToArray();
   if (headIds.Length > 0)
   {
    var heads = await AsyncExecuter.ToListAsync((await feeHeadRepository.GetQueryableAsync())
     .Where(head => head.AcademicYearId == context.AcademicYearId && Enumerable.Contains(headIds, head.Id))
     .Select(head => new { head.Id, head.Name }));
    var names = heads.ToDictionary(head => head.Id, head => head.Name);
    foreach (var option in options)
     option.FeeHeadNames = option.ApplicableFeeHeadIds!.Distinct().Where(names.ContainsKey)
      .ToDictionary(id => id, id => names[id]);

    var missingNameVersions = options
     .Where(option => option.ApplicableFeeHeadIds!.Any(id => !names.ContainsKey(id)))
     .ToDictionary(option => option.VersionId);
    if (missingNameVersions.Count > 0)
    {
     var versionIds = missingNameVersions.Keys.ToArray();
     var snapshots = await AsyncExecuter.ToListAsync(versions
      .Where(version => Enumerable.Contains(versionIds, version.Id))
      .Select(version => new { version.Id, version.SnapshotJson }));
     foreach (var row in snapshots)
     {
      RefundPolicySnapshot? snapshot;
      try { snapshot = JsonSerializer.Deserialize<RefundPolicySnapshot>(row.SnapshotJson, SnapshotOptions); }
      catch (JsonException) { continue; }
      if (snapshot?.FeeHeadNames == null) continue;
      var option = missingNameVersions[row.Id];
      foreach (var id in option.ApplicableFeeHeadIds!)
       if (snapshot.FeeHeadNames.TryGetValue(id, out var name))
        option.FeeHeadNames!.TryAdd(id, name);
     }
    }
   }
   result.Policies.AddRange(options);
  }
  if (context.Permissions.GetValueOrDefault("prorationRead"))
  {
   var masters = (await prorationPolicyRepository.GetQueryableAsync())
    .Where(p => p.AcademicYearId == context.AcademicYearId && p.Status == ProrationPolicyStatus.Active);
   result.Policies.AddRange(await AsyncExecuter.ToListAsync(
    from p in masters join v in versions.Where(v => v.PolicyType == "proration")
    on new { PolicyId = p.Id, Version = p.Version } equals new { v.PolicyId, Version = v.VersionNumber }
    select new FeeStructurePolicyOptionDto { Id = p.Id, Name = p.PolicyName, Family = "proration",
     VersionId = v.Id, VersionNumber = v.VersionNumber, IsCurrent = true, IsDefault = p.IsDefault }));
  }
  if (context.Permissions.GetValueOrDefault("termsRead"))
  {
   result.Terms = await AsyncExecuter.ToListAsync((await paymentTermRepository.GetQueryableAsync())
    .Where(t => t.AcademicYearId == context.AcademicYearId && t.BoardId == context.BoardId && t.Status == PaymentTermStatus.Active && !t.Versions.Any(v => v.Status == PaymentTermVersionStatus.Draft))
    .OrderBy(t => t.Name).Select(t => new FeeStructureTermOptionDto { Id = t.Id, Name = t.Name, IsDefault = t.IsDefault, IsCurrent = true,
     VersionId = t.Versions.Where(v => v.Status == PaymentTermVersionStatus.Published).OrderByDescending(v => v.VersionNumber).Select(v => v.Id).FirstOrDefault(),
     VersionNumber = t.Versions.Where(v => v.Status == PaymentTermVersionStatus.Published).OrderByDescending(v => v.VersionNumber).Select(v => v.VersionNumber).FirstOrDefault() })
    .Where(t => t.VersionId != Guid.Empty));
  }
  result.Policies = result.Policies.OrderBy(p => p.Name).ThenBy(p => p.Id).ToList();
  return result;
 }

 [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
 [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.View)]
 public async Task<FeeStructureTermOptionDto> GetTermScheduleAsync(Guid feeStructureMetadataId, Guid standardId, Guid paymentTermId, Guid versionId)
 {
  var context = await GetContextAsync(feeStructureMetadataId, standardId);
  var row = await AsyncExecuter.FirstOrDefaultAsync((await paymentTermRepository.GetQueryableAsync())
   .Where(t => t.Id == paymentTermId && t.AcademicYearId == context.AcademicYearId && t.BoardId == context.BoardId)
   .SelectMany(t => t.Versions.Where(v => v.Id == versionId).Select(v => new FeeStructureTermOptionDto {
    Id = t.Id, Name = t.Name, VersionId = v.Id, VersionNumber = v.VersionNumber, IsCurrent = v.Status == PaymentTermVersionStatus.Published,
    IsDefault = t.IsDefault && v.Status == PaymentTermVersionStatus.Published,
    DemandGenerationRule = v.DemandGenerationRule.ToString(), DemandOffsetDays = v.DemandOffsetDays,
    Installments = v.Installments.OrderBy(i => i.Sequence).Select(i => new ScheduleOverrideDto { SourcePaymentTermInstallmentId = i.Id, Sequence = i.Sequence, Name = i.Name,
     CoverageStartDate = i.CoverageStartDate, CoverageEndDate = i.CoverageEndDate, DemandDate = i.DemandDate, DueDate = i.DueDate, AllocationPercentage = i.AllocationPercentage }).ToList()
   })));
  return row ?? throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration);
 }
}
