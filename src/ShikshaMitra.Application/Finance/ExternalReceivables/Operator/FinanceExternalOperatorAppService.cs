using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.ExternalReceivables.Operator;

[Authorize, RemoteService(false)]
public partial class FinanceExternalOperatorAppService(FinanceExternalSettlementAuthority authority, IPermissionChecker permissions,
	IFinanceExternalReceivableSourceReader sourceReader, FinanceScopeAccessService scope,
	FinanceExternalReceivableProjection projection, FinanceExternalRevisionReader revisions,
	IRepository<FinanceExternalSettlementPolicy, Guid> policies)
	: ApplicationService, IFinanceExternalOperatorAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	public Task<PagedResultDto<FinanceExternalOperatorOptionDto>> GetPostingOptionsAsync(FinanceExternalSourceReference source,
		FinanceExternalOperatorOptionKind kind, int page = 1, int pageSize = 20, string? search = null) =>
		PostingOptionsAsync(source, kind, page, pageSize, search);

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	public Task<PagedResultDto<FinanceExternalOperatorOptionDto>> GetBindingOptionsAsync(FinanceExternalSourceReference source,
		FinanceExternalOperatorOptionKind kind, int page = 1, int pageSize = 20, string? search = null) =>
		BindingOptionsAsync(source, kind, page, pageSize, search);

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	public Task<FinanceExternalOperatorContextDto> GetBindingContextAsync(FinanceExternalSourceReference source) =>
		ContextAsync(source, approval: false);

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)]
	public Task<FinanceExternalOperatorContextDto> GetApprovalContextAsync(FinanceExternalSourceReference source) =>
		ContextAsync(source, approval: true);

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)]
	public Task<PagedResultDto<FinanceExternalOperatorHistoryDto>> GetHistoryAsync(FinanceExternalSourceReference source,
		int page = 1, int pageSize = 20) => HistoryAsync(source, page, pageSize);

	private async Task RequireActionAsync(string action)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue || !await permissions.IsGrantedAsync(action))
			throw new AbpAuthorizationException("Finance source operator access denied.");
	}

	private async Task<FinanceExternalReceivableSourceFacts> PostingSourceAsync(FinanceExternalSourceReference source)
	{
		await RequireActionAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add);
		FinanceExternalReceivableSourceGuard.ValidateReference(source);
		var facts = await sourceReader.ReadAuthorizedAsync(source);
		FinanceExternalReceivableSourceGuard.ValidateFacts(source, facts, CurrentTenant.Id);
		await scope.EnsureCollectionYearAccessibleAsync(facts.AcademicYearId);
		return facts;
	}

	private static long Offset(int page, int pageSize, string? search = null)
	{
		if (page < 1 || pageSize is < 1 or > 100 || search?.Length > 200)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource, "Choose a valid page and search.");
		return ((long)page - 1) * pageSize;
	}
}
