using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	// This internal calculation entry point cannot assign or change a Student's enrollment.
	[RemoteService(false)]
	public async Task<AdmissionAssignmentPreparation> PrepareAdmissionAsync(
		AdmissionFinanceContext source, Guid accountId, IReadOnlyList<VariableFeeHeadAmountDto> variableHeads,
		Guid? prorationPolicyVersionId = null, string? policyOverrideReason = null, bool requireOverrideReason = true)
	{
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add))
			throw new Volo.Abp.Authorization.AbpAuthorizationException();
		if (!source.StructureId.HasValue || !source.StructureRevision.HasValue || accountId == Guid.Empty)
			throw new BusinessException(AssignmentErrorCodes.StructureMismatch);
		var input = new AssignStudentFeeStructureDto
		{
			AcademicYearId = source.AcademicYearId, StandardFeeStructureId = source.StructureId.Value,
			SelectedOptionalFeeHeadIds = source.OptionalHeadIds.ToList(), VariableFeeHeads = variableHeads.ToList()
		};
		var resolver = LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Finance.Policies.AdmissionProrationPolicyResolver>();
		var defaultProration = await resolver.ResolveAsync(source.AcademicYearId);
		var proration = prorationPolicyVersionId.HasValue
			? await resolver.ResolveActiveVersionAsync(source.AcademicYearId, prorationPolicyVersionId.Value)
			: defaultProration;
		var isOverride = proration?.Id != defaultProration?.Id;
		if (isOverride)
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.OverridePolicies);
			if (requireOverrideReason && (string.IsNullOrWhiteSpace(policyOverrideReason) || policyOverrideReason.Trim().Length > 500))
				throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		}
		input.PolicyOverrideReason = isOverride
			? string.IsNullOrWhiteSpace(policyOverrideReason)
				? "Selected by authorized staff during Admission fee preparation."
				: policyOverrideReason.Trim()
			: null;
		var prepared = await PrepareForContextAsync(input, new(accountId, source.StudentId, source.StandardId, source.BoardId,
			source.MediumId, source.StructureRevision.Value, source.SourceRevision, source.JoiningDate, proration, source.ApplicationId,
			IsProrationOverride: isOverride));
		for (var i = 0; i < prepared.Dues.Count; i++) prepared.Preview.Lines[i].DueFeeId = prepared.Dues[i].Id;
		var fingerprint = Hash(new { source.StructureId, source.StructureRevision,
			Heads = source.OptionalHeadIds.Order().ToArray(), Variables = variableHeads.OrderBy(h => h.FeeHeadId).ToArray(), source.JoiningDate,
			ProrationVersion = proration?.Id, PolicyOverrideReason = input.PolicyOverrideReason });
		return new(prepared.Structure, prepared.Preview, prepared.Dues, fingerprint);
	}

	private sealed record AdmissionAssignmentContext(Guid? AccountId, Guid? StudentId, Guid StandardId,
		Guid BoardId, Guid MediumId, int? StructureRevision, string SourceRevision, DateTime? JoiningDate,
		ShikshaMitra.Domain.Finance.Policies.FinancePolicyVersion? Proration, Guid ApplicationId,
		bool IsFinanceAssignment = false, bool IsProrationOverride = false);
}

public sealed record AdmissionAssignmentPreparation(FeeStructure Structure, StudentFeeAssignmentPreviewDto Preview,
	List<DueFee> Dues, string Fingerprint);
