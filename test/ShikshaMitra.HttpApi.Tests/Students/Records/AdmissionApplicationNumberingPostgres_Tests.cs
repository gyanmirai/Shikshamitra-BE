using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Numbering_postgres_http_settings_concurrent_allocations_rollback_and_tenant_isolation()
	{
		const string url = "/api/student/enrollment/admissions/application-numbering";
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		using var initial = await Client.GetAsync(url);
		initial.StatusCode.ShouldBe(HttpStatusCode.OK, await initial.Content.ReadAsStringAsync());
		(await initial.Content.ReadFromJsonAsync<Result<AdmissionApplicationNumberingDto>>(JsonOptions))!.Data!.IsConfigured.ShouldBeFalse();
		await InTenantAsync(async services =>
		{
			var legacy = AdmissionReferenceSequence.Create(Guid.NewGuid(), TenantId, DateTime.UtcNow.Year, AdmissionReferenceKind.Application);
			for (var i = 0; i < 50; i++) legacy.TakeNext();
			await services.GetRequiredService<IRepository<AdmissionReferenceSequence, Guid>>().InsertAsync(legacy, true);
			return true;
		});
		var input = new SaveAdmissionApplicationNumberingDto { Prefix = "PG", Separator = "", Suffix = "",
			YearFormat = AdmissionNumberYearFormat.None, ResetPolicy = AdmissionNumberResetPolicy.Continuous, Digits = 5, StartingNumber = 1 };
		for (var i = 0; i < 2; i++)
		{
			using var preview = await Client.PostAsJsonAsync(url + "/preview", input, JsonOptions);
			preview.StatusCode.ShouldBe(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
			(await preview.Content.ReadFromJsonAsync<Result<AdmissionApplicationNumberPreviewDto>>(JsonOptions))!.Data!.NextNumber.ShouldBe("PG00051");
		}
		using var saved = await Client.PutAsJsonAsync(url, input, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		using var stale = await Client.PutAsJsonAsync(url, input, JsonOptions);
		stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await stale.Content.ReadAsStringAsync());
		(await stale.Content.ReadAsStringAsync()).ShouldContain(AdmissionNumberingErrorCodes.Changed);
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await anonymous.PostAsJsonAsync(url + "/preview", input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		var cycleId = await InTenantAsync(async services =>
		{
			var cycle = new AdmissionCycle(Guid.NewGuid(), TenantId, "Numbering provider test", "NUMBERING-PG", factory.SeededAcademicYearId,
				DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(30));
			await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle, true);
			return cycle.Id;
		});
		// Every scope owns a distinct real transaction/connection; the production lock must serialize allocation.
		var numbers = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => InTenantAsync(services =>
			services.GetRequiredService<AdmissionApplicationNumberingService>().AllocateAsync(cycleId))));
		numbers.Distinct().Count().ShouldBe(12);
		numbers.Order().ShouldBe(Enumerable.Range(51, 12).Select(n => $"PG{n:D5}"));
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId))
		using (scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			(await scope.ServiceProvider.GetRequiredService<AdmissionApplicationNumberingService>().AllocateAsync(cycleId)).ShouldBe("PG00063");
			// Deliberately no CompleteAsync: failed registrations must roll back their reservation.
		}
		(await InTenantAsync(services => services.GetRequiredService<AdmissionApplicationNumberingService>().AllocateAsync(cycleId))).ShouldBe("PG00063");
		using var current = await Client.GetAsync(url);
		(await current.Content.ReadFromJsonAsync<Result<AdmissionApplicationNumberingDto>>(JsonOptions))!.Data!.SequenceLocked.ShouldBeTrue();
		input.ExpectedVersion = 1; input.StartingNumber = 1; input.ResetPolicy = AdmissionNumberResetPolicy.AcademicYear; input.YearFormat = AdmissionNumberYearFormat.AcademicYear;
		using var locked = await Client.PutAsJsonAsync(url, input, JsonOptions);
		locked.StatusCode.ShouldBe(HttpStatusCode.BadRequest); (await locked.Content.ReadAsStringAsync()).ShouldContain(AdmissionNumberingErrorCodes.SequenceLocked);
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
		using (var work = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			(await scope.ServiceProvider.GetRequiredService<AdmissionApplicationNumberingService>().GetAsync()).IsConfigured.ShouldBeFalse();
			await work.CompleteAsync();
		}
	}
}

public class AdmissionApplicationNumberingAuthorization_Tests
{
	[Theory]
	[InlineData(typeof(AdmissionApplicationNumberingAppService), "GetAsync", "Students.Admissions.View")]
	[InlineData(typeof(AdmissionApplicationNumberingAppService), "SaveAsync", "Students.Admissions.Add")]
	[InlineData(typeof(AdmissionApplicationNumberingAppService), "PreviewAsync", "Students.Admissions.View")]
	[InlineData(typeof(AdmissionApplicationNumberingController), "Get", "Students.Admissions.View")]
	[InlineData(typeof(AdmissionApplicationNumberingController), "Save", "Students.Admissions.Add")]
	[InlineData(typeof(AdmissionApplicationNumberingController), "Preview", "Students.Admissions.View")]
	public void Numbering_authorization_guards_owning_action_at_both_entry_points(Type type, string method, string policy)
	{
		var member = type.GetMethod(method)!;
		member.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ShouldContain(policy);
		member.GetCustomAttributes<AllowAnonymousAttribute>().ShouldBeEmpty();
		type.GetCustomAttributes<AllowAnonymousAttribute>().ShouldBeEmpty();
	}
}
