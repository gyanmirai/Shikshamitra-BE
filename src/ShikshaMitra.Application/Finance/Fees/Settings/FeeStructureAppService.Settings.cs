using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
	public async Task<FeeStructureSettingsDto> GetSettingsAsync(Guid feeStructureMetadataId, Guid standardId)
	{
		var scope = await EnsureStandardScopeAsync(standardId);
		var metadata = await GetMetadataReferenceAsync(feeStructureMetadataId);
		if (metadata.AcademicYearId != scope.AcademicYearId || metadata.AcademicBoardId != scope.EducationBoardId || metadata.AcademicMediumId != scope.EducationMediumId)
			throw new BusinessException(FeeStructureErrorCodes.ScopeMismatch);
		var year = await academicYearRepository.GetAsync(scope.AcademicYearId!.Value);
		var board = await GetBoardCalendarAsync(scope.EducationBoardId!.Value);
		var standard = await standardRepository.GetAsync(standardId);
		var heads = await feeHeadRepository.GetListAsync(x => x.AcademicYearId == year.Id && x.IsActive);
		var versions = await ActivePolicyVersionsAsync(year.Id);
		// Default selection belongs to the current master. Historical snapshots retain
		// their original flags and must not become defaults after a replacement.
		var policyMasters = new Dictionary<(string Family, Guid Id), (int Version, bool IsDefault)>();
		foreach (var policy in await collectionPolicyRepository.GetListAsync(x => x.AcademicYearId == year.Id && x.Status == CollectionPolicyStatus.Active))
			policyMasters[("collection", policy.Id)] = (policy.Version, policy.IsDefault);
		foreach (var policy in await concessionPolicyRepository.GetListAsync(x => x.AcademicYearId == year.Id && x.Status == ConcessionPolicyStatus.Active))
			policyMasters[("concession", policy.Id)] = (policy.Version, policy.IsDefault);
		foreach (var policy in await refundPolicyRepository.GetListAsync(x => x.AcademicYearId == year.Id && x.Status == RefundPolicyStatus.Active))
			policyMasters[("refund", policy.Id)] = (policy.Version, policy.IsDefault);
		foreach (var policy in await prorationPolicyRepository.GetListAsync(x => x.AcademicYearId == year.Id && x.Status == ProrationPolicyStatus.Active))
			policyMasters[("proration", policy.Id)] = (policy.Version, policy.IsDefault);
		var terms = await paymentTermRepository.GetListAsync(x => x.AcademicYearId == year.Id && x.BoardId == board.Id && x.Status == PaymentTermStatus.Active);
		var result = new FeeStructureSettingsDto
		{
			AcademicYearId = year.Id, AcademicYearName = year.Name, BoardId = board.Id, ClassName = standard.Name,
			StartDate = board.StartDate, EndDate = board.EndDate,
			Heads = heads.OrderBy(x => x.Name).Select(x => new FeeStructureHeadOptionDto { Id = x.Id, Name = x.Name, CategoryName = x.FeeCategory?.Name ?? "", IsConcessionApplied = x.IsConcessionApplied }).ToList(),
			Policies = versions.Where(x => policyMasters.ContainsKey((x.PolicyType, x.PolicyId)))
				.OrderBy(x => x.PolicyName).ThenByDescending(x => x.VersionNumber).Select(x => new FeeStructurePolicyOptionDto
			{
				Id = x.PolicyId, VersionId = x.Id, Family = x.PolicyType, Name = x.PolicyName, VersionNumber = x.VersionNumber,
				IsCurrent = x.VersionNumber == policyMasters[(x.PolicyType, x.PolicyId)].Version,
				IsDefault = x.VersionNumber == policyMasters[(x.PolicyType, x.PolicyId)].Version && policyMasters[(x.PolicyType, x.PolicyId)].IsDefault
			}).ToList()
		};
		foreach (var term in terms.OrderBy(x => x.Name))
			foreach (var version in term.Versions.Where(x => x.Status is PaymentTermVersionStatus.Published or PaymentTermVersionStatus.Superseded).OrderByDescending(x => x.VersionNumber))
				result.Terms.Add(new FeeStructureTermOptionDto
				{
					Id = term.Id, VersionId = version.Id, Name = term.Name, VersionNumber = version.VersionNumber,
					IsCurrent = version.Status == PaymentTermVersionStatus.Published, DemandGenerationRule = version.DemandGenerationRule.ToString(), DemandOffsetDays = version.DemandOffsetDays,
					Installments = version.Installments.OrderBy(x => x.Sequence).Select(x => new ScheduleOverrideDto
					{
						SourcePaymentTermInstallmentId = x.Id, Sequence = x.Sequence, Name = x.Name, CoverageStartDate = x.CoverageStartDate,
						CoverageEndDate = x.CoverageEndDate, DemandDate = x.DemandDate, DueDate = x.DueDate, AllocationPercentage = x.AllocationPercentage
					}).ToList()
				});
		var permissions = new Dictionary<string, string>
		{
			["add"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add,
			["edit"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add,
			["delete"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Delete,
			["publish"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add,
			["collection"] = ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add,
			["concession"] = ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Add,
			["proration"] = ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Add,
			["refund"] = ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Add
		};
		foreach (var permission in permissions) result.Permissions[permission.Key] = await AuthorizationService.IsGrantedAsync(permission.Value);
		return result;
	}
}
