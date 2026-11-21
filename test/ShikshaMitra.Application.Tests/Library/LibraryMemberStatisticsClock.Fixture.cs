using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	private async Task<LibraryMemberStatisticsDto> ReadStatisticsAsync(Guid memberId, string zone, DateTime now)
	{
		await WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>().SetForTenantAsync(
			GetRequiredService<ICurrentTenant>().Id!.Value, LibraryPolicySettingNames.CalendarTimeZone, zone, forceToSet: true));
		var services = new StatisticsClockServices(ServiceProvider, now);
		return (await WithUnitOfWorkAsync(() => services.Get().ReadAsync([memberId])))[memberId];
	}

	private sealed class StatisticsClockServices(IServiceProvider inner, DateTime now) : IServiceProvider
	{
		private readonly Dictionary<Type, object> instances = new();
		public LibraryMemberStatisticsReader Get() => (LibraryMemberStatisticsReader)GetService(typeof(LibraryMemberStatisticsReader))!;
		public BookIssueAppService GetMemberService() => (BookIssueAppService)GetService(typeof(BookIssueAppService))!;
		public object? GetService(Type type)
		{
			if (type == typeof(IClock))
			{
				if (!instances.TryGetValue(type, out var clock))
				{
					var value = Substitute.For<IClock>(); value.Now.Returns(now); instances[type] = clock = value;
				}
				return clock;
			}
			if (type == typeof(LibraryCalendarService) || type == typeof(LibraryMemberStatisticsReader) || type == typeof(BookIssueAppService))
			{
				if (!instances.TryGetValue(type, out var service))
				{
					service = ActivatorUtilities.CreateInstance(this, type);
					if (service is ApplicationService application) application.LazyServiceProvider = new AbpLazyServiceProvider(this);
					instances[type] = service;
				}
				return service;
			}
			return inner.GetService(type);
		}
	}
}
