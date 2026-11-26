using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Records;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentBirthdaysPostgres_Tests(StudentRecordsApplicationPostgresTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<StudentRecordsApplicationPostgresTestFactory>
{
	[StudentRecordsApplicationPostgresFact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Birthday_pages_include_leap_observance_and_can_repeat_a_student_across_years()
	{
		var leapStudent = await SeedEnrolledStudentAsync("BirthdayOccurrenceLeap", new DateTimeOffset(2012, 2, 28, 18, 30, 0, TimeSpan.Zero));
		var regularStudent = await SeedEnrolledStudentAsync("BirthdayOccurrenceUsual", new DateTimeOffset(2012, 2, 28, 0, 0, 0, TimeSpan.Zero));
		await SeedEnrolledStudentAsync("BirthdayOccurrenceInvalidOld", DateTimeOffset.MinValue);
		await SeedEnrolledStudentAsync("BirthdayOccurrenceInvalidFuture", DateTimeOffset.MaxValue);
		var range = $"/api/students/records/birthdays?academicYearId={Factory.SeededAcademicYearId}&period=custom&search=BirthdayOccurrence";

		var first = await ReadAsync($"{range}&startDate=2023-02-28&endDate=2024-02-28&maxResultCount=2");
		first.GetProperty("totalCount").GetInt64().ShouldBe(3);
		var firstItems = first.GetProperty("items");
		firstItems.GetArrayLength().ShouldBe(2);
		firstItems[0].GetProperty("occurrenceDate").GetString().ShouldBe("2023-02-28");
		firstItems[1].GetProperty("occurrenceDate").GetString().ShouldBe("2023-02-28");
		firstItems[0].GetProperty("studentId").GetGuid().ShouldBe(leapStudent);
		firstItems[1].GetProperty("studentId").GetGuid().ShouldBe(regularStudent);
		firstItems[0].GetProperty("canOpenProfile").GetBoolean().ShouldBeFalse();
		firstItems[0].TryGetProperty("dateOfBirth", out _).ShouldBeFalse();

		var second = await ReadAsync($"{range}&startDate=2023-02-28&endDate=2024-02-28&maxResultCount=2&skipCount=2");
		second.GetProperty("totalCount").GetInt64().ShouldBe(3);
		var secondItems = second.GetProperty("items");
		secondItems.GetArrayLength().ShouldBe(1);
		secondItems[0].GetProperty("studentId").GetGuid().ShouldBe(regularStudent);
		secondItems[0].GetProperty("occurrenceDate").GetString().ShouldBe("2024-02-28");

		var leapYear = await ReadAsync($"{range}&startDate=2024-02-29&endDate=2024-02-29");
		leapYear.GetProperty("totalCount").GetInt64().ShouldBe(1);
		leapYear.GetProperty("items")[0].GetProperty("studentId").GetGuid().ShouldBe(leapStudent);

		var beforeBirth = await ReadAsync($"{range}&startDate=2010-02-28&endDate=2010-03-01");
		beforeBirth.GetProperty("totalCount").GetInt64().ShouldBe(0);
	}

	private async Task<JsonElement> ReadAsync(string url)
	{
		using var response = await Client.GetAsync(url);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
	}

	private async Task<Guid> SeedEnrolledStudentAsync(string name, DateTimeOffset dateOfBirth)
	{
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var studentId = Guid.NewGuid();
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
		var repository = services.ServiceProvider.GetRequiredService<IStudentRepository>();
		var student = Student.CreateDirectRegistration(studentId, tenantId, name, null, "Fixture",
			Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
			Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId, dateOfBirth,
			GenderEnum.Female, "9999999999");
		student.MoveToAdmission();
		student.AssignDivision(Factory.SeededDivisionId);
		student.ApproveAdmission();
		await repository.InsertAsync(student);
		await uow.CompleteAsync();
		return studentId;
	}
}
