using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Dashboard;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reports;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	private async Task<LibraryReportPreviewDto> ReadOperationalReportAsync(string reportType, Guid? selectedYear = null,
		IServiceProvider? services = null, DateOnly? asOfDate = null)
	{
		var context = await GetRequiredService<Shared.ILibraryAccessService>().GetContextAsync(selectedYear);
		var scope = new LibraryReportExecutionScope
		{
			TenantId = GetRequiredService<ICurrentTenant>().Id, UserScope = context.UserScope,
			AllowedAcademicYearIds = context.AllowedAcademicYearIds.ToList()
		};
		var generator = (LibraryReportGenerator)(services ?? ServiceProvider).GetService(typeof(LibraryReportGenerator))!;
		return await WithUnitOfWorkAsync(() => generator.GenerateAsync(new()
		{
			ReportType = reportType, Page = 1, PageSize = 1, Filters = new() { AcademicYearId = selectedYear, AsOfDate = asOfDate }
		}, scope, exportAllRows: false));
	}

	private Task<(Book Book, BookCopy? Copy)> SeedOperationalBookAsync(StaffScopeFixture fixture, string title, Guid year, bool addCopy) =>
		WithUnitOfWorkAsync<(Book, BookCopy?)>(async () =>
		{
			var catalog = fixture.Context.Catalog;
			var tenantId = GetRequiredService<ICurrentTenant>().Id;
			var book = new Book(title, "", "School Author", "School Press", "1", "Reading", catalog.CategoryId,
				Language.English, "10+", 100m, 100, addCopy ? 1 : 0, year, tenantId);
			await GetRequiredService<IRepository<Book, Guid>>().InsertAsync(book, true);
			BookCopy? copy = null;
			if (addCopy)
			{
				copy = new(Guid.NewGuid(), book.Id, 1, $"OP-{Guid.NewGuid():N}", catalog.SectionId, catalog.ShelfId, tenantId);
				await GetRequiredService<IRepository<BookCopy, Guid>>().InsertAsync(copy, true);
			}
			return (book, copy);
		});

	private async Task<LibraryDashboardOverviewDto> ReadOperationalDashboardAsync(Guid? yearId, string zone = "UTC")
	{
		await WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>().SetForTenantAsync(
			GetRequiredService<ICurrentTenant>().Id!.Value, LibraryPolicySettingNames.CalendarTimeZone, zone, forceToSet: true));
		var services = new OperationalClockServices(ServiceProvider, new(2026, 11, 18, 0, 30, 0, DateTimeKind.Utc));
		return await WithUnitOfWorkAsync(() => services.Dashboard.GetOverviewAsync(yearId));
	}

	private sealed class OperationalClockServices(IServiceProvider inner, DateTime now) : IServiceProvider
	{
		private readonly Dictionary<Type, object> instances = new();
		public LibraryDashboardAppService Dashboard => (LibraryDashboardAppService)GetService(typeof(LibraryDashboardAppService))!;
		public object? GetService(Type type)
		{
			if (type == typeof(IClock))
			{
				if (!instances.TryGetValue(type, out var value))
				{
					var clock = Substitute.For<IClock>(); clock.Now.Returns(now); instances[type] = value = clock;
				}
				return value;
			}
			if (type == typeof(LibraryCalendarService) || type == typeof(LibraryDashboardAppService) || type == typeof(LibraryReportGenerator))
			{
				if (!instances.TryGetValue(type, out var value))
				{
					value = ActivatorUtilities.CreateInstance(this, type);
					if (value is ApplicationService application) application.LazyServiceProvider = new AbpLazyServiceProvider(this);
					instances[type] = value;
				}
				return value;
			}
			return inner.GetService(type);
		}
	}
}
