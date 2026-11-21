using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Dashboard;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private LibraryDashboardAppService ConsumerDashboardAtUtc(Fixture owner, DateTime instant)
	{
		var services = DashboardClockServices(owner, instant);
		var service = ActivatorUtilities.CreateInstance<LibraryDashboardAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private IServiceProvider DashboardClockServices(Fixture owner, DateTime instant)
	{
		instant.Kind.ShouldBe(DateTimeKind.Utc);
		var actual = new DashboardConsumerServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
		return new DashboardConsumerClockServices(actual, instant);
	}

	private Task SetDashboardClockZoneAsync(Fixture owner, string setting, string value) => WithUnitOfWorkAsync(() =>
		GetRequiredService<ISettingManager>().SetForTenantAsync(owner.TenantId, setting, value, forceToSet: true));

	private Task AssertDashboardClockSourceAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
		source.TenantId.ShouldBe(owner.TenantId); source.AcademicYearId.ShouldBe(owner.YearId);
		source.SchoolChargeDate.ShouldBe(owner.AssessmentDate); source.SchoolDueDate.ShouldBe(owner.DueDate);
		source.ChargeDateProvenance.ShouldBe(LibraryCivilDateProvenance.ReviewedLegacy);
		source.DueDateProvenance.ShouldBe(LibraryCivilDateProvenance.ReviewedLegacy);
		source.FinanceSourceLinkId.ShouldNotBeNull();
		source.Amount.ShouldBe(100.75m); source.PaymentStatus.ShouldBe(ChargePaymentStatus.Pending);
	});

	private Task<string> CaptureDashboardClockSourceAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId, includeDetails: false);
		var link = await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
			.GetAsync(source.FinanceSourceLinkId!.Value, includeDetails: false);
		return System.Text.Json.JsonSerializer.Serialize(new
		{
			source.Id, source.TenantId, source.MemberId, source.MemberRole, source.ChargeCategoryId, source.BookIssueId,
			source.Amount, source.PaymentStatus, source.ChargeDate, source.DueDate,
			source.SchoolChargeDate, source.SchoolDueDate, source.ChargeDateProvenance, source.DueDateProvenance,
			source.AcademicYearId, source.AcademicYearProvenance, source.AcademicYearEvidenceJson,
			source.SourceDateEvidenceJson, source.Currency, source.Remarks, source.FinanceSourceLinkId,
			Version = source.GetAssessmentVersion(), link.SourceSnapshotJson, link.SourceFingerprint
		});
	});

	private static void AssertDashboardClockMoney(LibraryDashboardOverviewDto overview)
	{
		overview.Alerts.Where(alert => alert.Type == "pendingCharges").ShouldHaveSingleItem().Count.ShouldBe(1,
			"The real collectible balance remains actionable even when the Library as-of date is unavailable.");
		var summary = DashboardFinanceSummary(overview);
		summary.GetProperty("verifiedCount").GetInt32().ShouldBe(1);
		summary.GetProperty("needsReviewCount").GetInt32().ShouldBe(0);
		summary.GetProperty("unpostedCount").GetInt32().ShouldBe(0);
		var totals = summary.GetProperty("currencyTotals").EnumerateArray().ShouldHaveSingleItem();
		totals.GetProperty("currency").GetString().ShouldBe("INR");
		totals.GetProperty("originalAmountText").GetString().ShouldBe("100.75");
		totals.GetProperty("settledAmountText").GetString().ShouldBe("40.25");
		totals.GetProperty("financialOutstandingAmountText").GetString().ShouldBe("60.50");
		totals.GetProperty("collectibleAmountText").GetString().ShouldBe("60.50");
	}

	private sealed class DashboardConsumerClockServices(IServiceProvider inner, DateTime instant) : IServiceProvider
	{
		private readonly Dictionary<Type, object> instances = new();
		public object? GetService(Type type)
		{
			if (type == typeof(IClock))
			{
				if (!instances.TryGetValue(type, out var value))
				{
					var clock = Substitute.For<IClock>();
					clock.Now.Returns(instant);
					instances[type] = value = clock;
				}
				return value;
			}
			if (type == typeof(LibraryCalendarService))
			{
				if (!instances.TryGetValue(type, out var value))
					instances[type] = value = ActivatorUtilities.CreateInstance<LibraryCalendarService>(this);
				return value;
			}
			return inner.GetService(type);
		}
	}
}

