using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Mobile;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Volo.Abp.Security.Claims;
using Xunit;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	// Tenant users need the tenant claim as well as the live tenant scope for creation auditing.
	private IDisposable FinanceIdentity(Guid userId, string? role = null)
	{
		var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };
		if (role != null) claims.Add(new Claim(AbpClaimTypes.Role, role));
		if (GetRequiredService<ICurrentTenant>().Id is Guid tenantId)
			claims.Add(new Claim(AbpClaimTypes.TenantId, tenantId.ToString()));
		return GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
	}
	private async Task<T> FinanceRequestAsync<T>(Func<Task<T>> action)
	{
		using var request = GetRequiredService<IUnitOfWorkManager>().Begin(isTransactional: true, requiresNew: true);
		var result = await action();
		await request.CompleteAsync();
		return result;
	}
	private sealed class FamilyServiceProvider(IServiceProvider original, MobileFinanceAccessService access) : IServiceProvider
	{
		public object? GetService(Type type) => type == typeof(MobileFinanceAccessService) ? access : original.GetService(type);
	}
	// Permission caches publish grant changes when their UOW completes, as a real grant-management request would.
	private async Task GrantFinancePermissionAsync(Guid userId, string permission)
	{
		using var grantUnit = GetRequiredService<IUnitOfWorkManager>().Begin(isTransactional: true, requiresNew: true);
		await GetRequiredService<IPermissionManager>().SetAsync(permission, "U", userId.ToString(), true);
		await grantUnit.CompleteAsync();
		(await GetRequiredService<IPermissionManager>().GetAsync(permission, "U", userId.ToString())).IsGranted.ShouldBeTrue("Persisted fixture grant must be committed before family authorization.");
	}

	private T FamilyService<T>() where T : ApplicationService
	{
		var access = RealFinanceAccess();
		var service = typeof(T) == typeof(MobileFinanceActivityService)
			? ActivatorUtilities.CreateInstance<T>(ServiceProvider, access)
			: ActivatorUtilities.CreateInstance<T>(ServiceProvider);
		service.LazyServiceProvider = new AbpLazyServiceProvider(new FamilyServiceProvider(ServiceProvider, access));
		return service;
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public Task Guardian_and_database_administrator_guardian_submission_stays_pending_without_changing_dues(bool administrator, bool staffRoute) =>
		WithUnitOfWorkAsync(new AbpUnitOfWorkOptions { IsTransactional = false }, async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		if (administrator)
		{
			var role = await Insert(new IdentityRole(Guid.NewGuid(), "admin", tenantId));
			f.User.AddRole(role.Id);
			await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
		}
		foreach (var permission in new[] { Permissions.ViewConcessions, Permissions.RequestConcession, Permissions.EditConcession, Permissions.SubmitConcession, Permissions.CancelConcession })
			await GrantFinancePermissionAsync(f.User.Id, permission);
		var category = await Insert(new FeeCategory(Guid.NewGuid(), tenantId, f.Year.Id, "Family fees", null));
		var income = await Insert(new TransactionCategory(Guid.NewGuid(), tenantId, f.Year.Id, TransactionType.Income, "Family income", null));
		var head = await Insert(new FeeHead(Guid.NewGuid(), tenantId, f.Year.Id, category.Id, "Tuition", null, true, null, income.Id));
		var type = await Insert(new ConcessionType(Guid.NewGuid(), tenantId, f.Year.Id, "Family hardship"));
		var policyId = Guid.NewGuid();
		var snapshot = new ConcessionPolicySnapshot(policyId, f.Year.Id, "Family policy", "FAMILY", ConcessionPolicyStatus.Active, false,
			ConcessionValueSource.RequestEnteredWithinLimits, ConcessionCalculationMethod.FixedAmount, null, null, null, 500m,
			ConcessionApplyOn.AllOutstanding, RoundingMethodType.RoundUp, 1, [type.Id]);
		var version = await Insert(new FinancePolicyVersion(Guid.NewGuid(), tenantId, "concession", policyId, f.Year.Id, 1,
			"Family policy", "Active", JsonSerializer.Serialize(snapshot), "create"));
		var assignment = await Insert(new StudentFeeStructure(Guid.NewGuid(), tenantId, f.Student.Id, Guid.NewGuid(), f.Year.Id,
			policyId, version.Id, ConcessionFeeHeadScope.EntireFeeStructure, []));
		var due = new DueFee(Guid.NewGuid(), tenantId, f.Student.Id, head.Id, (ShikshaMitra.Finance.Fees.Collections.MonthEnum)(DateTime.Today.Month - 1),
			DateTime.Today.Year, false, 500m, DateTime.Today);
		due.BindAssignment(assignment.Id); await Insert(due);
		var service = FamilyService<ConcessionRequestAppService>();
		var input = new MobileConcessionInput { StudentFeeStructureId = assignment.Id, ConcessionPolicyId = policyId,
			ConcessionPolicyVersionId = version.Id, ConcessionTypeId = type.Id, IdempotencyKey = Guid.NewGuid().ToString("N"),
			DueFeeConcessions = [new() { DueFeeId = due.Id, FeeHeadId = head.Id, Amount = "200.00" }] };
		var context=await service.FamilyEligibilityAsync(f.Student.Id,f.Year.Id,assignment.Id);
  ShikshaMitra.Finance.Mobile.MobileFinanceWireContract_Tests.Write("concession-context.json",context);
  context.StudentFeeStructureId.ShouldBe(assignment.Id); context.EligibleDueFees.ShouldHaveSingleItem().DueFeeId.ShouldBe(due.Id);
  context.EligibleDueFees[0].OriginalAmount.ShouldBe("500.00");context.EligibleDueFees[0].EligibleConcessionAmount.ShouldBe("500.00");
  await Should.ThrowAsync<AbpAuthorizationException>(()=>service.FamilyEligibilityAsync(f.Student.Id,f.Year.Id,Guid.NewGuid()));
  var typedPreview=await service.FamilyPreviewAsync(f.Student.Id, f.Year.Id, input);
  ShikshaMitra.Finance.Mobile.MobileFinanceWireContract_Tests.Write("concession-preview.json",typedPreview);
  var preview = JsonSerializer.SerializeToElement(typedPreview);
		var draft = JsonSerializer.SerializeToElement(await service.FamilySaveAsync(f.Student.Id, f.Year.Id, null, input));
		ShikshaMitra.Finance.Mobile.MobileFinanceWireContract_Tests.Write("concession-detail.json",await FinanceRequestAsync(()=>service.FamilyGetAsync(f.Student.Id,f.Year.Id,draft.GetProperty("ConcessionRequestId").GetGuid())));
  var id = draft.GetProperty("ConcessionRequestId").GetGuid();
		(await FinanceRequestAsync(async () => (await GetRequiredService<IConcessionRequestRepository>().GetWithDetailsAsync(id)).CreatorId)).ShouldBe(f.User.Id,
			"A family draft must retain its authenticated tenant creator before ownership-protected submission.");
		var submit = new MobileFinanceRequestAction { IdempotencyKey = Guid.NewGuid().ToString("N"),
			ConcurrencyStamp = draft.GetProperty("ConcurrencyStamp").GetString()!, PreviewFingerprint = preview.GetProperty("PreviewFingerprint").GetString() };
		await Should.ThrowAsync<BusinessException>(() => service.FamilyActionAsync(f.Student.Id, f.Year.Id, id,
			new MobileFinanceRequestAction { IdempotencyKey = Guid.NewGuid().ToString("N"), ConcurrencyStamp = submit.ConcurrencyStamp, PreviewFingerprint = "stale" }, true));
		if (staffRoute)
			await FinanceRequestAsync(() => service.SubmitAsync(id, new SubmitConcessionRequestDto { IdempotencyKey = submit.IdempotencyKey,
				ConcurrencyStamp = submit.ConcurrencyStamp, PreviewFingerprint = submit.PreviewFingerprint }));
		var result = JsonSerializer.SerializeToElement(staffRoute
			? await FinanceRequestAsync(() => service.FamilyGetAsync(f.Student.Id, f.Year.Id, id))
			: await service.FamilyActionAsync(f.Student.Id, f.Year.Id, id, submit, true));
		result.GetProperty("Status").GetString().ShouldBe("Pending");
		if (!staffRoute)
		{
			var replay = JsonSerializer.SerializeToElement(await service.FamilyActionAsync(f.Student.Id, f.Year.Id, id, submit, true));
			replay.GetProperty("ConcessionRequestId").GetGuid().ShouldBe(id);
		}
		var request = await GetRequiredService<IConcessionRequestRepository>().GetWithDetailsAsync(id);
		request.Status.ShouldBe(ConcessionRequestStatus.Pending); request.GetTotalApprovedConcession().ShouldBe(0m);
  var history=await RealFinanceReads().RequestsAsync(f.Student.Id,f.Year.Id,"concession");
  var listed=history.Items.ShouldHaveSingleItem().ShouldBeOfType<MobileConcessionRequest>();
  listed.RequestedConcessionAmount.ShouldBe("200.00");listed.ApprovedConcessionAmount.ShouldBe("0.00");listed.ConcessionTypeName.ShouldBe(type.Name);
  ShikshaMitra.Finance.Mobile.MobileFinanceWireContract_Tests.Write("concessions-persisted.json",history);
  (await RealFinanceReads().RequestsAsync(f.Student.Id,f.Year.Id,"concession",skipCount:1,maxResultCount:1)).Items.ShouldBeEmpty();
		(await GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(due.Id)).OutstandingAmount.ShouldBe(500m);
		var other = new IdentityUser(Guid.NewGuid(), "second-requestor", "second-requestor@example.test", tenantId);
		other.SetEmailConfirmed(true); await Insert(other);
		// The persisted portal schema permits one active guardian account per child.
		await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
		await Insert(new StudentPortalAccountLink(Guid.NewGuid(), tenantId, f.Student.Id, other.Id, "guardian", "father", Guid.NewGuid()));
		await GrantFinancePermissionAsync(other.Id, Permissions.RequestConcession);
		await GrantFinancePermissionAsync(other.Id, Permissions.ViewConcessions);
		using var otherIdentity = FinanceIdentity(other.Id);
		(await RealFinanceAccess().RequireAsync(f.Student.Id, f.Year.Id, true, Permissions.RequestConcession)).UserId.ShouldBe(other.Id);
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.FamilyGetAsync(f.Student.Id, f.Year.Id, id));
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.FamilyDownloadAsync(f.Student.Id, f.Year.Id, id, Guid.NewGuid()));
	});

	[Theory]
	[InlineData(false, true, false)]
	[InlineData(true, false, false)]
	[InlineData(false, true, true)]
	[InlineData(true, false, true)]
	public Task Every_parent_command_requires_guardian_and_exact_grant_before_body_or_resource_validation(bool guardian, bool grant, bool administrator) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, guardian); using var identity = FinanceIdentity(f.User.Id);
		if (administrator)
		{
			var role = await Insert(new IdentityRole(Guid.NewGuid(), "admin", tenantId));
			f.User.AddRole(role.Id); await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(f.User, true);
		}
		if (grant)
			foreach (var permission in new[] { Permissions.RequestConcession, Permissions.RequestRefund, Permissions.RequestAdjustment })
				await GrantFinancePermissionAsync(f.User.Id, permission);
		var concession = FamilyService<ConcessionRequestAppService>();
		var refund = FamilyService<FeeRefundAppService>();
		var adjustment = FamilyService<FeeAdjustmentAppService>();
		var id = Guid.NewGuid();
		var commands = new List<Func<Task>> {
			() => concession.FamilyEligibilityAsync(f.Student.Id, f.Year.Id),
			() => concession.FamilyPreviewAsync(f.Student.Id, f.Year.Id, new()),
			() => concession.FamilyGetAsync(f.Student.Id, f.Year.Id, id),
			() => concession.FamilySaveAsync(f.Student.Id, f.Year.Id, null, new()),
			() => concession.FamilySaveAsync(f.Student.Id, f.Year.Id, id, new()),
			() => concession.FamilyActionAsync(f.Student.Id, f.Year.Id, id, new(), true),
			() => concession.FamilyActionAsync(f.Student.Id, f.Year.Id, id, new(), false),
			() => concession.FamilyUploadAsync(f.Student.Id, f.Year.Id, id, id, new UploadConcessionDocumentFileDto()),
			() => concession.FamilyDownloadAsync(f.Student.Id, f.Year.Id, id, id),
			() => refund.FamilyRefundEligibilityAsync(f.Student.Id, f.Year.Id),
			() => refund.FamilyRefundPreviewAsync(f.Student.Id, f.Year.Id, new()),
			() => refund.FamilyRefundCreateAsync(f.Student.Id, f.Year.Id, new()),
			() => adjustment.FamilyAdjustmentEligibilityAsync(f.Student.Id, f.Year.Id),
			() => adjustment.FamilyAdjustmentPreviewAsync(f.Student.Id, f.Year.Id, new()),
			() => adjustment.FamilyAdjustmentCreateAsync(f.Student.Id, f.Year.Id, new()) };
		foreach (var command in commands) await Should.ThrowAsync<AbpAuthorizationException>(command);
	});

	[Fact]
	public Task Parent_request_permission_never_allows_an_unrelated_child() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		foreach (var permission in new[] { Permissions.RequestConcession, Permissions.RequestRefund, Permissions.RequestAdjustment })
			await GrantFinancePermissionAsync(f.User.Id, permission);
		await Should.ThrowAsync<AbpAuthorizationException>(() => FamilyService<ConcessionRequestAppService>().FamilySaveAsync(Guid.NewGuid(), f.Year.Id, null, new()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => FamilyService<FeeRefundAppService>().FamilyRefundCreateAsync(Guid.NewGuid(), f.Year.Id, new()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => FamilyService<FeeAdjustmentAppService>().FamilyAdjustmentCreateAsync(Guid.NewGuid(), f.Year.Id, new()));
	});

	[Fact]
	public Task Durable_receipt_replays_same_command_and_rejects_changed_child_or_beneficiary() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		var store = GetRequiredService<MobileFinanceCommandStore>(); var request = Guid.NewGuid(); var key = Guid.NewGuid().ToString("N");
		var payload = new { child = f.Student.Id, beneficiary = "1234" };
		(await store.ReplayAsync("refund_create", key, payload)).ShouldBeNull();
		await store.RecordAsync("refund_create", key, payload, request, f.Student.Id, f.Year.Id);
		await GetRequiredService<IUnitOfWorkManager>().Current!.SaveChangesAsync();
		(await store.ReplayAsync("refund_create", key, payload)).ShouldBe(request);
		await Should.ThrowAsync<BusinessException>(() => store.ReplayAsync("refund_create", key, new { child = f.Student.Id, beneficiary = "5678" }));
		await Should.ThrowAsync<BusinessException>(() => store.ReplayAsync("refund_create", key, new { child = Guid.NewGuid(), beneficiary = "1234" }));
	});

	[FamilyFinancePostgresFact]
	public Task Competing_command_receipt_rolls_back_the_losing_transaction_and_its_side_effects() =>
		WithUnitOfWorkAsync(new AbpUnitOfWorkOptions { IsTransactional = false }, async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		var store = GetRequiredService<MobileFinanceCommandStore>(); var key = Guid.NewGuid().ToString("N");
		var payload = new { child = f.Student.Id, amount = "100.00" }; var winningRequest = Guid.NewGuid();
		await store.RecordAsync("adjustment_create", key, payload, winningRequest, f.Student.Id, f.Year.Id);
		await GetRequiredService<IUnitOfWorkManager>().Current!.SaveChangesAsync();
		var losingSideEffectId = Guid.NewGuid();
		using (var transaction = GetRequiredService<IUnitOfWorkManager>().Begin(isTransactional: true, requiresNew: true))
		{
			await GetRequiredService<IRepository<FinanceAuditEvent, Guid>>().InsertAsync(new FinanceAuditEvent(losingSideEffectId,
				tenantId, f.Year.Id, "family", "family_test_effect", Guid.NewGuid(), string.Empty, "create", f.Student.Id,
				null, null, null, f.User.Id, null, null, DateTime.UtcNow, "Losing command effect."), autoSave: true);
			// A concurrent request that observed no receipt before the winner committed reaches this insert.
			await store.RecordAsync("adjustment_create", key, payload, Guid.NewGuid(), f.Student.Id, f.Year.Id);
			await Should.ThrowAsync<DbUpdateException>(() => transaction.SaveChangesAsync());
		}
		(await GetRequiredService<IRepository<FinanceAuditEvent, Guid>>().FindAsync(losingSideEffectId)).ShouldBeNull();
		(await store.ReplayAsync("adjustment_create", key, payload)).ShouldBe(winningRequest);
	});

	[Fact]
	public Task Activity_omits_foreign_drafts_and_private_payloads_and_keeps_per_user_read_state() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
		await GrantFinancePermissionAsync(f.User.Id, Permissions.View);
		FinanceAuditEvent Audit(string action, Guid actor) => new(Guid.NewGuid(), tenantId, f.Year.Id, "concession", "concession_request",
			Guid.NewGuid(), "secret filename.pdf", action, f.Student.Id, null, null, null, actor, null, null,
			DateTime.UtcNow, "secret beneficiary 1234567890", contextJson: "secret evidence");
		var staff = Guid.NewGuid(); var draft = await Insert(Audit("create", staff)); var approved = await Insert(Audit("approve", staff));
		var service = FamilyService<MobileFinanceActivityService>();
		(await service.GetAsync(f.Student.Id, f.Year.Id)).Items.ShouldBeEmpty("Concession events need Concessions View.");
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.MarkReadAsync(f.Student.Id, f.Year.Id, approved.Id));
		await GrantFinancePermissionAsync(f.User.Id, Permissions.ViewConcessions);
		var before = await service.GetAsync(f.Student.Id, f.Year.Id);
		before.Items.ShouldHaveSingleItem().Id.ShouldBe(approved.Id);
		JsonSerializer.Serialize(before).ShouldNotContain("secret");
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.MarkReadAsync(f.Student.Id, f.Year.Id, draft.Id));
		await service.MarkReadAsync(f.Student.Id, f.Year.Id, approved.Id);
		await service.MarkReadAsync(f.Student.Id, f.Year.Id, approved.Id);
		(await service.GetAsync(f.Student.Id, f.Year.Id)).Items.ShouldHaveSingleItem().IsRead.ShouldBeTrue();
		(await service.GetAsync(f.Student.Id, f.Year.Id, unreadOnly: true)).Items.ShouldBeEmpty();
		var second = new IdentityUser(Guid.NewGuid(), "other-guardian", "other@example.test", tenantId);
		second.SetEmailConfirmed(true); await Insert(second);
		await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
		await Insert(new StudentPortalAccountLink(Guid.NewGuid(), tenantId, f.Student.Id, second.Id, "guardian", "father", Guid.NewGuid()));
		await GrantFinancePermissionAsync(second.Id, Permissions.View);
		await GrantFinancePermissionAsync(second.Id, Permissions.ViewConcessions);
		using var secondIdentity = FinanceIdentity(second.Id);
		(await FamilyService<MobileFinanceActivityService>().GetAsync(f.Student.Id, f.Year.Id)).Items.ShouldHaveSingleItem().IsRead.ShouldBeFalse();
	});
}
