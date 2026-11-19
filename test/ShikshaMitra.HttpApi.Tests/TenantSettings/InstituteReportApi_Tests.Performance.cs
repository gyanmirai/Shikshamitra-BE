using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.TenantSettings.Reports;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Data;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class InstituteReportApi_Tests
{
	[Theory]
	[InlineData("27 Oct 2026", "Asia/Kolkata")]
	[InlineData("27-10-2026", "Asia/Kolkata")]
	[InlineData("10:05 AM", "Asia/Kolkata")]
	[InlineData("4:35 am", "UTC")]
	[InlineData("2026-10-27T04:35:00", "UTC")]
	[InlineData("Pending", "UTC")]
	public async Task History_search_matches_rendered_dates_aliases_and_pending(string search, string timeZone)
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var job = new InstituteReportJob(Guid.NewGuid(), tenant, user, JsonSerializer.Serialize(new InstituteReportRequest { ReportType = "shift-register" }), false)
				{ CreationTime = new DateTime(2026, 10, 27, 4, 35, 0, DateTimeKind.Utc) };
			await scope.ServiceProvider.GetRequiredService<IRepository<InstituteReportJob, Guid>>().InsertAsync(job);
			await unit.CompleteAsync();
		}
		factory.Commands.Clear();
		var response = await client.GetAsync(Url + "/history?maxResultCount=1&filter=" + Uri.EscapeDataString(search) + "&timeZone=" + Uri.EscapeDataString(timeZone));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
		result.GetProperty("totalCount").GetInt32().ShouldBe(1);
		result.GetProperty("items").GetArrayLength().ShouldBe(1);
		factory.Commands.ShouldNotContain(sql => sql.Contains("AppInstituteReportJobs", StringComparison.Ordinal)
			&& sql.Split("FROM", 2, StringSplitOptions.None)[0].Contains("RequestJson", StringComparison.Ordinal) && !sql.Contains("LIMIT", StringComparison.Ordinal));
	}

	[Fact]
	public async Task History_rejects_unknown_time_zones()
	{
		var (client, _, _) = await ClientAsync(); using var http = client;
		(await client.GetAsync(Url + "/history?filter=27%20Oct&timeZone=School/Unknown")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Theory]
	[InlineData("eq", "2.00", 1)]
	[InlineData("eq", "2.5", 0)]
	[InlineData("gt", "1.5", 1)]
	[InlineData("lt", "2.5", 1)]
	[InlineData("gt", "2", 0)]
	[InlineData("eq", "invalid", 0)]
	public async Task Numeric_class_counts_compare_integer_values_before_pagination(string op, string value, int count)
	{
		var (client, tenant, _) = await ClientAsync(); using var http = client;
		var (year, _, _) = await SeedHierarchyAsync(tenant);
		var result = await ModulePreviewAsync(client, new() { ReportType = "class-division-summary", PageSize = 1,
			Filters = new() { AcademicYearId = year }, Conditions = [new() { Field = "divisionCount", Operator = op, Value = value }] });
		result.TotalCount.ShouldBe(count); result.Rows.Count.ShouldBe(count);
	}

	[Fact]
	public async Task History_search_column_criteria_and_sort_apply_before_database_paging()
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var jobs = scope.ServiceProvider.GetRequiredService<IRepository<InstituteReportJob, Guid>>();
			for (var i = 0; i < 16; i++)
			{
				var job = new InstituteReportJob(Guid.NewGuid(), tenant, user, JsonSerializer.Serialize(new InstituteReportRequest {
					ReportType = i % 2 == 0 ? "shift-register" : "school-document-counts", Format = i % 2 == 0 ? "pdf" : "csv"
				}), false) { CreationTime = DateTime.UtcNow.AddMinutes(-i) };
				await jobs.InsertAsync(job);
			}
			await unit.CompleteAsync();
		}
		var filters = Uri.EscapeDataString("[{\"column\":\"format\",\"type\":\"equals\",\"value\":\"CSV\"},{\"column\":\"status\",\"type\":\"equals\",\"value\":\"Queued\"},{\"column\":\"status\",\"type\":\"equals\",\"value\":\"Failed\"}]");
		factory.Commands.Clear();
		var response = await client.GetAsync(Url + "/history?skipCount=3&maxResultCount=3&filter=document&sorting=creationTime%20asc&columnFilters=" + filters);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
		result.GetProperty("totalCount").GetInt32().ShouldBe(8);
		var rows = result.GetProperty("items").EnumerateArray().ToArray(); rows.Length.ShouldBe(3);
		rows.ShouldAllBe(row => row.GetProperty("reportTypeId").GetString() == "school-document-counts");
		rows.Select(row => row.GetProperty("creationTime").GetDateTime()).ShouldBeInOrder();
		factory.Commands.ShouldContain(sql => sql.Contains("AppInstituteReportJobs", StringComparison.Ordinal) && sql.Contains("LIMIT", StringComparison.Ordinal));
		(await client.GetAsync(Url + "/history?sorting=requestJson%20asc")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Room_report_does_not_reveal_a_foreign_floor_from_an_invalid_legacy_reference()
	{
		var (client, tenant, _) = await ClientAsync(); using var http = client;
		var (foreignClient, foreignTenant, _) = await ClientAsync(); using var other = foreignClient;
		Guid buildingId; Guid floorId;
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(foreignTenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var building = await scope.ServiceProvider.GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building { TenantId = foreignTenant, BuildingName = "Foreign secret block", TotalFloors = 1 }, true);
			var floor = await scope.ServiceProvider.GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor { BuildingId = building.Id, FloorName = "Foreign secret floor", TotalRoom = 1 }, true);
			buildingId = building.Id; floorId = floor.Id; await unit.CompleteAsync();
		}
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var type = await scope.ServiceProvider.GetRequiredService<IRepository<RoomType, Guid>>().InsertAsync(new RoomType { TenantId = tenant, RoomTypeName = "Classroom", ShortName = "CR" }, true);
			var room = new Room { TenantId = tenant, RoomName = "Legacy room", BuildingId = buildingId, FloorId = floorId, RoomTypeId = type.Id, Capacity = 4, TotalBenches = 2, BenchCapacity = 2 };
			room.SetProperty("LegacyImage", new string('x', 10000));
			await scope.ServiceProvider.GetRequiredService<IRepository<Room, Guid>>().InsertAsync(room); await unit.CompleteAsync();
		}
		factory.Commands.Clear();
		var report = await ModulePreviewAsync(client, new() { ReportType = "rooms-inventory", PageSize = 1 });
		report.Rows.Single()["floor"].ShouldBeNull(); report.Rows.Single()["building"].ShouldBeNull();
		report.Rows.Single()["room"].ShouldBe("Legacy room");
		factory.Commands.ShouldNotContain(sql => sql.Contains("academic_Rooms", StringComparison.Ordinal) && sql.Contains("\"ExtraProperties\"", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Legacy_custom_board_labels_survive_database_paging_and_summary_grouping()
	{
		var (client, tenant, _) = await ClientAsync(); using var http = client;
		var (year, board, _, _) = await SeedReportIdentityAsync(tenant);
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<EducationBoard, Guid>>();
			var entity = await repository.GetAsync(board); entity.SetProperty("CustomName", "Legacy Board");
			await repository.UpdateAsync(entity); await unit.CompleteAsync();
		}
		var hierarchy = await ModulePreviewAsync(client, new() { ReportType = "academic-hierarchy", PageSize = 1, Filters = new() { AcademicYearId = year } });
		hierarchy.Rows.Single()["board"].ShouldBe("Legacy Board");
		var summary = await ModulePreviewAsync(client, new() { ReportType = "class-division-summary", PageSize = 1, Filters = new() { AcademicYearId = year } });
		summary.Rows.Single()["board"].ShouldBe("Legacy Board");
		var identity = await ModulePreviewAsync(client, new() { ReportType = "school-identity-register", PageSize = 1, Filters = new() { BoardId = board } });
		identity.Rows.Single()["board"].ShouldBe("Legacy Board");
	}

	[Fact]
	public async Task Identity_register_scoped_projection_can_execute_on_the_relational_provider()
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		var (year, board, medium, organization) = await SeedReportIdentityAsync(tenant);
		factory.Scopes[user] = new UserScopeContext { UserId = user, TenantId = tenant, AcademicYearIds = [year], BoardIds = [board], MediumIds = [medium], MediumStageIds = [organization] };
		using var scope = factory.Services.CreateScope();
		using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var result = await scope.ServiceProvider.GetRequiredService<InstituteReportData>().GenerateAsync(new() { ReportType = "school-identity-register" }, user);
		result.TotalCount.ShouldBe(4);
	}

	[Fact]
	public async Task Text_conditions_preserve_ordinal_case_insensitive_unicode_matching()
	{
		var (client, tenant, _) = await ClientAsync(); using var http = client;
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var shifts = scope.ServiceProvider.GetRequiredService<IRepository<ShiftManagement, Guid>>();
			var shift = (await shifts.GetListAsync()).Single(); shift.ShiftName = "école";
			await shifts.UpdateAsync(shift); await unit.CompleteAsync();
		}
		var preview = await ModulePreviewAsync(client, new() { ReportType = "shift-register", Conditions = [new() { Field = "shift", Operator = "eq", Value = "ÉCOLE" }] });
		preview.TotalCount.ShouldBe(1); preview.Rows.Single()["shift"].ShouldBe("école");
	}

	[Fact]
	public async Task Processing_lease_is_exclusive_recoverable_and_rejects_completion_by_the_old_worker()
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		using var scope = factory.Services.CreateScope();
		using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var jobs = scope.ServiceProvider.GetRequiredService<IInstituteReportJobRepository>();
		var job = new InstituteReportJob(Guid.NewGuid(), tenant, user, "{\"ReportType\":\"shift-register\"}", false);
		await jobs.InsertAsync(job, autoSave: true);
		var first = Guid.NewGuid(); var replacement = Guid.NewGuid(); var now = DateTime.UtcNow;
		(await jobs.TryAcquireAsync(job.Id, first, now)).ShouldBeTrue();
		(await jobs.TryAcquireAsync(job.Id, replacement, now)).ShouldBeFalse();
		(await jobs.RenewAsync(job.Id, first, now.AddMinutes(8))).ShouldBeTrue();
		(await jobs.TryAcquireAsync(job.Id, replacement, now.AddMinutes(11))).ShouldBeFalse();
		(await jobs.ExpireAsync(job.Id, now.AddMinutes(11))).ShouldBeFalse();
		(await jobs.TryAcquireAsync(job.Id, replacement, now.AddMinutes(19))).ShouldBeTrue();
		(await jobs.CompleteAsync(job.Id, first, "old-worker-file", now.AddMinutes(19))).ShouldBeFalse();
		(await jobs.CompleteAsync(job.Id, replacement, "replacement-file", now.AddMinutes(19))).ShouldBeTrue();
		await unit.CompleteAsync();
	}

	[Fact]
	public async Task Document_counts_are_grouped_in_sql_and_register_hydrates_only_the_requested_page()
	{
		var (client, tenant, _) = await ClientAsync(); using var http = client;
		await SeedReportDocumentsAsync(tenant);
		factory.Commands.Clear();
		var counts = await ModulePreviewAsync(client, new() { ReportType = "school-document-counts" });
		counts.Rows.First()["documentCount"].ShouldBe("2");
		factory.Commands.ShouldContain(command => command.Contains("GROUP BY", StringComparison.Ordinal));
		factory.Commands.ShouldNotContain(command => command.Contains("AppSchoolIdentity", StringComparison.Ordinal));
		factory.Commands.Clear();
		var page = await ModulePreviewAsync(client, new() { ReportType = "school-document-register", PageSize = 1, Page = 2, SortField = "attachmentCount", Descending = true });
		page.TotalCount.ShouldBe(2); page.Rows.Single()["name"].ShouldBe("NOC");
		factory.Commands.ShouldContain(command => command.Contains("AppSchoolDocuments", StringComparison.Ordinal) && command.Contains("LIMIT", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Catalogue_reuses_one_principal_within_the_request()
	{
		var (client, _, _) = await ClientAsync(); using var http = client;
		factory.Commands.Clear();
		var response = await client.GetAsync(Url + "/options");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		factory.Commands.Count(command => command.Contains("\"AbpUsers\"", StringComparison.Ordinal)).ShouldBeLessThanOrEqualTo(1);
	}

	[Fact]
	public async Task Conditional_branding_request_authorizes_metadata_without_reading_blob_bytes()
	{
		var (client, _, _) = await ClientAsync(); using var http = client;
		const string branding = "/api/institute-setup/school-identity";
		using var upload = new System.Net.Http.MultipartFormDataContent();
		upload.Add(new System.Net.Http.StringContent("0"), "kind");
		upload.Add(new System.Net.Http.ByteArrayContent(SchoolIdentityTestImage.Png()), "file", "logo.png");
		var uploaded = await client.PostAsync(branding + "/assets", upload);
		uploaded.StatusCode.ShouldBe(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
		var asset = (await uploaded.Content.ReadFromJsonAsync<SchoolIdentityAssetDto>())!;
		var saved = await client.PutAsJsonAsync(branding, new SchoolIdentityUpdateDto { Choices = new() { Logo = new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = asset.Id } } });
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		var first = await client.GetAsync(branding + "/branding/logo");
		first.StatusCode.ShouldBe(HttpStatusCode.OK);
		factory.Commands.Clear();
		using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, branding + "/branding/logo");
		request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
		var cached = await client.SendAsync(request);
		cached.StatusCode.ShouldBe(HttpStatusCode.NotModified);
		factory.Commands.ShouldNotContain(command => command.Contains("AbpBlobs", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Shift_preview_does_not_read_unrelated_catalogues_and_pages_in_sql()
	{
		var (client, _, _) = await ClientAsync(); using var http = client;
		factory.Commands.Clear();
		var response = await client.PostAsJsonAsync(Url + "/preview", new InstituteReportRequest { ReportType = "shift-register", PageSize = 1 });
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var sql = factory.Commands.ToArray();
		sql.ShouldNotContain(command => command.Contains("AppSchoolIdentityProfiles", StringComparison.Ordinal));
		sql.ShouldNotContain(command => command.Contains("academic_Buildings", StringComparison.Ordinal));
		sql.ShouldContain(command => command.Contains("ShiftManagement", StringComparison.Ordinal) && command.Contains("LIMIT", StringComparison.Ordinal));
	}

	[Fact]
	public async Task History_is_paged_compact_and_status_requests_are_bounded_and_owner_filtered()
	{
		var (client, tenant, user) = await ClientAsync(); using var http = client;
		using (var scope = factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			var jobs = scope.ServiceProvider.GetRequiredService<IRepository<InstituteReportJob, Guid>>();
			for (var i = 0; i < 12; i++) await jobs.InsertAsync(new(Guid.NewGuid(), tenant, user, JsonSerializer.Serialize(new InstituteReportRequest { ReportType = "shift-register" }), false));
			await unit.CompleteAsync();
		}
		var response = await client.GetAsync(Url + "/history?skipCount=5&maxResultCount=5");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
		body.GetProperty("totalCount").GetInt32().ShouldBe(12);
		var rows = body.GetProperty("items").EnumerateArray().ToArray(); rows.Length.ShouldBe(5);
		rows[0].TryGetProperty("request", out _).ShouldBeFalse();
		var ids = rows.Select(row => row.GetProperty("id").GetGuid()).ToArray();
		var statuses = await client.PostAsJsonAsync(Url + "/statuses", ids);
		statuses.StatusCode.ShouldBe(HttpStatusCode.OK, await statuses.Content.ReadAsStringAsync());
		JsonDocument.Parse(await statuses.Content.ReadAsStringAsync()).RootElement.GetArrayLength().ShouldBe(5);
		(await client.PostAsJsonAsync(Url + "/statuses", Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray())).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var (other, _, _) = await ClientAsync(); using var second = other;
		var denied = await other.PostAsJsonAsync(Url + "/statuses", ids);
		JsonDocument.Parse(await denied.Content.ReadAsStringAsync()).RootElement.GetArrayLength().ShouldBe(0);
	}

	[Fact]
	public async Task Recently_started_job_is_not_failed_because_it_waited_in_the_queue()
	{
		var (_, tenant, user) = await ClientAsync();
		using var scope = factory.Services.CreateScope();
		using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenant);
		Guid id;
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var job = new InstituteReportJob(Guid.NewGuid(), tenant, user, "{\"ReportType\":\"shift-register\"}", false);
			job.CreationTime = DateTime.UtcNow.AddHours(-2); job.Start(); id = job.Id;
			await scope.ServiceProvider.GetRequiredService<IRepository<InstituteReportJob, Guid>>().InsertAsync(job);
			await unit.CompleteAsync();
		}
		await scope.ServiceProvider.GetRequiredService<InstituteReportRetention>().PurgeAsync();
		using var read = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		(await scope.ServiceProvider.GetRequiredService<IRepository<InstituteReportJob, Guid>>().GetAsync(id)).Status.ShouldBe("Processing");
	}
}

public sealed class InstituteReportCommandRecorder(ConcurrentQueue<string> commands) : DbCommandInterceptor
{
	public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
	{
		commands.Enqueue(command.CommandText);
		return base.ReaderExecuting(command, eventData, result);
	}
	public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
		InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
	{
		commands.Enqueue(command.CommandText);
		return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
	}
}
