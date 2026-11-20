using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Library.Charges.Positions;

[RemoteService(false)]
[ExposeServices(typeof(ILibraryChargePositionReader))]
public class LibraryChargePositionReader(IPermissionChecker permissions, ILibraryAccessService access,
	LibraryAssessmentSourceScopeQuery scopeQuery, ILibraryChargePositionSourceQuery sources,
	IFinanceExternalPositionQuery financeQuery, IFinanceExternalPositionReader financeDetails,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ICurrentUser user)
	: ILibraryChargePositionReader, ITransientDependency
{
	public async Task<IQueryable<Guid>> GetReadableSourceIdsAsync(
		LibraryChargePositionReadInput input, CancellationToken cancellationToken = default) =>
		(await ReadSourcesAsync(input, cancellationToken)).Select(source => source.SourceId);

	public async Task<IQueryable<FinanceExternalPositionQueryRow>> GetQueryAsync(
		LibraryChargePositionReadInput input, CancellationToken cancellationToken = default) =>
		await financeQuery.GetQueryAsync(await ReadSourcesAsync(input, cancellationToken), cancellationToken);

	public async Task<IReadOnlyList<FinanceExternalSourcePosition>> ReadAsync(
		LibraryChargePositionReadInput input, CancellationToken cancellationToken = default)
	{
		if (input.AssessmentIds == null || input.AssessmentIds.Count is < 1 or > 100)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource, "Select a bounded set of assessments for detail reads.");
		var query = await ReadSourcesAsync(input, cancellationToken);
		var selected = await queries.ToListAsync(query, cancellationToken);
		return await financeDetails.ReadAsync(selected, cancellationToken);
	}

	private async Task<IQueryable<FinanceExternalPositionSource>> ReadSourcesAsync(LibraryChargePositionReadInput input,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var action = input.Purpose switch
		{
			LibraryChargePositionReadPurpose.Dashboard => ShikshaMitraSubmodulePermissions.Library.Dashboard.View,
			LibraryChargePositionReadPurpose.MemberCharges => ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View,
			LibraryChargePositionReadPurpose.Reports => ShikshaMitraSubmodulePermissions.Library.Reports.View,
			LibraryChargePositionReadPurpose.ChargeHistory => ShikshaMitraSubmodulePermissions.Library.Charges.View,
			_ => throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource)
		};
		if (!tenant.Id.HasValue || !user.IsAuthenticated || !await permissions.IsGrantedAsync(action))
			throw new AbpAuthorizationException();
		var context = await access.GetContextAsync(input.AcademicYearId);
		if (context.UserScope.UserId != user.Id || context.UserScope.TenantId != tenant.Id)
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		var readable = await scopeQuery.GetQueryAsync(context.UserScope, cancellationToken);
		var raw = await sources.GetQueryAsync(cancellationToken);
		var allowedYears = context.AllowedAcademicYearIds.ToArray();
		var wide = context.UserScope.HasTenantWideScope();
		raw = raw.Where(row => row.TenantId == tenant.Id);
		if (input.AcademicYearId.HasValue)
			raw = raw.Where(row => row.AcademicYearId == input.AcademicYearId.Value);
		else if (!wide)
			raw = raw.Where(row => row.AcademicYearId.HasValue && Enumerable.Contains(allowedYears, row.AcademicYearId.Value));
		if (input.AssessmentIds != null)
		{
			var ids = input.AssessmentIds.ToArray();
			raw = raw.Where(row => Enumerable.Contains(ids, row.SourceId));
		}
		cancellationToken.ThrowIfCancellationRequested();
		return WithCanonicalPayers(raw, readable);
	}

	internal static IQueryable<FinanceExternalPositionSource> WithCanonicalPayers(IQueryable<FinanceExternalPositionSource> sources,
		IQueryable<FinanceExternalReadableSource> payers) =>
		sources.Join(payers, row => row.SourceId, allowed => allowed.SourceId, (row, allowed) => new FinanceExternalPositionSource
		{
			TenantId = row.TenantId, Kind = row.Kind, SourceId = row.SourceId, SourceVersion = row.SourceVersion,
			ExpectedSourceLinkId = row.ExpectedSourceLinkId, AcademicYearId = row.AcademicYearId,
			AcademicYearConfirmed = row.AcademicYearConfirmed, AcademicYearEvidenceJson = row.AcademicYearEvidenceJson,
			PayerKind = row.PayerKind, StudentId = allowed.StudentId, EmployeeId = allowed.EmployeeId,
			AssessmentOriginal = row.AssessmentOriginal, RecordedCurrency = row.RecordedCurrency,
			SchoolAssessmentDate = row.SchoolAssessmentDate, SchoolDueDate = row.SchoolDueDate,
			SchoolDatesConfirmed = row.SchoolDatesConfirmed, SourceDateEvidenceJson = row.SourceDateEvidenceJson,
			RawSourceCategoryId = row.RawSourceCategoryId, RawSourceRemarks = row.RawSourceRemarks,
			CurrentLegacyResolvedReason = row.CurrentLegacyResolvedReason, RecordedSettlementDeclaration = row.RecordedSettlementDeclaration
		});
}
