using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalSettlementContext(FinanceExternalReceivableSourceFacts Source,
	FinanceExternalReceivableSourceLink Link, Charge Charge, ChargeRequest Request, Guid ActorId);

public class FinanceExternalSettlementAuthority(
	IFinanceExternalReceivableSourceReader sourceReader,
	IPermissionChecker permissions,
	FinanceScopeAccessService scope,
	IRepository<FinanceExternalReceivableSourceLink, Guid> links,
	IRepository<Charge, Guid> charges,
	IRepository<ChargeRequest, Guid> requests,
	IRepository<IdentityUser, Guid> users,
	IRepository<IdentityRole, Guid> roles,
	ICurrentTenant tenant,
	ICurrentUser user,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	private static readonly HashSet<string> Actions =
	[
		ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View,
		ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
		ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve,
		ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver,
		ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver,
		ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver,
		ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process,
		ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reverse
	];

	public async Task<FinanceExternalSettlementContext> RequireAsync(FinanceExternalSourceReference source, string action)
	{
		if (!tenant.Id.HasValue || !user.Id.HasValue || !Actions.Contains(action) || !await permissions.IsGrantedAsync(action))
			throw new AbpAuthorizationException("Finance source action access denied.");
		return await RequireScopeAsync(source);
	}

	// Shared receipt data scope is also used by separately authorized bank
	// verification actions. It must not require an unrelated receipt-view grant.
	internal async Task<FinanceExternalSettlementContext> RequireScopeAsync(FinanceExternalSourceReference source)
	{
		if (!tenant.Id.HasValue || !user.Id.HasValue) throw new AbpAuthorizationException("Finance source access denied.");
		FinanceExternalReceivableSourceGuard.ValidateReference(source);
		var facts = await sourceReader.ReadAuthorizedAsync(source);
		FinanceExternalReceivableSourceGuard.ValidateFacts(source, facts, tenant.Id);
		await scope.EnsureCollectionYearAccessibleAsync(facts.AcademicYearId);
		var link = await links.FindAsync(row => row.TenantId == tenant.Id && row.SourceKind == source.Kind && row.SourceId == source.SourceId)
			?? throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		if (link.SourceFingerprint != FinanceExternalReceivableFingerprint.Source(facts))
			throw new BusinessException(LibraryFinanceErrorCodes.SourceChanged);
		var charge = await charges.FindAsync(link.FinanceChargeId, includeDetails: false);
		var request = await requests.FindAsync(link.FinanceChargeRequestId, includeDetails: false);
		if (charge == null || request == null || charge.TenantId != tenant.Id || request.TenantId != tenant.Id ||
			charge.ChargeRequestId != request.Id || charge.AcademicYearId != facts.AcademicYearId ||
			request.AcademicYearId != facts.AcademicYearId || link.AcademicYearId != facts.AcademicYearId ||
			charge.StudentId != facts.Payer.StudentId || charge.EmployeeId != facts.Payer.EmployeeId ||
			charge.Amount != facts.Amount || request.Amount != facts.Amount || charge.SchoolAssessmentDate != facts.AssessmentDate ||
			charge.SchoolDueDate != facts.DueDate || request.SchoolDueDate != facts.DueDate || link.Currency != facts.Currency)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		return new(facts, link, charge, request, user.Id.Value);
	}

	public static void EnsureIndependent(FinanceExternalSettlementContext context, Guid makerId)
	{
		if (context.ActorId == makerId)
			throw new BusinessException(LibraryFinanceErrorCodes.IndependentApprovalRequired,
				"A different authorized person must review this request.");
	}

	public async Task EnsureWaiverApproverAsync(FinanceExternalSettlementContext context, CollectionPolicy policy, Guid makerId)
	{
		EnsureIndependent(context, makerId);
		await EnsureWaiverReviewerRoleAsync(context, policy);
	}

	internal async Task EnsureWaiverReviewerRoleAsync(FinanceExternalSettlementContext context, CollectionPolicy policy)
	{
		var required = policy.GetApprovalRoleIds().ToArray();
		var roleIds = (await roles.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id && Enumerable.Contains(required, row.Id))
			.Select(row => row.Id);
		if (required.Length == 0 || !await queries.AnyAsync((await users.GetQueryableAsync()).Where(row =>
			row.Id == context.ActorId && row.TenantId == tenant.Id && row.IsActive && row.Roles.Any(role => roleIds.Contains(role.RoleId)))))
			throw new AbpAuthorizationException("The current account is not an eligible waiver approver.");
	}
}
