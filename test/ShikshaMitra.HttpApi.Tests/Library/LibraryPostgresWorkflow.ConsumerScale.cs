using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private Task<FinanceConsumerPgScale> MeasureFinancePgConsumerScaleAsync(LibraryPostgresApiTestFactory factory,
		int expectedCount, int expectedReview, decimal expectedOutstanding) => InFinancePgAsync(factory, async services =>
	{
		using var principal = ConsumerPgPrincipal(services);
		var reader = await ConsumerPgReaderAsync(services);
		var db = await FinancePgDbAsync(services);
		await db.Database.ExecuteSqlRawAsync("SET LOCAL DateStyle = 'SQL, DMY'");
		using var activity = new Activity("Native consumer bounded scale").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var capture = new FinanceConsumerPgSqlCapture(activity.TraceId);
		var query = await reader.GetQueryAsync(new(LibraryChargePositionReadPurpose.ChargeHistory, factory.YearId));
		capture.Commands.ShouldBeEmpty();
		(await query.CountAsync()).ShouldBe(expectedCount);
		(await query.CountAsync(row => row.Evidence == FinanceExternalPositionEvidence.NeedsReview)).ShouldBe(expectedReview);
		var verified = query.Where(row => row.Evidence == FinanceExternalPositionEvidence.Verified);
		(await verified.CountAsync()).ShouldBe(expectedCount - expectedReview);
		(await verified.CountAsync(row => row.Collectible > 0m)).ShouldBe(0);
		(await verified.SumAsync(row => row.FinancialOutstanding)).ShouldBe(expectedOutstanding);
		var ids = verified.Select(row => row.SourceId);
		var joined = from assessment in db.Set<LibraryCharge>()
			join id in ids on assessment.Id equals id
			where assessment.TenantId == FinancePgTenantId && assessment.AcademicYearId == factory.YearId
			select assessment.Id;
		(await joined.CountAsync()).ShouldBe(expectedCount - expectedReview);
		(await joined.OrderBy(id => id).Take(1).ToListAsync()).Count.ShouldBe(1);
		(await joined.OrderBy(id => id).Skip(expectedCount - expectedReview).Take(1).ToListAsync()).ShouldBeEmpty();
		var commands = capture.Commands.ToArray();
		commands.Length.ShouldBe(8);
		await ExplainConsumerPgAsync(db, commands, $"source-count-{expectedCount}-review-{expectedReview}");
		return new FinanceConsumerPgScale(commands.Length, commands.Select(row => row.Sql.Length).ToArray());
	});

	private static async Task SeedFinancePgConsumerSiblingsAsync(LibraryPostgresApiTestFactory factory,
		FinancePgFixture fixture, int count)
	{
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		for (var index = 0; index < count; index++)
		{
			var source = await InFinancePgAsync(factory, async services =>
			{
				var db = await FinancePgDbAsync(services);
				var original = await db.Set<LibraryCharge>().AsNoTracking().SingleAsync(row => row.Id == fixture.Source.SourceId);
				var sibling = new LibraryCharge(Guid.NewGuid(), fixture.EmployeeId, 1, original.ChargeCategoryId, null,
					100.75m, new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc),
					new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc), FinancePgTenantId)
					{ Remarks = "Reviewed native bounded-scale assessment." };
				sibling.ConfirmSourceContext(factory.YearId, LibraryAcademicYearProvenance.ReviewedLegacy,
					JsonSerializer.Serialize(new { AssessmentId = sibling.Id, AcademicYearId = factory.YearId,
						ReviewedBy = FinancePgMakerId, Reason = "Checked this bounded fixture assessment's source register." }),
					new(2026, 11, 17), new(2026, 11, 24),
					LibraryCivilDateProvenance.ReviewedLegacy, LibraryCivilDateProvenance.ReviewedLegacy,
					JsonSerializer.Serialize(new { AssessmentId = sibling.Id, ReviewedBy = FinancePgMakerId,
						Reason = "Confirmed this fixture assessment's explicit school dates." }), "INR");
				db.Add(sibling); await db.SaveChangesAsync();
				return new FinanceExternalSourceReference { Kind = FinanceExternalSourceKind.LibraryAssessment,
					SourceId = sibling.Id, SourceVersion = sibling.GetAssessmentVersion() };
			});
			var posted = await PostAsync<FinanceExternalReceivableDto>(maker, FinancePgRoute,
				new PostFinanceExternalReceivableInput { Source = source, FinanceChargeCategoryId = fixture.ChargeCategoryId,
					IdempotencyKey = Guid.NewGuid().ToString("N") });
			posted.ApprovalStatus.ShouldBe(Finance.Fees.Charges.ChargeStatus.Pending);
			posted.OutstandingAmount.ShouldBe(0m);
		}
	}
	private sealed record FinanceConsumerPgScale(int Commands, int[] SqlLengths);
}
