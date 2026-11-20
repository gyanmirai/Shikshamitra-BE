using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(FinanceExternalPayerKind.Student)]
	[InlineData(FinanceExternalPayerKind.Employee)]
	public async Task A_Production_Source_Reader_Should_Post_And_Replay_The_Assessments_Stored_Confirmed_Evidence(
		FinanceExternalPayerKind payerKind)
	{
		var fixture = await SeedAsync(payerKind, true);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		fixture = await ConfirmStoredAssessmentAsync(fixture);
		var service = Service(fixture, useStoredSource: true);

		var posted = await WithUnitOfWorkAsync(() => service.PostAsync(Post(fixture)));
		var replay = await WithUnitOfWorkAsync(() => service.PostAsync(Post(fixture)));
		var current = await WithUnitOfWorkAsync(() => service.GetAsync(Post(fixture).Source));

		replay.FinanceChargeId.ShouldBe(posted.FinanceChargeId);
		current.Source.SourceVersion.ShouldBe(fixture.Version);
		current.AssessmentDate.ShouldBe(fixture.AssessmentDate);
		current.DueDate.ShouldBe(fixture.DueDate);
		current.AcademicYearId.ShouldBe(fixture.YearId);
		current.Payer.StudentId.ShouldBe(payerKind == FinanceExternalPayerKind.Student ? fixture.PersonId : null);
		current.Payer.EmployeeId.ShouldBe(payerKind == FinanceExternalPayerKind.Employee ? fixture.PersonId : null);
		current.SettlementAvailable.ShouldBeFalse();
	}

	[Fact]
	public async Task A_Production_Source_Reader_Should_Reject_Unreviewed_Legacy_Year_And_Date_Components()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			Service(fixture, useStoredSource: true).PostAsync(Post(fixture))));

		error.Code.ShouldBe(LibraryFinanceErrorCodes.SourceRequiresReview);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(0));
	}

	[Fact]
	public async Task A_Production_Source_Reader_Should_Still_Reject_An_Assessment_Changed_Before_Posting()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		fixture = await ConfirmStoredAssessmentAsync(fixture);
		await WithUnitOfWorkAsync(async () =>
		{
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var assessment = await charges.GetAsync(fixture.AssessmentId);
			assessment.ApplyUpdate(125m, null, null, null, null, DateTime.UtcNow);
			await charges.UpdateAsync(assessment, true);
		});

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			Service(fixture, useStoredSource: true).PostAsync(Post(fixture))));

		error.Code.ShouldBe(LibraryFinanceErrorCodes.SourceChanged);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(0));
	}

	private Task<Fixture> ConfirmStoredAssessmentAsync(Fixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
		var assessment = await charges.GetAsync(fixture.AssessmentId);
		var yearEvidence = JsonSerializer.Serialize(new { fixture.YearId, fixture.AssessmentId,
			ReviewedBy = fixture.ActorId, Reason = "Checked the original assessment register." });
		var dateEvidence = JsonSerializer.Serialize(new { fixture.AssessmentDate, fixture.DueDate,
			ReviewedBy = fixture.ActorId, Reason = "Checked the original school-date register." });
		assessment.ConfirmSourceContext(fixture.YearId, LibraryAcademicYearProvenance.ReviewedLegacy,
			yearEvidence, fixture.AssessmentDate, fixture.DueDate, LibraryCivilDateProvenance.ReviewedLegacy,
			LibraryCivilDateProvenance.ReviewedLegacy, dateEvidence, "INR");
		await charges.UpdateAsync(assessment, true);
		return fixture with { Version = assessment.GetAssessmentVersion() };
	});
}
