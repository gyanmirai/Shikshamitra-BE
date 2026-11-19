using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Infrastructure.Traits;
using Xunit;

namespace ShikshaMitra.Finance.Charges;

[Collection(FinanceRoutingApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ChargeTableFilterApi_Tests(FinanceRoutingApiTestFactory factory) : ChargeApiTestsBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task List_query_count_does_not_grow_per_displayed_charge()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client, isApprovalRequired: true);
		var prefix = $"Charge-list-query-{Guid.NewGuid():N}";
		for (var index = 0; index < 3; index++)
		{
			var input = ChargeTestHelper.BuildCreateDto(studentId, categoryId);
			input.ChargeName = $"{prefix}-{index}";
			var created = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}", input, JsonOptions);
			created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		}

		var url = $"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}&status=0&search={prefix}";
		using var singleCapture = new ChargeListSqlCapture();
		var single = await Client.GetFromJsonAsync<ChargeListResponse>($"{url}-0", JsonOptions);
		single.ShouldNotBeNull();
		single.TotalCount.ShouldBe(1);
		var singleCount = singleCapture.Count;
		singleCount.ShouldBeGreaterThan(0);

		using var allCapture = new ChargeListSqlCapture();
		var all = await Client.GetFromJsonAsync<ChargeListResponse>(url, JsonOptions);
		all.ShouldNotBeNull();
		all.TotalCount.ShouldBe(3);
		all.Items.Count.ShouldBe(3);
		(allCapture.Count - singleCount).ShouldBeLessThanOrEqualTo(4);
	}

	private sealed class ChargeListSqlCapture : IObserver<DiagnosticListener>,
		IObserver<KeyValuePair<string, object?>>, IDisposable
	{
		private static readonly AsyncLocal<ChargeListSqlCapture?> Current = new();
		private readonly IDisposable listener;
		private readonly ChargeListSqlCapture? previous;
		private readonly ConcurrentBag<IDisposable> subscriptions = [];
		private int count;
		public int Count => count;
		public ChargeListSqlCapture()
		{
			previous = Current.Value;
			Current.Value = this;
			listener = DiagnosticListener.AllListeners.Subscribe(this);
		}
		public void OnNext(DiagnosticListener value)
		{
			if (value.Name == "Microsoft.EntityFrameworkCore")
				subscriptions.Add(value.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name));
		}
		public void OnNext(KeyValuePair<string, object?> value)
		{
			if (Current.Value == this && value.Value is CommandExecutedEventData data &&
				data.Command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
				System.Threading.Interlocked.Increment(ref count);
		}
		public void OnCompleted() { }
		public void OnError(Exception error) { }
		public void Dispose()
		{
			Current.Value = previous;
			listener.Dispose();
			foreach (var subscription in subscriptions) subscription.Dispose();
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Amount_Header_Filter_Is_Applied_Before_Counting_And_Paging()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client, isApprovalRequired: true);
		var search = $"Filter-{Guid.NewGuid():N}";
		foreach (var amount in new[] { 50m, 150m })
		{
			var input = ChargeTestHelper.BuildCreateDto(studentId, categoryId);
			input.ChargeName = search;
			input.Amount = amount;
			var created = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}", input, JsonOptions);
			created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		}
		var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { new { column = "amount", type = "greaterThan", value = 100 } }));
		var url = $"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}&status=0&search={search}&pageSize=1";
		var filtered = await Client.GetFromJsonAsync<ChargeListResponse>($"{url}&filters={filters}", JsonOptions);
		filtered.ShouldNotBeNull();
		filtered.TotalCount.ShouldBe(1);
		filtered.Items.Single().Amount.ShouldBe(150m);
		var next = await Client.GetFromJsonAsync<ChargeListResponse>($"{url}&filters={filters}&page=2", JsonOptions);
		next.ShouldNotBeNull();
		next.TotalCount.ShouldBe(1);
		next.Items.ShouldBeEmpty();
		var cleared = await Client.GetFromJsonAsync<ChargeListResponse>(url, JsonOptions);
		cleared.ShouldNotBeNull();
		cleared.TotalCount.ShouldBe(2);
	}
}
