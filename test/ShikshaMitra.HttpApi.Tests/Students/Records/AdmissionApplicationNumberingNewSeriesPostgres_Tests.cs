using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Numbering_new_series_postgres_skips_persisted_and_deleted_numbers_under_concurrency()
	{
		// Use a separate school so this scenario cannot alter the other provider tests' settings.
		var school = await CreateNumberingTestSchoolAsync();
		Client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, school.ToString());
		Client.DefaultRequestHeaders.Add("__tenant", school.ToString());
		async Task<T> InSchool<T>(Func<IServiceProvider, Task<T>> action)
		{
			using var scope = factory.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(school);
			using var work = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
				.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
			var result = await action(scope.ServiceProvider);
			await work.CompleteAsync();
			return result;
		}

		var fixture = await InSchool(async services =>
		{
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Numbering provider year", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddYears(1));
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE);
			await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English");
			await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary");
			await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var standard = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Class 1", 1, null);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
			var cycle = new AdmissionCycle(Guid.NewGuid(), school, "Numbering provider cycle", "NUMBERING-SERIES", year.Id, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(30));
			await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle, true);
			var offering = new AdmissionOffering(Guid.NewGuid(), school, cycle.Id, null, board.Id, medium.Id, stage.Id, standard.Id, "NUMBERING-SERIES", true, null, null);
			await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(offering, true);
			var applications = services.GetRequiredService<IRepository<AdmissionApplication, Guid>>();
			var deleted = new AdmissionApplication(Guid.NewGuid(), school, "001", Guid.NewGuid(), null, cycle.Id, offering.Id, Guid.NewGuid(), Guid.NewGuid());
			await applications.InsertAsync(deleted, true);
			await applications.DeleteAsync(deleted, true);
			await applications.InsertAsync(new AdmissionApplication(Guid.NewGuid(), school, "003", Guid.NewGuid(), null, cycle.Id, offering.Id, Guid.NewGuid(), Guid.NewGuid()), true);
			return (CycleId: cycle.Id, OfferingId: offering.Id);
		});

		var input = new PreviewAdmissionApplicationNumberingDto
		{
			Prefix = "", Separator = "", Suffix = "", Digits = 3, StartingNumber = 1,
			YearFormat = AdmissionNumberYearFormat.None, ResetPolicy = AdmissionNumberResetPolicy.Continuous,
			BeginNewSeries = true
		};
		const string url = "/api/student/enrollment/admissions/application-numbering";
		using var previewResponse = await Client.PostAsJsonAsync(url + "/preview", input, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = (await previewResponse.Content.ReadFromJsonAsync<Result<AdmissionApplicationNumberPreviewDto>>(JsonOptions))!.Data!;
		preview.NextNumber.ShouldBe("002");
		preview.StartingNumberUnavailable.ShouldBeTrue();
		using var saved = await Client.PutAsJsonAsync(url, input, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		using var historyResponse = await Client.GetAsync(url + "/history");
		historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await historyResponse.Content.ReadAsStringAsync());
		var history = (await historyResponse.Content.ReadFromJsonAsync<Result<List<AdmissionApplicationNumberingHistoryDto>>>(JsonOptions))!.Data!;
		var revision = history.ShouldHaveSingleItem();
		revision.Version.ShouldBe(1);
		revision.Prefix.ShouldBeEmpty();
		input.ExpectedVersion = 1;
		input.BeginNewSeries = false;
		using var savedPreview = await Client.PostAsJsonAsync(url + "/preview", input, JsonOptions);
		savedPreview.StatusCode.ShouldBe(HttpStatusCode.OK, await savedPreview.Content.ReadAsStringAsync());
		(await savedPreview.Content.ReadFromJsonAsync<Result<AdmissionApplicationNumberPreviewDto>>(JsonOptions))!.Data!.NextNumber.ShouldBe("002");

		// The application insert and counter increment share each transaction and advisory lock.
		var allocated = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => InSchool(async services =>
		{
			var number = await services.GetRequiredService<AdmissionApplicationNumberingService>().AllocateAsync(fixture.CycleId);
			await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().InsertAsync(
				new AdmissionApplication(Guid.NewGuid(), school, number, Guid.NewGuid(), null, fixture.CycleId, fixture.OfferingId, Guid.NewGuid(), Guid.NewGuid()), true);
			return number;
		})));
		allocated.Order().ShouldBe(new[] { "002", "004", "005", "006", "007", "008", "009", "010" });
		(await InSchool(services => services.GetRequiredService<AdmissionApplicationNumberingService>().PreviewAsync(input))).NextNumber.ShouldBe("011");
	}

	private async Task<Guid> CreateNumberingTestSchoolAsync()
	{
		using var scope = factory.Services.CreateScope();
		using var host = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(null);
		using var work = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var school = await scope.ServiceProvider.GetRequiredService<TenantManager>().CreateAsync($"Numbering-{Guid.NewGuid():N}");
		await scope.ServiceProvider.GetRequiredService<ITenantRepository>().InsertAsync(school, true);
		await work.CompleteAsync();
		return school.Id;
	}
}
