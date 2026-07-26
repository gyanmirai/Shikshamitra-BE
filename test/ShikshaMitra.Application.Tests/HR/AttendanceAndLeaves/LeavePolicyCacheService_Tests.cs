using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeavePolicyCacheService_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly LeavePolicyCacheService _cacheService;

	public LeavePolicyCacheService_Tests()
	{
		_cacheService = GetRequiredService<LeavePolicyCacheService>();
	}

	[Fact]
	public async Task Clear_Contract_Should_Rotate_All_Page_State_And_Search_Cache_Keys()
	{
		var academicYearId = Guid.NewGuid();
		var variants = new (int Page, int PageSize, string? Search, bool? ArchivedOnly)[]
		{
			(1, 10, null, false),
			(11, 25, null, true),
			(37, 500, null, null),
			(7, 50, "date-like policy 2026-01-01", false)
		};
		var oldKeys = new List<string>();

		foreach (var variant in variants)
		{
			var key = await _cacheService.GetContractCacheKeyAsync(
				academicYearId,
				variant.Page,
				variant.PageSize,
				variant.Search,
				variant.ArchivedOnly,
				CancellationToken.None);
			oldKeys.Add(key);
			await _cacheService.SetContractAsync(
				key,
				new LeavePolicyListResponse
				{
					CurrentPage = variant.Page,
					PageSize = variant.PageSize,
					TotalPages = 1
				},
				CancellationToken.None);
		}

		await _cacheService.ClearContractAsync(academicYearId);

		for (var index = 0; index < variants.Length; index++)
		{
			var variant = variants[index];
			var newKey = await _cacheService.GetContractCacheKeyAsync(
				academicYearId,
				variant.Page,
				variant.PageSize,
				variant.Search,
				variant.ArchivedOnly,
				CancellationToken.None);

			newKey.ShouldNotBe(oldKeys[index]);
			(await _cacheService.GetContractAsync(newKey, CancellationToken.None)).ShouldBeNull();
		}
	}

	[Fact]
	public async Task Clear_Active_Should_Rotate_Unbounded_Page_Cache_Keys()
	{
		const int skipCount = 250_000;
		const int maxResultCount = 37;
		var oldKey = await _cacheService.GetActiveCacheKeyAsync(skipCount, maxResultCount);
		await _cacheService.SetActiveAsync(oldKey, new PagedResultDto<LeavePolicyDto>());

		await _cacheService.ClearActiveAsync();

		var newKey = await _cacheService.GetActiveCacheKeyAsync(skipCount, maxResultCount);
		newKey.ShouldNotBe(oldKey);
		(await _cacheService.GetActiveAsync(newKey)).ShouldBeNull();
	}
}
