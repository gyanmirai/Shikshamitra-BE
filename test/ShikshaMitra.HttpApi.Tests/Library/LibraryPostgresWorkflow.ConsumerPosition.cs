using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Charges.Positions;
using ShikshaMitra.Security.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private Task AssertFinancePgConsumerAsync(LibraryPostgresApiTestFactory factory, FinancePgPrepared prepared,
		decimal settled, decimal pending, decimal refunded) => InFinancePgAsync(factory, async services =>
	{
		using var principal = ConsumerPgPrincipal(services);
		var reader = await ConsumerPgReaderAsync(services);
		var db = await FinancePgDbAsync(services);
		db.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
		await db.Database.ExecuteSqlRawAsync("SET LOCAL DateStyle = 'SQL, DMY'");
		var input = new LibraryChargePositionReadInput(LibraryChargePositionReadPurpose.ChargeHistory,
			factory.YearId, [prepared.Fixture.Source.SourceId]);
		using var activity = new Activity("Native canonical consumer").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var capture = new FinanceConsumerPgSqlCapture(activity.TraceId);
		var query = await reader.GetQueryAsync(input);
		capture.Commands.ShouldBeEmpty("Constructing a consumer query must not execute source/Finance reads.");
		(await query.CountAsync()).ShouldBe(1);
		var verified = query.Where(row => row.Evidence == FinanceExternalPositionEvidence.Verified);
		(await verified.CountAsync()).ShouldBe(1);
		(await verified.SumAsync(row => row.Original)).ShouldBe(100.75m);
		var row = await query.OrderBy(row => row.SourceId).Take(1).SingleAsync();
		(await query.OrderBy(row => row.SourceId).Skip(1).Take(1).ToListAsync()).ShouldBeEmpty();
		capture.Commands.Count.ShouldBe(5);
		row.SourceId.ShouldBe(prepared.Fixture.Source.SourceId);
		row.SourceLinkId.ShouldBe(prepared.LinkId);
		row.AcademicYearId.ShouldBe(factory.YearId);
		row.Currency.ShouldBe("INR");
		row.Accepted.ShouldBe(settled + pending); row.Settled.ShouldBe(settled); row.Pending.ShouldBe(pending);
		row.NetWaived.ShouldBe(0m); row.Refunded.ShouldBe(refunded);
		row.FinancialOutstanding.ShouldBe(100.75m - settled);
		row.Collectible.ShouldBe(100.75m - settled - pending);
		var detail = (await reader.ReadAsync(input)).ShouldHaveSingleItem();
		detail.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		detail.Amounts.ShouldBe(new FinanceExternalPositionAmounts(100.75m, settled + pending,
			settled, pending, 0m, 100.75m - settled, 100.75m - settled - pending, refunded));
		var charge = await db.Set<Charge>().SingleAsync(value => value.Id == prepared.Posted.FinanceChargeId);
		var canonical = (await services.GetRequiredService<FinanceExternalChargeBalanceReader>().ReadAsync(charge)).Amounts;
		row.Original.ShouldBe(canonical.Original); row.Accepted.ShouldBe(canonical.Accepted);
		row.Settled.ShouldBe(canonical.Settled); row.Pending.ShouldBe(canonical.PendingVerification);
		row.NetWaived.ShouldBe(canonical.NetWaived); row.Collectible.ShouldBe(canonical.Collectible);
		row.FinancialOutstanding.ShouldBe(canonical.Unsettled);
		await ExplainConsumerPgAsync(db, capture.Commands.ToArray(), $"settled-{settled}-pending-{pending}-refunded-{refunded}");
		return 0;
	});

	private static IDisposable ConsumerPgPrincipal(IServiceProvider services) =>
		services.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
		[
			new(AbpClaimTypes.UserId, FinancePgMakerId.ToString("D")),
			new(AbpClaimTypes.TenantId, FinancePgTenantId.ToString("D")),
			new(AbpClaimTypes.Role, FinancePgMakerRole)
		], "Native persisted consumer operator")));

	private static async Task<LibraryChargePositionReader> ConsumerPgReaderAsync(IServiceProvider services)
	{
		// This purpose read is checked by the actual database checker, not the
		// positive HTTP fixture's header-based checker or a Finance write grant.
		await services.GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Library.Charges.View, "R", FinancePgMakerRole, true);
		await services.GetRequiredService<IUnitOfWorkManager>().Current!.SaveChangesAsync();
		var checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(services);
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
		(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)).ShouldBeFalse();
		return ActivatorUtilities.CreateInstance<LibraryChargePositionReader>(services, checker);
	}
}
