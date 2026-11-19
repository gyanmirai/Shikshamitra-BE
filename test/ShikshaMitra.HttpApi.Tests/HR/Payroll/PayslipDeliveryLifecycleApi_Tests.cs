using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class PayslipDeliveryLifecycleApi_Tests
{
	[PayrollRegistrationPostgresFact]
	public async Task Issuance_and_correction_candidates_preserve_committed_payroll_and_delivery_revalidates_persisted_state()
	{
		using var factory = new PayslipPostgresTestFactory();
		var workflow = new EsiPayrollWorkflowScenario(factory);
		var client = workflow.Api;
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(factory);
		var settings = (await client.GetFromJsonAsync<PayslipSettingsDto>("/api/payroll/payslip-settings", json))!;
		settings.AutomaticEmail = true;
		settings.IdempotencyKey = Guid.NewGuid();
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PutAsJsonAsync("/api/payroll/payslip-settings", settings, json));
		var now = DateTime.UtcNow;
		var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(client, "PayslipAuto", dateOfJoining: month);
		await workflow.ConfigureAsync(true);
		await workflow.EnrollAsync(employee.Id, "1234567890");
		await workflow.SaveCompensationAsync(employee.Id, month, "GROSS", 10000m);
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.ActivateAsync(employee.Id));
		await workflow.SeedAttendanceAsync(employee.Id, month);
		var run = await workflow.CalculateAsync(month);
		await workflow.ReviewAsync(run.Id, employee.Id, month);
		factory.Barrier.FailNextCommand("INSERT INTO \"HR_PayslipDocuments\"");
		var failedLock = await workflow.LockAsync(run.Id, employee.Id, month);
		failedLock.IsSuccessStatusCode.ShouldBeFalse();
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			(await scope.ServiceProvider.GetRequiredService<IRepository<PayslipDocument, Guid>>()
				.CountAsync(x => x.EmployeeId == employee.Id)).ShouldBe(0);
			(await scope.ServiceProvider.GetRequiredService<IRepository<PayslipDelivery, Guid>>()
				.CountAsync(x => x.EmployeeId == employee.Id)).ShouldBe(0);
			(await scope.ServiceProvider.GetRequiredService<ISalarySlipRepository>()
				.CountAsync(x => x.EmployeeId == employee.Id)).ShouldBe(0);
			var snapshot = await scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>()
				.GetAsync(x => x.EmployeeId == employee.Id && x.MonthYear == month.ToString("yyyy-MM"));
			snapshot.IsLocked.ShouldBeFalse();
			await unit.CompleteAsync();
		}
		factory.Messages.ShouldBeEmpty();
		await EsiPayrollWorkflowScenario.RequireSuccess(await workflow.LockAsync(run.Id, employee.Id, month));
		var query = $"employeeId={employee.Id}&monthYear={month:yyyy-MM}";
		var documentsUrl = $"/api/payroll/payslip-documents?{query}";
		var issued = (await client.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single();
		var historyUrl = $"/api/payroll/payslip-deliveries?salarySlipId={issued.SalarySlipId}";
		var initialAttempt = (await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single();
		initialAttempt.Automatic.ShouldBeTrue();
		initialAttempt.Status.ShouldBe("QUEUED");
		factory.Messages.ShouldBeEmpty();
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>().Change(TestPrincipal()))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var revisions = scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>();
			var beforeRevision = await revisions.LockAsync();
			var sourceAttendance = await scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>()
				.GetListAsync(x => x.EmployeeId == employee.Id && x.AttendanceDate.Year == month.Year && x.AttendanceDate.Month == month.Month);
			var changeDate = sourceAttendance.First(x => x.AttendanceDate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).AttendanceDate;
			var candidateAttendance = sourceAttendance.Select(x => new EmployeeAttendance
			{
				EmployeeId = x.EmployeeId,
				TenantId = x.TenantId,
				AttendanceDate = x.AttendanceDate,
				EducationMediumStageId = x.EducationMediumStageId,
				TimeIn = x.TimeIn,
				TimeOut = x.TimeOut,
				StatusType = x.AttendanceDate == changeDate ? EmployeeAttendanceStatusType.FullDayLeave : x.StatusType
			}).ToList();
			var snapshots = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
			var original = await snapshots.GetAsync(x => x.EmployeeId == employee.Id && x.MonthYear == month.ToString("yyyy-MM"));
			var originalNet = original.NetPay;
			var adjustments = scope.ServiceProvider.GetRequiredService<IRepository<PayrollAdjustment, Guid>>();
			var adjustmentCount = await adjustments.CountAsync(x => x.EmployeeId == employee.Id);
			var calculator = scope.ServiceProvider.GetRequiredService<PayrollSnapshotRefresher>();
			await Should.ThrowAsync<Volo.Abp.BusinessException>(() => calculator.CalculateCorrectionAsync(
				employee.Id, month.ToString("yyyy-MM"), run.AcademicYearId,
				[candidateAttendance[0], candidateAttendance[0]]));
			var invalidStatus = new EmployeeAttendance
			{
				EmployeeId = employee.Id,
				TenantId = original.TenantId,
				AttendanceDate = candidateAttendance[0].AttendanceDate,
				StatusType = (EmployeeAttendanceStatusType) 999
			};
			await Should.ThrowAsync<Volo.Abp.BusinessException>(() => calculator.CalculateCorrectionAsync(
				employee.Id, month.ToString("yyyy-MM"), run.AcademicYearId, [invalidStatus]));
			var candidate = await calculator.CalculateCorrectionAsync(
				employee.Id, month.ToString("yyyy-MM"), run.AcademicYearId, candidateAttendance);
			candidate.IsLocked.ShouldBeFalse();
			candidate.NetPay.ShouldBeLessThan((double) originalNet);
			candidate.EmployeeStateInsuranceNumber.ShouldBe("1234567890");
			candidate.EmployerContributions.ShouldContain(x => x.Amount > 0);
			original.NetPay.ShouldBe(originalNet);
			original.IsLocked.ShouldBeTrue();
			(await adjustments.CountAsync(x => x.EmployeeId == employee.Id)).ShouldBe(adjustmentCount);
			(await revisions.ReadAsync()).ShouldBe(beforeRevision);
			await unit.CompleteAsync();
		}
		Convert.ToHexString(SHA256.HashData(await client.GetByteArrayAsync($"/api/payroll/payslip-documents/{issued.Id}/pdf"))).ShouldBe(issued.Sha256);
		var startInput = new StartPayslipCorrectionDto
		{
			DocumentId = issued.Id,
			IdempotencyKey = Guid.NewGuid(),
			Reason = "Correct synthetic attendance before payment"
		};
		var startResponse = await client.PostAsJsonAsync("/api/payroll/payslip-corrections", startInput, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(startResponse);
		var draft = (await startResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		draft.Status.ShouldBe("DRAFT");
		var repeatedStart = await client.PostAsJsonAsync("/api/payroll/payslip-corrections", startInput, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(repeatedStart);
		(await repeatedStart.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!.Id.ShouldBe(draft.Id);
		var payment = new RecordPayslipPaymentDto
		{
			DocumentId = issued.Id,
			IdempotencyKey = Guid.NewGuid(),
			PaidAt = DateTimeOffset.UtcNow,
			Reference = "Synthetic correction cancellation payment"
		};
		(await client.PostAsJsonAsync("/api/payroll/payslip-documents/payment", payment, json)).IsSuccessStatusCode.ShouldBeFalse();
		var correctionAttendance = Enumerable.Range(1, DateTime.DaysInMonth(month.Year, month.Month))
			.Select(day => new PayslipCorrectionAttendanceDto
			{
				Date = new DateOnly(month.Year, month.Month, day),
				Status = day == 1 ? EmployeeAttendanceStatusType.FullDayLeave : EmployeeAttendanceStatusType.Present
			}).ToList();
		var calculateResponse = await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/calculate",
			new CalculatePayslipCorrectionDto { ConcurrencyStamp = draft.ConcurrencyStamp, Attendance = correctionAttendance }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(calculateResponse);
		var calculated = (await calculateResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		calculated.Status.ShouldBe("CALCULATED");
		calculated.Candidate.ShouldNotBeNull();
		var reviewedResponse = await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/review",
			new PayslipCorrectionActionDto { ConcurrencyStamp = calculated.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(reviewedResponse);
		var reviewed = (await reviewedResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!;
		reviewed.Status.ShouldBe("REVIEWED");
		var cancelledResponse = await client.PostAsJsonAsync($"/api/payroll/payslip-corrections/{draft.Id}/cancel",
			new PayslipCorrectionActionDto { ConcurrencyStamp = reviewed.ConcurrencyStamp }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(cancelledResponse);
		(await cancelledResponse.Content.ReadFromJsonAsync<PayslipCorrectionDto>(json))!.Status.ShouldBe("CANCELLED");
		await EsiPayrollWorkflowScenario.RequireSuccess(await client.PostAsJsonAsync("/api/payroll/payslip-documents/payment", payment, json));
		Convert.ToHexString(SHA256.HashData(await client.GetByteArrayAsync($"/api/payroll/payslip-documents/{issued.Id}/pdf"))).ShouldBe(issued.Sha256);

		// Exercise the real issuance/outbox service in a transaction that fails after artifact insertion.
		// Disposal rolls back the document and its queued job together; the original remains current.
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>().Change(TestPrincipal()))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			await scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>().LockAsync();
			var source = await scope.ServiceProvider.GetRequiredService<IRepository<PayslipDocument, Guid>>().GetAsync(issued.Id);
			var slip = await scope.ServiceProvider.GetRequiredService<ISalarySlipRepository>().GetAsync(issued.SalarySlipId);
			await scope.ServiceProvider.GetRequiredService<PayslipIssuanceService>().ReplaceFactsAsync(slip, source,
				Guid.NewGuid(), "Synthetic rolled-back replacement");
			await unit.SaveChangesAsync();
			await unit.RollbackAsync();
		}
		(await client.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Count.ShouldBe(1);
		(await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Count.ShouldBe(1);
		factory.Messages.ShouldBeEmpty();

		var replacementResponse = await client.PostAsJsonAsync("/api/payroll/payslip-documents/replace-facts",
			new ReplacePayslipFactsDto { DocumentId = issued.Id, Reason = "Synthetic replacement before delivery", IdempotencyKey = Guid.NewGuid() }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(replacementResponse);
		var replacement = (await replacementResponse.Content.ReadFromJsonAsync<PayslipDocumentDto>(json))!;
		var attempts = (await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!;
		attempts.Count.ShouldBe(2);
		var currentAttempt = attempts.Single(x => x.DocumentId == replacement.Id);
		currentAttempt.Automatic.ShouldBeTrue();
		await ExecuteAsync(factory, initialAttempt.Id);
		factory.Messages.ShouldBeEmpty();
		(await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!
			.Single(x => x.Id == initialAttempt.Id).Status.ShouldBe("SUPERSEDED");

		// New service scopes read committed intent; neither the HTTP scope nor a live job object is needed.
		await Task.WhenAll(ExecuteAsync(factory, currentAttempt.Id), ExecuteAsync(factory, currentAttempt.Id));
		await ExecuteAsync(factory, currentAttempt.Id);
		factory.Messages.Count.ShouldBe(1);
		var captured = factory.Messages.Single();
		captured.Recipient.ShouldBe(issued.RecipientEmail);
		Convert.ToHexString(SHA256.HashData(captured.Pdf)).ShouldBe(replacement.Sha256);
		captured.FileName.ShouldContain("R2.pdf");

		// Simulate a process that persisted its claim then died before recording the SMTP outcome.
		var resendResponse = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries",
			new SendPayslipDto { DocumentId = replacement.Id, IdempotencyKey = Guid.NewGuid() }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(resendResponse);
		var abandoned = (await resendResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<PayslipDelivery, Guid>>();
			var delivery = await repository.GetAsync(abandoned.Id);
			delivery.Start(issued.RecipientEmail!, DateTime.UtcNow.AddMinutes(-31));
			await repository.UpdateAsync(delivery);
			await unit.CompleteAsync();
		}
		await ExecuteAsync(factory, abandoned.Id);
		await ExecuteAsync(factory, abandoned.Id);
		factory.Messages.Count.ShouldBe(1);
		var recovered = (await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single(x => x.Id == abandoned.Id);
		recovered.Status.ShouldBe("UNCERTAIN");
		recovered.ErrorCategory.ShouldBe("SEND_OUTCOME_UNKNOWN");

		var missingEmailInput = new SendPayslipDto { DocumentId = replacement.Id, IdempotencyKey = Guid.NewGuid() };
		var missingEmailResponse = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries", missingEmailInput, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(missingEmailResponse);
		var missingEmailAttempt = (await missingEmailResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		missingEmailAttempt.IdempotencyKey.ShouldBe(missingEmailInput.IdempotencyKey);
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			// Simulate a previously recorded employee whose contact address is now unavailable.
			var storedEmployee = await scope.ServiceProvider.GetRequiredService<IEmployeeRepository>().GetAsync(employee.Id);
			var context = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			context.Entry(storedEmployee).Property(x => x.Email).CurrentValue = null;
			await unit.CompleteAsync();
		}
		await ExecuteAsync(factory, missingEmailAttempt.Id);
		var missingEmailResult = (await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single(x => x.Id == missingEmailAttempt.Id);
		missingEmailResult.Status.ShouldBe("FAILED");
		missingEmailResult.ErrorCategory.ShouldBe("EMPLOYEE_EMAIL_REQUIRED");
		(await client.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single(x => x.IsCurrent).PaymentStatus.ShouldBe("Paid");
		factory.Messages.Count.ShouldBe(1);

		// Invalid persisted contact data also blocks delivery without changing finalized payroll.
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var storedEmployee = await scope.ServiceProvider.GetRequiredService<IEmployeeRepository>().GetAsync(employee.Id);
			var context = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			context.Entry(storedEmployee).Property(x => x.Email).CurrentValue = "invalid-synthetic-address";
			await unit.CompleteAsync();
		}
		var invalidEmailResponse = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries",
			new SendPayslipDto { DocumentId = replacement.Id, IdempotencyKey = Guid.NewGuid() }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(invalidEmailResponse);
		var invalidEmailAttempt = (await invalidEmailResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		await ExecuteAsync(factory, invalidEmailAttempt.Id);
		var invalidEmailResult = (await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single(x => x.Id == invalidEmailAttempt.Id);
		invalidEmailResult.Status.ShouldBe("FAILED");
		invalidEmailResult.ErrorCategory.ShouldBe("EMPLOYEE_EMAIL_REQUIRED");
		factory.Messages.Count.ShouldBe(1);

		// HR repairs the address through the controlled Identity-managed employee workflow.
		var personalInfoUrl = $"/api/employees/{employee.Id}/personal-info";
		var accountUrl = $"/api/employees/{employee.Id}/account-email";
		var account = (await client.GetFromJsonAsync<EmployeeAccountEmailDto>(accountUrl, json))!;
		var repairedEmail = "repaired-" + Guid.NewGuid().ToString("N") + "@test.shikshamitra.local";
		var repair = await client.PutAsJsonAsync(accountUrl, new ChangeEmployeeAccountEmailDto
		{
			Email = repairedEmail, Reason = "HR verified this corrected synthetic recipient with the employee",
			ConfirmEmployeeVerified = true, ConcurrencyToken = account.ConcurrencyToken, IdempotencyKey = Guid.NewGuid()
		}, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(repair);
		var repaired = (await repair.Content.ReadFromJsonAsync<EmployeeAccountEmailDto>(json))!;
		repaired.AccountEmail.ShouldBe(repairedEmail);
		repaired.UserName.ShouldBe(repairedEmail);
		(await client.GetFromJsonAsync<PersonalInfoResponse>(personalInfoUrl, json))!.Email.ShouldBe(repairedEmail);
		var repeatedFailedResponse = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries", missingEmailInput, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(repeatedFailedResponse);
		var repeatedFailed = (await repeatedFailedResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		repeatedFailed.Id.ShouldBe(missingEmailAttempt.Id);
		repeatedFailed.IdempotencyKey.ShouldBe(missingEmailInput.IdempotencyKey);
		repeatedFailed.Status.ShouldBe("FAILED");
		await ExecuteAsync(factory, repeatedFailed.Id);
		factory.Messages.Count.ShouldBe(1);
		var retryInput = new SendPayslipDto { DocumentId = replacement.Id, IdempotencyKey = Guid.NewGuid() };
		var retryResponse = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries", retryInput, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(retryResponse);
		var retry = (await retryResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		retry.IdempotencyKey.ShouldBe(retryInput.IdempotencyKey);
		retry.Id.ShouldNotBe(missingEmailAttempt.Id);
		await Task.WhenAll(ExecuteAsync(factory, retry.Id), ExecuteAsync(factory, retry.Id));
		await ExecuteAsync(factory, retry.Id);
		factory.Messages.Count.ShouldBe(2);
		var recoveredMessage = factory.Messages.Last();
		recoveredMessage.Recipient.ShouldBe(repairedEmail);
		Convert.ToHexString(SHA256.HashData(recoveredMessage.Pdf)).ShouldBe(replacement.Sha256);
		var recoveredHistory = (await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!;
		recoveredHistory.Single(x => x.Id == retry.Id).Status.ShouldBe("PROVIDER_ACCEPTED");
		recoveredHistory.Single(x => x.Id == retry.Id).IdempotencyKey.ShouldBe(retryInput.IdempotencyKey);
		recoveredHistory.Single(x => x.Id == missingEmailAttempt.Id).IdempotencyKey.ShouldBe(missingEmailInput.IdempotencyKey);
		recoveredHistory.Single(x => x.Id == missingEmailAttempt.Id).Status.ShouldBe("FAILED");
		recoveredHistory.Single(x => x.Id == invalidEmailAttempt.Id).Status.ShouldBe("FAILED");
		Convert.ToHexString(SHA256.HashData(await client.GetByteArrayAsync($"/api/payroll/payslip-documents/{issued.Id}/pdf"))).ShouldBe(issued.Sha256);
		Convert.ToHexString(SHA256.HashData(await client.GetByteArrayAsync($"/api/payroll/payslip-documents/{replacement.Id}/pdf"))).ShouldBe(replacement.Sha256);
		(await client.GetFromJsonAsync<List<PayslipDocumentDto>>(documentsUrl, json))!.Single(x => x.IsCurrent).PaymentStatus.ShouldBe("Paid");

		var revokedResponse = await client.PostAsJsonAsync("/api/payroll/payslip-deliveries",
			new SendPayslipDto { DocumentId = replacement.Id, IdempotencyKey = Guid.NewGuid() }, json);
		await EsiPayrollWorkflowScenario.RequireSuccess(revokedResponse);
		var revokedAttempt = (await revokedResponse.Content.ReadFromJsonAsync<PayslipDeliveryDto>(json))!;
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var users = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
			var actor = (await users.FindByIdAsync(TestAuthHandler.TestUserId))!;
			actor.SetIsActive(false);
			(await users.UpdateAsync(actor)).Succeeded.ShouldBeTrue();
			await unit.CompleteAsync();
		}
		await ExecuteAsync(factory, revokedAttempt.Id);
		var revokedResult = (await client.GetFromJsonAsync<List<PayslipDeliveryDto>>(historyUrl, json))!.Single(x => x.Id == revokedAttempt.Id);
		revokedResult.Status.ShouldBe("FAILED");
		revokedResult.ErrorCategory.ShouldBe("ACCESS_REVOKED");
		factory.Messages.Count.ShouldBe(2);
	}

	private static async Task ExecuteAsync(PayslipPostgresTestFactory factory, Guid deliveryId)
	{
		using var scope = factory.Services.CreateScope();
		await scope.ServiceProvider.GetRequiredService<PayslipDeliveryBackgroundJob>().ExecuteAsync(
			new PayslipDeliveryJobArgs { TenantId = Guid.Parse(TestAuthHandler.TestTenantId), DeliveryId = deliveryId });
	}

	private static ClaimsPrincipal TestPrincipal() => new(new ClaimsIdentity(new[] {
		new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId),
		new Claim(AbpClaimTypes.UserName, TestAuthHandler.TestUserName),
		new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId),
		new Claim(AbpClaimTypes.Role, "admin") }, "Test"));
}
