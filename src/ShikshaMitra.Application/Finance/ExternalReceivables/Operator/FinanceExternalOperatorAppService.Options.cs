using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.ExternalReceivables.Operator;

public partial class FinanceExternalOperatorAppService
{
	private async Task<PagedResultDto<FinanceExternalOperatorOptionDto>> PostingOptionsAsync(FinanceExternalSourceReference source,
		FinanceExternalOperatorOptionKind kind, int page, int pageSize, string? search)
	{
		var facts = await PostingSourceAsync(source);
		return await OptionsAsync(facts, kind, page, pageSize, search);
	}

	private async Task<PagedResultDto<FinanceExternalOperatorOptionDto>> BindingOptionsAsync(FinanceExternalSourceReference source,
		FinanceExternalOperatorOptionKind kind, int page, int pageSize, string? search)
	{
		var context = await authority.RequireAsync(source, ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update);
		if (kind == FinanceExternalOperatorOptionKind.ChargeCategory)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidFinanceMapping, "The posted charge category cannot be changed by policy binding.");
		var existing = await policies.FindAsync(row => row.TenantId == CurrentTenant.Id && row.SourceLinkId == context.Link.Id);
		return await OptionsAsync(context.Source, kind, page, pageSize, search,
			kind == FinanceExternalOperatorOptionKind.CollectionPolicyVersion
				? context.Link.CollectionPolicyVersionId : context.Link.RefundPolicyVersionId,
			existing != null);
	}

	private async Task<PagedResultDto<FinanceExternalOperatorOptionDto>> OptionsAsync(FinanceExternalReceivableSourceFacts facts,
		FinanceExternalOperatorOptionKind kind, int page, int pageSize, string? search, Guid? requiredVersionId = null, bool bound = false)
	{
		var offset = Offset(page, pageSize, search);
		if (!Enum.IsDefined(kind)) throw new BusinessException(LibraryFinanceErrorCodes.InvalidFinancePolicy);
		var text = search?.Trim().ToLowerInvariant();
		if (kind == FinanceExternalOperatorOptionKind.ChargeCategory)
		{
			var query = (await LazyServiceProvider.LazyGetRequiredService<IRepository<ChargeCategory, Guid>>().GetQueryableAsync())
				.Where(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == facts.AcademicYearId && row.IsActive);
			if (!string.IsNullOrEmpty(text)) query = query.Where(row => row.Name.ToLower().Contains(text));
			var total = await AsyncExecuter.CountAsync(query);
			if (offset >= total) return new(total, []);
			var rows = await AsyncExecuter.ToListAsync(query.OrderBy(row => row.Name).ThenBy(row => row.Id).Skip((int)offset).Take(pageSize));
			var incomeIds = rows.Where(row => row.IncomeCategoryId.HasValue).Select(row => row.IncomeCategoryId!.Value).Distinct().ToArray();
			var incomes = await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<TransactionCategory, Guid>>()
				.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == facts.AcademicYearId &&
					row.CategoryType == TransactionType.Income && row.IsActive && Enumerable.Contains(incomeIds, row.Id))
				.Select(row => new { row.Id, row.EffectiveFrom, row.EffectiveTo }));
			var now = Clock.Now;
			var effectiveIncomeIds = incomes.Where(row => !(row.EffectiveFrom > now || row.EffectiveTo < now))
				.Select(row => row.Id).ToHashSet();
			return new(total, rows.Select(row => new FinanceExternalOperatorOptionDto
			{
				Id = row.Id, Name = row.Name, IsSelectable = row.IncomeCategoryId.HasValue && effectiveIncomeIds.Contains(row.IncomeCategoryId.Value),
				BlockedReason = row.IncomeCategoryId.HasValue && effectiveIncomeIds.Contains(row.IncomeCategoryId.Value)
					? null : "Configure an active source-year income mapping valid for this Finance action before posting this category."
			}).ToList());
		}
		var type = kind == FinanceExternalOperatorOptionKind.CollectionPolicyVersion ? "collection" : "refund";
		var versions = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FinancePolicyVersion, Guid>>().GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == facts.AcademicYearId &&
				row.PolicyType == type && string.Compare(row.Status.ToLower(), "active") == 0);
		if (requiredVersionId.HasValue) versions = versions.Where(row => row.Id == requiredVersionId.Value);
		if (!string.IsNullOrEmpty(text)) versions = versions.Where(row => row.PolicyName.ToLower().Contains(text));
		var versionTotal = await AsyncExecuter.CountAsync(versions);
		if (offset >= versionTotal) return new(versionTotal, []);
		var pageRows = await AsyncExecuter.ToListAsync(versions.OrderBy(row => row.PolicyName).ThenBy(row => row.VersionNumber)
			.ThenBy(row => row.Id).Skip((int)offset).Take(pageSize));
		return new(versionTotal, pageRows.Select(version => PolicyOption(version, facts.Payer.Kind, bound)).ToList());
	}

	private static FinanceExternalOperatorOptionDto PolicyOption(FinancePolicyVersion version, FinanceExternalPayerKind payer, bool bound)
	{
		string? issue = bound ? "The settlement policy is already recorded and cannot be overwritten." : null;
		try
		{
			if (version.PolicyType == "refund") FinanceExternalSettlementPolicyRules.EnsureRefundCreationCompatible(version, payer);
			else
			{
				var snapshot = CollectionPolicySnapshot.Deserialize(version.SnapshotJson);
				if (snapshot.Status != CollectionPolicyStatus.Active) throw new BusinessException(LibraryFinanceErrorCodes.InvalidFinancePolicy);
				_ = CollectionPolicy.FromSnapshot(version.PolicyId, version.TenantId, version.AcademicYearId, version.VersionNumber, snapshot);
			}
		}
		catch (Exception error) when (error is JsonException or BusinessException)
		{
			issue = "This recorded policy version is not compatible with this source; select a valid version.";
		}
		return new() { Id = version.Id, Name = version.PolicyName, PolicyId = version.PolicyId, VersionNumber = version.VersionNumber,
			IsSelectable = issue == null, BlockedReason = issue };
	}
}
