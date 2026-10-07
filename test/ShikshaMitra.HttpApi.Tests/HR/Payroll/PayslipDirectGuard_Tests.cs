using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayslipDirectGuardFactAttribute : FactAttribute
{
	public PayslipDirectGuardFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_PAYSLIP_DIRECT_GUARD_TEST_POSTGRES")))
			Skip = "Set PAYROLL_PAYSLIP_DIRECT_GUARD_TEST_POSTGRES to a fresh owned loopback payroll_tests_* database.";
	}
}

public sealed class PayslipDirectGuardTestFactory : PayslipPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_PAYSLIP_DIRECT_GUARD_TEST_POSTGRES";
}

[Collection(ApiTestCollection.Name)]
public class PayslipDirectGuard_Tests
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	private const string View = ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View;
	private const string Issue = ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add;
	private const string PayrollView = ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View;
	private const string Review = ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Add;
	private const string Adjust = ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Adjustments;
	private const string Lock = ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Lock;

	[PayslipDirectGuardFact]
	public async Task Unproxied_services_enforce_action_tenant_and_scope_guards_and_rollback_issued_evidence()
	{
		using var factory = new PayslipDirectGuardTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var api = workflow.Api;
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		await workflow.ConfigureAsync(true);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(api, "PayslipDirectGuard", dateOfJoining: month);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var document = (await api.GetFromJsonAsync<List<PayslipDocumentDto>>(
			$"/api/payroll/payslip-documents?employeeId={employee.Id}&monthYear={month:yyyy-MM}", Json))!.Single();

		// Both actors are new, active non-administrators in this fresh fixture only.
		var role = "payslip-direct-" + Guid.NewGuid().ToString("N");
		var (actor, outside) = await CreateActorsAsync(factory, role);
		await EsiPayrollWorkflowScenario.RequireSuccess(await api.PostAsJsonAsync("/api/app/user-scope/assign-scope",
			new CreateUserScopeAssignmentDto { UserId = actor, Description = "Fresh direct payslip guard fixture" }, Json));

		using var scope = factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var context = new DefaultHttpContext { User = Principal(actor, role) };
		var http = services.GetRequiredService<IHttpContextAccessor>();
		var priorContext = http.HttpContext;
		http.HttpContext = context;
		try
		{
			using var tenant = services.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
			using var caller = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(context.User);
			T Direct<T>() where T : ApplicationService
			{
				var instance = ActivatorUtilities.CreateInstance<T>(services);
				instance.LazyServiceProvider = services.GetRequiredService<IAbpLazyServiceProvider>();
				instance.GetType().ShouldBe(typeof(T), "An ABP/Castle proxy would hide missing explicit authorization.");
				return instance;
			}
			var documents = Direct<PayslipDocumentAppService>();
			var corrections = Direct<PayslipCorrectionAppService>();
			var deliveries = Direct<PayslipDeliveryAppService>();
			var settings = Direct<PayslipSettingsAppService>();
			var unknown = Guid.NewGuid();
			var entries = new Dictionary<string, Func<Task>>
			{
				["document history"] = async () => { await documents.GetAsync(unknown, "2026-10"); },
				["PDF"] = async () => { await documents.GetPdfAsync(unknown); },
				["payment"] = () => documents.RecordPaymentAsync(new()),
				["replacement"] = async () => { await documents.ReplaceFactsAsync(new()); },
				["correction history"] = async () => { await corrections.GetAsync(unknown); },
				["start"] = async () => { await corrections.StartAsync(new()); },
				["calculate"] = async () => { await corrections.CalculateAsync(unknown, new()); },
				["review"] = async () => { await corrections.ReviewAsync(unknown, new()); },
				["cancel"] = async () => { await corrections.CancelAsync(unknown, new()); },
				["reissue"] = async () => { await corrections.ApplyAsync(unknown, new()); },
				["settle"] = async () => { await corrections.SettleAsync(unknown, new()); },
				["delivery history"] = async () => { await deliveries.GetAsync(unknown); },
				["send"] = async () => { await deliveries.SendAsync(new()); },
				["settings read"] = async () => { await settings.GetAsync(); },
				["settings save"] = async () => { await settings.SaveAsync(new()); }
			};
			void Permissions(params string[] names) => context.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader]
				= names.Length == 0 ? "__none__" : string.Join(',', names);
			async Task DeniedAsync(IEnumerable<string> names)
			{
				foreach (var name in names)
					await Should.ThrowAsync<AbpAuthorizationException>(entries[name], name + " must fail before accessing invalid input or data.");
			}
			Permissions();
			await DeniedAsync(entries.Keys);
			Permissions(View, PayrollView);
			var mutations = entries.Keys.Except(["document history", "PDF", "correction history", "delivery history", "settings read"]).ToArray();
			await DeniedAsync(mutations);
			Permissions(View, PayrollView, Issue, Review, Lock);
			await DeniedAsync(["start", "calculate", "review", "cancel", "reissue", "settle"]);
			Permissions(View, PayrollView, Issue, Adjust, Lock);
			await DeniedAsync(["review"]);
			Permissions(View, PayrollView, Issue, Review, Adjust);
			await DeniedAsync(["reissue", "settle"]);
			Permissions(View);
			await DeniedAsync(["correction history"]);
			Permissions(View, PayrollView, Issue, Review, Adjust, Lock);

			var units = services.GetRequiredService<IUnitOfWorkManager>();
			async Task<int> QueuedJobsAsync(Guid deliveryId)
			{
				// ABP's job repository can use a different DbContext from tenant payroll.
				var jobs = await services.GetRequiredService<IBackgroundJobRepository>().GetListAsync();
				var matches = jobs.Where(job => job.JobName == typeof(PayslipDeliveryJobArgs).FullName)
					.Select(job => JsonSerializer.Deserialize<PayslipDeliveryJobArgs>(job.JobArgs, Json)!)
					.Where(args => args.DeliveryId == deliveryId).ToList();
				foreach (var args in matches) args.TenantId.ShouldBe(EsiPayrollWorkflowScenario.Tenant);
				return matches.Count;
			}
			using (var read = units.Begin(requiresNew: true, isTransactional: false))
			{
				Convert.ToHexString(SHA256.HashData(await documents.GetPdfAsync(document.Id))).ShouldBe(document.Sha256);
				document.PaymentStatus.ShouldBe("Finalized");
				(await documents.GetAsync(employee.Id, month.ToString("yyyy-MM"))).Single().Id.ShouldBe(document.Id);
				(await corrections.GetAsync(document.Id)).ShouldBeEmpty();
				(await deliveries.GetAsync(document.SalarySlipId)).ShouldBeEmpty();
			}
			var protectedReads = new Func<Task>[]
			{
				async () => { await documents.GetPdfAsync(document.Id); },
				async () => { await documents.GetAsync(employee.Id, month.ToString("yyyy-MM")); },
				async () => { await corrections.GetAsync(document.Id); },
				async () => { await deliveries.GetAsync(document.SalarySlipId); }
			};
			var protectedWrites = new Func<Task>[]
			{
				() => documents.RecordPaymentAsync(new() { DocumentId = document.Id, IdempotencyKey = Guid.NewGuid(), PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reference = "Synthetic denied payment" }),
				async () => { await documents.ReplaceFactsAsync(new() { DocumentId = document.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic denied replacement" }); },
				async () => { await corrections.StartAsync(new() { DocumentId = document.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic denied correction" }); },
				async () => { await deliveries.SendAsync(new() { DocumentId = document.Id, IdempotencyKey = Guid.NewGuid() }); }
			};
			async Task ScopeDeniedAsync(Func<Task> action)
			{
				using var transaction = units.Begin(requiresNew: true, isTransactional: true);
				(await Should.ThrowAsync<BusinessException>(action)).Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
			}
			async Task OtherTenantDeniedAsync(Func<Task> action)
			{
				using var transaction = units.Begin(requiresNew: true, isTransactional: true);
				var error = await Should.ThrowAsync<Exception>(action);
				(error is EntityNotFoundException || error is BusinessException business && business.Code == HRErrorCodes.TakeAttendanceMediumStageOutOfScope)
					.ShouldBeTrue("The owning tenant's resource must not be returned.");
			}
			// A forged admin role claim must not substitute for current database membership/scope.
			using (var unscoped = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(Principal(outside, "admin")))
			{
				foreach (var action in protectedReads.Concat(protectedWrites)) await ScopeDeniedAsync(action);
				await ScopeDeniedAsync(async () => { await settings.SaveAsync(new() { SchoolName = "Synthetic denied school", SchoolAddress = "Synthetic denied address", IdempotencyKey = Guid.NewGuid() }); });
			}
			using (var otherTenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			{
				foreach (var action in protectedReads.Concat(protectedWrites)) await OtherTenantDeniedAsync(action);
				await ScopeDeniedAsync(async () => { await settings.SaveAsync(new() { SchoolName = "Synthetic other tenant school", SchoolAddress = "Synthetic other tenant address", IdempotencyKey = Guid.NewGuid() }); });
				using var read = units.Begin(requiresNew: true, isTransactional: false);
				var isolated = await settings.GetAsync();
				isolated.SchoolAddress.ShouldBeEmpty();
				isolated.AutomaticEmail.ShouldBeFalse();
			}

			Guid rollbackDraft, rollbackDelivery;
			PayslipSettingsDto originalSettings;
			using (var read = units.Begin(requiresNew: true, isTransactional: false)) originalSettings = await settings.GetAsync();
			using (var transaction = units.Begin(requiresNew: true, isTransactional: true))
			{
				await documents.RecordPaymentAsync(new()
				{
					DocumentId = document.Id,
					IdempotencyKey = Guid.NewGuid(),
					PaidAt = DateTimeOffset.UtcNow.AddMinutes(-1),
					Reference = "Synthetic rollback-only direct payment"
				});
				(await documents.GetAsync(employee.Id, month.ToString("yyyy-MM"))).Single().PaymentStatus.ShouldBe("Paid");
				var sent = await deliveries.SendAsync(new() { DocumentId = document.Id, IdempotencyKey = Guid.NewGuid() });
				rollbackDelivery = sent.Id;
				sent.Status.ShouldBe("QUEUED");
				sent.RequestedBy.ShouldBe(actor);
				await transaction.SaveChangesAsync();
				(await QueuedJobsAsync(rollbackDelivery)).ShouldBe(1);
				var draft = await corrections.StartAsync(new() { DocumentId = document.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic rollback-only direct correction" });
				rollbackDraft = draft.Id;
				var changes = new Func<Task>[]
				{
					async () => { await corrections.CalculateAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance }); },
					async () => { await corrections.ReviewAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp }); },
					async () => { await corrections.CancelAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp }); },
					async () => { await corrections.ApplyAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp }); },
					async () => { await corrections.SettleAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp, TargetMonth = month.AddMonths(1).ToString("yyyy-MM"), PreviewRunId = Guid.NewGuid() }); }
				};
				using (var unscoped = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(Principal(outside, "admin")))
					foreach (var action in changes)
						(await Should.ThrowAsync<BusinessException>(action)).Code.ShouldBe(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
				using (var otherTenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
					foreach (var action in changes) await Should.ThrowAsync<EntityNotFoundException>(action);
				var calculated = await corrections.CalculateAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = draft.Attendance });
				var reviewed = await corrections.ReviewAsync(draft.Id, new() { ConcurrencyStamp = calculated.ConcurrencyStamp });
				reviewed.ReviewedBy.ShouldBe(actor);
				(await Should.ThrowAsync<BusinessException>(() => corrections.ApplyAsync(draft.Id, new() { ConcurrencyStamp = reviewed.ConcurrencyStamp })))
					.Code.ShouldBe(HRErrorCodes.PayslipConflict);
				var cancelled = await corrections.CancelAsync(draft.Id, new() { ConcurrencyStamp = reviewed.ConcurrencyStamp });
				cancelled.Status.ShouldBe("CANCELLED");
				(await corrections.CancelAsync(draft.Id, new() { ConcurrencyStamp = reviewed.ConcurrencyStamp })).Id.ShouldBe(cancelled.Id);
				(await Should.ThrowAsync<BusinessException>(() => corrections.CalculateAsync(draft.Id, new() { ConcurrencyStamp = cancelled.ConcurrencyStamp, Attendance = draft.Attendance })))
					.Code.ShouldBe(HRErrorCodes.PayslipConflict);
				await documents.ReplaceFactsAsync(new() { DocumentId = document.Id, IdempotencyKey = Guid.NewGuid(), Reason = "Synthetic rollback-only direct replacement" });
				var alteredSettings = new PayslipSettingsDto
				{
					SchoolName = "Synthetic rollback-only school",
					SchoolAddress = originalSettings.SchoolAddress,
					ConcurrencyStamp = originalSettings.ConcurrencyStamp,
					IdempotencyKey = Guid.NewGuid(),
					AutomaticEmail = false
				};
				await settings.SaveAsync(alteredSettings);
				// No CompleteAsync: every successful direct mutation is rolled back deliberately.
			}
			using (var read = units.Begin(requiresNew: true, isTransactional: false))
			{
				(await services.GetRequiredService<IRepository<PayslipCorrection, Guid>>().AnyAsync(x => x.Id == rollbackDraft)).ShouldBeFalse();
				(await services.GetRequiredService<IRepository<PayslipDelivery, Guid>>().AnyAsync(x => x.Id == rollbackDelivery)).ShouldBeFalse();
				(await QueuedJobsAsync(rollbackDelivery)).ShouldBe(0);
				var restored = (await documents.GetAsync(employee.Id, month.ToString("yyyy-MM"))).Single();
				restored.Id.ShouldBe(document.Id);
				restored.PaymentStatus.ShouldBe("Finalized");
				restored.PaidAt.ShouldBeNull();
				Convert.ToHexString(SHA256.HashData(await documents.GetPdfAsync(document.Id))).ShouldBe(document.Sha256);
				var restoredSettings = await settings.GetAsync();
				restoredSettings.SchoolName.ShouldBe(originalSettings.SchoolName);
				restoredSettings.SchoolAddress.ShouldBe(originalSettings.SchoolAddress);
				restoredSettings.AutomaticEmail.ShouldBe(originalSettings.AutomaticEmail);
				restoredSettings.ConcurrencyStamp.ShouldBe(originalSettings.ConcurrencyStamp);
				restoredSettings.IdempotencyKey.ShouldBe(originalSettings.IdempotencyKey);
				factory.Messages.ShouldBeEmpty();
			}
		}
		finally { http.HttpContext = priorContext; }
	}

	private static ClaimsPrincipal Principal(Guid actor, string role) => new(new ClaimsIdentity([
		new Claim(AbpClaimTypes.UserId, actor.ToString()), new Claim(AbpClaimTypes.TenantId, EsiPayrollWorkflowScenario.Tenant.ToString()),
		new Claim(AbpClaimTypes.Role, role)], "direct-payslip-fixture"));

	private static async Task<(Guid Actor, Guid Outside)> CreateActorsAsync(PayslipPostgresTestFactory factory, string roleName)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(EsiPayrollWorkflowScenario.Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		(await scope.ServiceProvider.GetRequiredService<IdentityRoleManager>().CreateAsync(new IdentityRole(Guid.NewGuid(), roleName, EsiPayrollWorkflowScenario.Tenant)))
			.Succeeded.ShouldBeTrue();
		var users = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
		var ids = new List<Guid>();
		foreach (var suffix in new[] { "owner", "outside" })
		{
			var name = roleName + "-" + suffix;
			var user = new IdentityUser(Guid.NewGuid(), name, name + "@test.shikshamitra.local", EsiPayrollWorkflowScenario.Tenant);
			(await users.CreateAsync(user)).Succeeded.ShouldBeTrue();
			(await users.AddToRoleAsync(user, roleName)).Succeeded.ShouldBeTrue();
			(await users.IsInRoleAsync(user, "admin")).ShouldBeFalse();
			ids.Add(user.Id);
		}
		await unit.CompleteAsync();
		return (ids[0], ids[1]);
	}
}