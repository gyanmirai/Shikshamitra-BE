using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Library;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.ExternalReceivables;

/// <summary>Current action and source authority for requests in the existing Finance refund workflow.</summary>
public class FinanceExternalRefundRequestAccess(FinanceExternalSettlementAuthority authority,
	IFinanceExternalSourceScope sourceScope, FinanceScopeAccessService financeScope,
	IRepository<FinanceExternalReceivableSourceLink, Guid> links, IRepository<Charge, Guid> charges,
	IFeeRefundRepository refunds, IRepository<AcademicYear, Guid> years, IRepository<IdentityUser, Guid> identities,
	IUserScopeAccessService scopeAccess, IPermissionChecker permissions, ICurrentTenant tenant, ICurrentUser user)
	: ITransientDependency
{
	public async Task<FinanceExternalSettlementContext> RequireAsync(FeeRefundRequest request, string action)
	{
		await EnsureActorAsync(action);
		if (request.TenantId != tenant.Id || !request.FinanceExternalSourceLinkId.HasValue)
			throw new AbpAuthorizationException("Refund source access denied.");
		await financeScope.EnsureCollectionYearAccessibleAsync(request.AcademicYearId);
		var link = await links.FindAsync(request.FinanceExternalSourceLinkId.Value, includeDetails: false);
		if (link == null || link.TenantId != tenant.Id)
			throw new AbpAuthorizationException("Refund source access denied.");
		var context = await authority.RequireAsync(new()
		{
			Kind = link.SourceKind, SourceId = link.SourceId, SourceVersion = link.SourceVersion
		}, action);
		if (request.WorkflowVersion != 1 || context.Link.Id != request.FinanceExternalSourceLinkId ||
			request.AcademicYearId != context.Link.AcademicYearId || request.StudentId != context.Source.Payer.StudentId ||
			request.EmployeeId != context.Source.Payer.EmployeeId || request.StudentFeeStructureId.HasValue ||
			request.FinanceFeeAccountId.HasValue || request.Currency != context.Link.Currency ||
			request.Lines.Any(line => line.ChargeId != context.Charge.Id || line.DueFeeId.HasValue || line.LateFeeDueFeeId.HasValue))
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		return context;
	}

	public async Task<IQueryable<Guid>> GetReadableRequestIdsAsync()
	{
		await EnsureActorAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View);
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		var allowedYearIds = scope.AcademicYearIds.Concat(scope.WildcardBoardAcademicYearIds).Distinct().ToArray();
		var allYears = scope.IsAdmin || scope.HasWildcardAcademicYear;
		var allowedYears = (await years.GetQueryableAsync()).Where(year => year.TenantId == tenant.Id &&
			(allYears || Enumerable.Contains(allowedYearIds, year.Id)));
		var readableSources = await sourceScope.GetReadableSourcesAsync(FinanceExternalSourceKind.LibraryAssessment);
		var sourceLinks = (await links.GetQueryableAsync()).Where(link => link.TenantId == tenant.Id &&
			link.SourceKind == FinanceExternalSourceKind.LibraryAssessment);
		var financeCharges = (await charges.GetQueryableAsync()).Where(charge => charge.TenantId == tenant.Id);
		var requests = (await refunds.GetQueryableAsync()).Where(request => request.TenantId == tenant.Id &&
			request.WorkflowVersion == 1 && request.FinanceExternalSourceLinkId.HasValue &&
			!request.StudentFeeStructureId.HasValue && !request.FinanceFeeAccountId.HasValue);
		return from request in requests
			join link in sourceLinks on request.FinanceExternalSourceLinkId equals (Guid?)link.Id
			join source in readableSources on link.SourceId equals source.SourceId
			join charge in financeCharges on link.FinanceChargeId equals charge.Id
			join year in allowedYears on request.AcademicYearId equals year.Id
			where source.AcademicYearId == request.AcademicYearId && link.AcademicYearId == request.AcademicYearId &&
				charge.AcademicYearId == request.AcademicYearId && charge.ChargeRequestId == link.FinanceChargeRequestId &&
				source.StudentId == request.StudentId && source.EmployeeId == request.EmployeeId &&
				charge.StudentId == request.StudentId && charge.EmployeeId == request.EmployeeId && request.Currency == link.Currency
			select request.Id;
	}

	private async Task EnsureActorAsync(string action)
	{
		if (!tenant.Id.HasValue || !user.Id.HasValue || !await permissions.IsGrantedAsync(action))
			throw new AbpAuthorizationException("Finance refund action access denied.");
		var identity = await identities.FindAsync(user.Id.Value, includeDetails: false);
		if (identity == null || identity.TenantId != tenant.Id || !identity.IsActive)
			throw new AbpAuthorizationException("Finance refund identity is unavailable.");
	}
}
