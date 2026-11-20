using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private static async Task<FinancePgConsumerLargeSamples> SeedFinancePgConsumerLargeAsync(
		LibraryPostgresApiTestFactory factory, FinancePgFixture fixture, int additionalCount)
	{
		if (additionalCount < 1 || additionalCount > 2500) throw new ArgumentOutOfRangeException(nameof(additionalCount));
		var setup = await ReadFinancePgConsumerLargeSetupAsync(factory, fixture);
		FinancePgConsumerLargeSample? first = null;
		FinancePgConsumerLargeSample? last = null;
		for (var offset = 0; offset < additionalCount; offset += 250)
		{
			var count = Math.Min(250, additionalCount - offset);
			var chunk = await InFinancePgAsync(factory, async services =>
			{
				using var maker = ConsumerPgPrincipal(services);
				var db = await FinancePgDbAsync(services);
				var assessments = new List<LibraryCharge>(count);
				for (var index = 0; index < count; index++)
				{
					var id = Guid.NewGuid();
					var source = new LibraryCharge(id, fixture.EmployeeId, 1, setup.LibraryCategoryId, null, 100.75m,
						new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc),
						new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc), FinancePgTenantId)
						{ Remarks = $"Reviewed scale assessment {id:N}: José — पुस्तक." };
					source.ConfirmSourceContext(factory.YearId, LibraryAcademicYearProvenance.ReviewedLegacy,
						JsonSerializer.Serialize(new { AssessmentId = id, AcademicYearId = factory.YearId,
							ReviewedBy = FinancePgMakerId, Reason = "Reviewed this source's original school-year register entry." }),
						new(2026, 11, 17), new(2026, 11, 24),
						LibraryCivilDateProvenance.ReviewedLegacy, LibraryCivilDateProvenance.ReviewedLegacy,
						JsonSerializer.Serialize(new { AssessmentId = id, AcademicYearId = factory.YearId,
							ReviewedBy = FinancePgMakerId, AssessmentDate = "2026-11-17", DueDate = "2026-11-24",
							Reason = "Reviewed this source's explicit school dates; raw register timestamps are retained." }), "INR");
					db.Add(source);
					assessments.Add(source);
				}
				// Break the source/link FK cycle with a real first insert. Capture the
				// accepted source version after ABP has applied its insert conventions.
				await db.SaveChangesAsync();
				FinancePgConsumerLargeSample? chunkFirst = null;
				FinancePgConsumerLargeSample? chunkLast = null;
				foreach (var assessment in assessments)
				{
					var source = new FinanceExternalSourceReference
					{
						Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = assessment.Id,
						SourceVersion = assessment.GetAssessmentVersion()
					};
					var facts = FinancePgConsumerLargeFacts(factory, fixture, setup, assessment, source);
					var snapshot = JsonSerializer.Serialize(facts);
					var fingerprint = FinanceExternalSourceFingerprintV1.Source(new FinanceExternalFingerprintFacts(
						facts.TenantId, source.Kind, source.SourceId, source.SourceVersion, facts.AcademicYearId,
						facts.AcademicYearConfirmed, facts.AcademicYearEvidenceJson, facts.Payer.Kind,
						facts.Payer.StudentId, facts.Payer.EmployeeId, facts.Amount, facts.Currency,
						facts.AssessmentDate, facts.DueDate, facts.SchoolDatesConfirmed, facts.Reason, facts.EvidenceReference));
					var request = ChargeRequest.CreateExternal(Guid.NewGuid(), FinancePgTenantId, factory.YearId,
						"Library assessment", fixture.ChargeCategoryId, facts.Reason, facts.Amount,
						facts.DueDate, FinancePgMakerId, snapshot);
					request.SetProperty("IsApprovalRequiredAtCreation", setup.IsApprovalRequired);
					request.SetIncomeCategory(setup.IncomeCategoryId, setup.IncomeCategoryName);
					var charge = Charge.CreateExternal(Guid.NewGuid(), FinancePgTenantId, factory.YearId, request.Id,
						request.ChargeName, fixture.ChargeCategoryId,
						new FinanceExternalPayerIdentity(FinanceExternalPayerKind.Employee, fixture.EmployeeId),
						facts.Reason, facts.Amount, facts.AssessmentDate, facts.DueDate, FinancePgMakerId);
					charge.SetIncomeCategory(setup.IncomeCategoryId, setup.IncomeCategoryName);
					charge.SetAccountingSnapshot(setup.IncomeCategoryId, setup.IncomeAccountHeadId);
					charge.Approve(fixture.ReviewerId, "Independent persisted reviewer approved the named scale fixture source.");
					var link = new FinanceExternalReceivableSourceLink(Guid.NewGuid(), FinancePgTenantId,
						source.Kind, source.SourceId, source.SourceVersion, fingerprint, snapshot, request.Id,
						charge.Id, factory.YearId, facts.Currency, null, null);
					var postKey = $"scale-post-{assessment.Id:N}";
					var post = new PostFinanceExternalReceivableInput
						{ Source = source, FinanceChargeCategoryId = fixture.ChargeCategoryId, IdempotencyKey = postKey };
					var command = new FinanceExternalReceivableCommand(Guid.NewGuid(), FinancePgTenantId, "Post",
						postKey, FinancePgConsumerPostFingerprint(post, fingerprint), link.Id, FinancePgMakerId);
					db.Add(request); db.Add(charge); db.Add(link); db.Add(command);
					assessment.BindFinanceSource(link.Id, source.SourceVersion);
					var sample = new FinancePgConsumerLargeSample(source, postKey, link.Id, charge.Id);
					chunkFirst ??= sample;
					chunkLast = sample;
				}
				// All immutable evidence is inserted once with its final accepted payload.
				// No link/command UPDATE, payment, fake balance or trigger bypass occurs.
				await db.SaveChangesAsync();
				return new FinancePgConsumerLargeSamples(count, chunkFirst!, chunkLast!);
			});
			first ??= chunk.First;
			last = chunk.Last;
		}
		return new FinancePgConsumerLargeSamples(additionalCount, first!, last!);
	}

	private static Task<FinancePgConsumerLargeSetup> ReadFinancePgConsumerLargeSetupAsync(
		LibraryPostgresApiTestFactory factory, FinancePgFixture fixture) => InFinancePgAsync(factory, async services =>
	{
		var db = await FinancePgDbAsync(services);
		fixture.ReviewerId.ShouldNotBe(FinancePgMakerId);
		var reviewer = await db.Set<IdentityUser>().AsNoTracking().SingleAsync(row => row.TenantId == FinancePgTenantId && row.Id == fixture.ReviewerId);
		reviewer.IsActive.ShouldBeTrue();
		var employee = await db.Set<Employee>().AsNoTracking().Where(row => row.TenantId == FinancePgTenantId && row.Id == fixture.EmployeeId)
			.Select(row => new { row.FirstName, row.MiddleName, row.LastName }).SingleAsync();
		var libraryCategoryId = await db.Set<LibraryCharge>().AsNoTracking()
			.Where(row => row.TenantId == FinancePgTenantId && row.Id == fixture.Source.SourceId)
			.Select(row => row.ChargeCategoryId).SingleAsync();
		(await db.Set<LibraryChargeCategory>().AsNoTracking().SingleAsync(row => row.TenantId == FinancePgTenantId && row.Id == libraryCategoryId))
			.AcademicYearId.ShouldBe(factory.YearId);
		var category = await db.Set<ChargeCategory>().AsNoTracking().SingleAsync(row => row.TenantId == FinancePgTenantId && row.Id == fixture.ChargeCategoryId);
		category.AcademicYearId.ShouldBe(factory.YearId);
		category.IsActive.ShouldBeTrue();
		category.IncomeCategoryId.ShouldNotBeNull();
		var income = await db.Set<TransactionCategory>().AsNoTracking().SingleAsync(row => row.TenantId == FinancePgTenantId && row.Id == category.IncomeCategoryId);
		income.AcademicYearId.ShouldBe(factory.YearId);
		income.CategoryType.ShouldBe(TransactionType.Income);
		income.IsActive.ShouldBeTrue();
		return new FinancePgConsumerLargeSetup(libraryCategoryId, category.IsApprovalRequired, income.Id, income.Name,
			category.IncomeAccountHeadId, string.Join(" ", new[] { employee.FirstName, employee.MiddleName, employee.LastName }
				.Where(part => !string.IsNullOrWhiteSpace(part))));
	});

	private static FinanceExternalReceivableSourceFacts FinancePgConsumerLargeFacts(LibraryPostgresApiTestFactory factory,
		FinancePgFixture fixture, FinancePgConsumerLargeSetup setup, LibraryCharge assessment, FinanceExternalSourceReference source) =>
		new(source, FinancePgTenantId, factory.YearId,
			new FinanceExternalPayerDto { Kind = FinanceExternalPayerKind.Employee, EmployeeId = fixture.EmployeeId,
				StudentId = null, DisplayName = setup.EmployeeDisplayName }, assessment.Amount, assessment.Currency!,
			assessment.SchoolChargeDate!.Value, assessment.SchoolDueDate!.Value, true, assessment.Remarks!, assessment.SourceDateEvidenceJson!)
		{
			AcademicYearConfirmed = true, AcademicYearEvidenceJson = assessment.AcademicYearEvidenceJson!,
			ReasonOrigin = new FinanceExternalSourceReasonOrigin(1, assessment.ChargeCategoryId, assessment.Remarks, false)
		};

	private static string FinancePgConsumerPostFingerprint(PostFinanceExternalReceivableInput input, string sourceFingerprint)
	{
		// Reuse the actual App protocol. This test helper must not implement another
		// PostCommand serializer/hash that could make a fabricated replay pass.
		var protocol = typeof(FinanceExternalReceivableAppService).Assembly.GetType(
			"ShikshaMitra.Finance.ExternalReceivables.FinanceExternalReceivableFingerprint", throwOnError: true)!;
		var method = protocol.GetMethod("PostCommand", BindingFlags.Public | BindingFlags.Static,
			binder: null, types: [typeof(PostFinanceExternalReceivableInput), typeof(string)], modifiers: null)
			?? throw new InvalidOperationException("The actual PostCommand fingerprint protocol is unavailable.");
		return method.Invoke(null, [input, sourceFingerprint]) as string
			?? throw new InvalidOperationException("The actual PostCommand protocol returned no fingerprint.");
	}

	private sealed record FinancePgConsumerLargeSetup(Guid LibraryCategoryId, bool IsApprovalRequired,
		Guid IncomeCategoryId, string IncomeCategoryName, Guid? IncomeAccountHeadId, string EmployeeDisplayName);
	private sealed record FinancePgConsumerLargeSample(FinanceExternalSourceReference Source, string PostKey, Guid LinkId, Guid FinanceChargeId);
	private sealed record FinancePgConsumerLargeSamples(int AddedCount, FinancePgConsumerLargeSample First, FinancePgConsumerLargeSample Last);
}
