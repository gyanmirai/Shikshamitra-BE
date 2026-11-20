using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.ChargeCategory.Dtos;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<TypedSettlement> SeedReasonParitySourceAsync(string? remarks)
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee, prepareSource: owner => WithUnitOfWorkAsync(async () =>
		{
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var source = await charges.GetAsync(owner.AssessmentId);
			source.Remarks = remarks;
			await charges.UpdateAsync(source, true);
		}));
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		return context;
	}

	private async Task RenameReasonParityCategoryAsync(TypedSettlement context)
	{
		var owner = context.Owner;
		var ownActions = new[] { ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.View,
			ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.Add };
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var action in ownActions)
				await GetRequiredService<IPermissionManager>().SetAsync(action, "R", $"external-finance-{owner.ActorId:N}", true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
			var checker = services.GetRequiredService<IPermissionChecker>();
			var authorization = services.GetRequiredService<IAuthorizationService>();
			foreach (var action in ownActions)
			{
				(await checker.IsGrantedAsync(action)).ShouldBeTrue(action);
				await authorization.CheckAsync(action);
			}
			var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
			var category = await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().GetAsync(source.ChargeCategoryId);
			var service = ActivatorUtilities.CreateInstance<LibraryChargeCategoryAppService>(services);
			service.LazyServiceProvider = new AbpLazyServiceProvider(services);
			// Current Update is annotated with Add, not a guessed Update grant.
			// Explicit real-checker checks above replace no attribute test and do
			// not claim direct construction invokes the ABP proxy interceptor.
			await service.UpdateAsync(category.Id, new LibraryChargeCategoryUpdateRequest
			{
				ChargeType = "Updated current damage label", Amount = category.Amount,
				IsFixed = category.IsFixed, Description = category.Description
			});
		});
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var action in ownActions)
				await GetRequiredService<IPermissionManager>().SetAsync(action, "R", $"external-finance-{owner.ActorId:N}", false);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var checker = new FinanceServices(ServiceProvider, owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
			foreach (var action in ownActions) (await checker.IsGrantedAsync(action)).ShouldBeFalse(action);
		});
	}

	private Task<string> ReadReasonParityCategoryNameAsync(TypedSettlement context) => WithUnitOfWorkAsync(async () =>
	{
		var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(context.Owner.AssessmentId);
		return (await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().GetAsync(source.ChargeCategoryId)).ChargeType;
	});

	private Task<FinanceExternalReceivableSourceFacts> ReadReasonParityFactsAsync(TypedSettlement context) => WithUnitOfWorkAsync(() =>
		new FinanceServices(ServiceProvider, context.Owner, useStoredSource: true)
			.GetRequiredService<IFinanceExternalReceivableSourceReader>().ReadAuthorizedAsync(context.Post.Source));

	private Task<ReasonParityEvidence> CaptureReasonParityEvidenceAsync(TypedSettlement context) => WithUnitOfWorkAsync(async () =>
	{
		var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
			.GetListAsync(row => row.TenantId == context.Owner.TenantId && row.SourceId == context.Owner.AssessmentId)).Single();
		var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(context.Owner.AssessmentId);
		return new ReasonParityEvidence(link.Id, link.SourceVersion, link.SourceFingerprint, link.SourceSnapshotJson,
			link.FinanceChargeRequestId, link.FinanceChargeId, link.AcademicYearId, source.Remarks,
			source.Amount, source.SchoolChargeDate, source.SchoolDueDate);
	});

	private sealed record ReasonParityEvidence(Guid Id, string Version, string Fingerprint, string Snapshot,
		Guid ChargeRequestId, Guid ChargeId, Guid AcademicYearId, string? RawRemarks,
		decimal AssessmentAmount, DateOnly? AssessmentDate, DateOnly? DueDate);
}
