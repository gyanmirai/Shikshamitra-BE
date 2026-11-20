using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
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
	private async Task<LegacyOriginPost> SeedLegacyOriginPostAsync()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: true);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var source = await charges.GetAsync(owner.AssessmentId);
			source.Remarks = null;
			await charges.UpdateAsync(source, true);
		});
		owner = await ConfirmStoredAssessmentAsync(owner);
		var post = Post(owner);
		var services = new FinanceServices(ServiceProvider, owner, useStoredSource: true);
		var authorized = services.GetRequiredService<IFinanceExternalReceivableSourceReader>();
		var legacy = new LegacyOriginProducerReader(authorized);
		var service = ActivatorUtilities.CreateInstance<FinanceExternalReceivableAppService>(services, legacy);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		var posted = await WithUnitOfWorkAsync(() => service.PostAsync(post));
		return new(owner, post, posted);
	}

	private Task<FinanceExternalReceivableSourceLink> ReadLegacyOriginLinkAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
		(await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
			.GetListAsync(row => row.TenantId == owner.TenantId && row.SourceId == owner.AssessmentId)).Single());

	private async Task RenameLegacyOriginCategoryAsync(Fixture owner)
	{
		var actions = new[] { ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.View,
			ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.Add };
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var action in actions)
				await GetRequiredService<IPermissionManager>().SetAsync(action, "R", $"external-finance-{owner.ActorId:N}", true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
			foreach (var action in actions)
			{
				(await services.GetRequiredService<IPermissionChecker>().IsGrantedAsync(action)).ShouldBeTrue(action);
				await services.GetRequiredService<IAuthorizationService>().CheckAsync(action);
			}
			var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
			var category = await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().GetAsync(source.ChargeCategoryId);
			var service = ActivatorUtilities.CreateInstance<LibraryChargeCategoryAppService>(services);
			service.LazyServiceProvider = new AbpLazyServiceProvider(services);
			await service.UpdateAsync(category.Id, new LibraryChargeCategoryUpdateRequest
			{
				ChargeType = "Changed legacy category label", Amount = category.Amount,
				IsFixed = category.IsFixed, Description = category.Description
			});
		});
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var action in actions)
				await GetRequiredService<IPermissionManager>().SetAsync(action, "R", $"external-finance-{owner.ActorId:N}", false);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var checker = new FinanceServices(ServiceProvider, owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
			foreach (var action in actions) (await checker.IsGrantedAsync(action)).ShouldBeFalse(action);
		});
	}

	// A bounded old producer still delegates all real current source/tenant/year/
	// payer/permission facts; it reconstructs only the established v1 fields.
	// Future optional origin must remain omitted on null during serialization.
	private sealed class LegacyOriginProducerReader(IFinanceExternalReceivableSourceReader authorized)
		: IFinanceExternalReceivableSourceReader
	{
		public async Task<FinanceExternalReceivableSourceFacts> ReadAuthorizedAsync(FinanceExternalSourceReference source)
		{
			var facts = await authorized.ReadAuthorizedAsync(source);
			return new(facts.Source, facts.TenantId, facts.AcademicYearId, facts.Payer, facts.Amount,
				facts.Currency, facts.AssessmentDate, facts.DueDate, facts.SchoolDatesConfirmed,
				facts.Reason, facts.EvidenceReference)
			{
				AcademicYearConfirmed = facts.AcademicYearConfirmed,
				AcademicYearEvidenceJson = facts.AcademicYearEvidenceJson
			};
		}
	}

	private sealed record LegacyOriginPost(Fixture Owner, PostFinanceExternalReceivableInput Post,
		FinanceExternalReceivableDto Posted);
}
