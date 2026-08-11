using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Records;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentCertificateApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/students/records/certificates";

	public StudentCertificateApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Live_Preview_Data_Without_Persisting_Certificate()
	{
		var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "CertPreview");
		var issueDate = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);

		var placeholdersResponse = await Client.GetAsync($"{BaseUrl}/placeholders");
		placeholdersResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await placeholdersResponse.Content.ReadAsStringAsync());
		var placeholders = await placeholdersResponse.Content.ReadFromJsonAsync<Result<List<string>>>(JsonOptions);
		placeholders.ShouldNotBeNull();
		placeholders.Data.ShouldContain("StudentName");
		placeholders.Data.ShouldContain("Reason");

		var templateResponse = await Client.PutAsJsonAsync(
			$"{BaseUrl}/templates/{StudentCertificateType.Bonafide}",
			new StudentCertificateTemplateDto
			{
				CertificateType = StudentCertificateType.Bonafide,
				HeaderContent = "Certificate {{CertificateNo}} for {{StudentName}}",
				FooterContent = "Reason: {{Reason}}",
				LogoUrl = "https://static.example.test/school-logo.png",
				SchoolAddress = "Main Campus"
			},
			JsonOptions);
		templateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await templateResponse.Content.ReadAsStringAsync());

		var previewResponse = await Client.PostAsJsonAsync(
			$"{BaseUrl}/preview",
			new StudentCertificatePreviewRequestDto
			{
				StudentId = student.StudentId,
				CertificateType = StudentCertificateType.Bonafide,
				IssueDate = issueDate,
				CertificateFields = new Dictionary<string, string?>
				{
					["Reason"] = "Scholarship application"
				}
			},
			JsonOptions);

		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = await previewResponse.Content.ReadFromJsonAsync<Result<StudentCertificateResponseDto>>(JsonOptions);
		preview.ShouldNotBeNull();
		preview.Data.ShouldNotBeNull();
		preview.Data.CertificateId.ShouldBeNull();
		preview.Data.CertificateNo.ShouldStartWith("BON-2026-");
		preview.Data.Status.ShouldBe("Draft");
		preview.Data.Student.StudentId.ShouldBe(student.StudentId);
		preview.Data.Student.StudentName.ShouldContain(student.FirstName);
		preview.Data.CertificateData["Reason"].ShouldBe("Scholarship application");
		preview.Data.PlaceholderValues["Reason"].ShouldBe("Scholarship application");
		preview.Data.Template.RenderedHeaderContent.ShouldContain(student.FirstName);
		preview.Data.Template.RenderedHeaderContent.ShouldContain(preview.Data.CertificateNo);
		preview.Data.Template.RenderedFooterContent.ShouldContain("Scholarship application");
		preview.Data.Template.LogoUrl.ShouldBe("https://static.example.test/school-logo.png");
		preview.Data.Template.SchoolAddress.ShouldBe("Main Campus");
		preview.Data.Organization.LogoUrl.ShouldBe("https://static.example.test/school-logo.png");
		preview.Data.Organization.Address.ShouldBe("Main Campus");

		var historyResponse = await Client.GetAsync($"{BaseUrl}/students/{student.StudentId}/history");
		historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await historyResponse.Content.ReadAsStringAsync());
		var history = await historyResponse.Content.ReadFromJsonAsync<Result<List<StudentCertificateHistoryDto>>>(JsonOptions);
		history.ShouldNotBeNull();
		history.Data.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Generate_History_Duplicate_And_Pdf_Separately()
	{
		var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "CertGenerate");
		var issueDate = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);

		var generateResponse = await Client.PostAsJsonAsync(
			BaseUrl,
			new CreateStudentCertificateDto
			{
				StudentId = student.StudentId,
				CertificateType = StudentCertificateType.Character,
				IssueDate = issueDate,
				CertificateFields = new Dictionary<string, string?>
				{
					["Reason"] = "Conduct verification"
				}
			},
			JsonOptions);

		generateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await generateResponse.Content.ReadAsStringAsync());
		var generated = await generateResponse.Content.ReadFromJsonAsync<Result<StudentCertificateResponseDto>>(JsonOptions);
		generated.ShouldNotBeNull();
		generated.Data.ShouldNotBeNull();
		generated.Data.CertificateId.ShouldNotBeNull();
		generated.Data.CertificateNo.ShouldStartWith("CHA-2026-");
		generated.Data.Status.ShouldBe("Generated");

		var historyResponse = await Client.GetAsync($"{BaseUrl}/students/{student.StudentId}/history");
		historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await historyResponse.Content.ReadAsStringAsync());
		var history = await historyResponse.Content.ReadFromJsonAsync<Result<List<StudentCertificateHistoryDto>>>(JsonOptions);
		history.ShouldNotBeNull();
		history.Data.ShouldHaveSingleItem();
		history.Data[0].CertificateId.ShouldBe(generated.Data.CertificateId.Value);
		history.Data[0].SupportedActions.ShouldContain("Duplicate");
		history.Data[0].SupportedActions.ShouldContain("Download");

		var duplicateResponse = await Client.PostAsync(
			$"{BaseUrl}/{generated.Data.CertificateId}/duplicate",
			content: null);
		duplicateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await duplicateResponse.Content.ReadAsStringAsync());
		var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<Result<StudentCertificateResponseDto>>(JsonOptions);
		duplicate.ShouldNotBeNull();
		duplicate.Data.ShouldNotBeNull();
		duplicate.Data.CertificateId.ShouldNotBe(generated.Data.CertificateId);
		duplicate.Data.CertificateNo.ShouldStartWith($"CHA-{DateTimeOffset.UtcNow.Year}-");
		duplicate.Data.CertificateNo.ShouldNotBe(generated.Data.CertificateNo);
		duplicate.Data.CertificateData["Reason"].ShouldBe("Conduct verification");

		var updatedHistoryResponse = await Client.GetAsync($"{BaseUrl}/students/{student.StudentId}/history");
		updatedHistoryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updatedHistoryResponse.Content.ReadAsStringAsync());
		var updatedHistory = await updatedHistoryResponse.Content.ReadFromJsonAsync<Result<List<StudentCertificateHistoryDto>>>(JsonOptions);
		updatedHistory.ShouldNotBeNull();
		updatedHistory.Data.Count.ShouldBe(2);

		var pdfResponse = await Client.GetAsync($"{BaseUrl}/{generated.Data.CertificateId}/pdf");
		pdfResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await pdfResponse.Content.ReadAsStringAsync());
		pdfResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
		var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();
		pdfBytes.Length.ShouldBeGreaterThan(100);
		pdfBytes.Take(4).ToArray().ShouldBe(new byte[] { 0x25, 0x50, 0x44, 0x46 });
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Enforce_Student_Scope_For_Certificate_Actions()
	{
		var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "CertScope");
		var generated = await GenerateCertificateAsync(
			student.StudentId,
			StudentCertificateType.Character,
			new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero),
			"Scope seed");

		using var scopedClient = CreateClientAs(Guid.NewGuid());

		var previewResponse = await scopedClient.PostAsJsonAsync(
			$"{BaseUrl}/preview",
			new StudentCertificatePreviewRequestDto
			{
				StudentId = student.StudentId,
				CertificateType = StudentCertificateType.Character,
				IssueDate = new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero),
				CertificateFields = new Dictionary<string, string?>
				{
					["Reason"] = "Scope preview"
				}
			},
			JsonOptions);
		await ShouldBeScopeDeniedAsync(previewResponse);

		var generateResponse = await scopedClient.PostAsJsonAsync(
			BaseUrl,
			new CreateStudentCertificateDto
			{
				StudentId = student.StudentId,
				CertificateType = StudentCertificateType.Character,
				IssueDate = new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero),
				CertificateFields = new Dictionary<string, string?>
				{
					["Reason"] = "Scope generate"
				}
			},
			JsonOptions);
		await ShouldBeScopeDeniedAsync(generateResponse);

		var historyResponse = await scopedClient.GetAsync($"{BaseUrl}/students/{student.StudentId}/history");
		await ShouldBeScopeDeniedAsync(historyResponse);

		var getResponse = await scopedClient.GetAsync($"{BaseUrl}/{generated.CertificateId}");
		await ShouldBeScopeDeniedAsync(getResponse);

		var duplicateResponse = await scopedClient.PostAsync($"{BaseUrl}/{generated.CertificateId}/duplicate", content: null);
		await ShouldBeScopeDeniedAsync(duplicateResponse);

		var pdfResponse = await scopedClient.GetAsync($"{BaseUrl}/{generated.CertificateId}/pdf");
		await ShouldBeScopeDeniedAsync(pdfResponse);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Stored_Certificate_Snapshot_After_Student_And_Template_Change()
	{
		var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "CertSnapshot");
		var issueDate = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
		await UpsertTemplateAsync(
			StudentCertificateType.Bonafide,
			"Stored {{CertificateNo}} for {{StudentName}}",
			"Stored reason {{Reason}} on {{IssueDate}}",
			"https://static.example.test/original-logo.png",
			"Original Campus");

		var generated = await GenerateCertificateAsync(
			student.StudentId,
			StudentCertificateType.Bonafide,
			issueDate,
			"Stored snapshot");

		await RenameStudentAsync(student.StudentId, "MutatedCertificateName");
		await UpsertTemplateAsync(
			StudentCertificateType.Bonafide,
			"Mutated {{CertificateNo}} for {{StudentName}}",
			"Mutated reason {{Reason}}",
			"https://static.example.test/mutated-logo.png",
			"Mutated Campus");

		var stored = await GetCertificateAsync(generated.CertificateId!.Value);
		stored.CertificateNo.ShouldBe(generated.CertificateNo);
		stored.CertificateData["StudentName"].ShouldBe(generated.CertificateData["StudentName"]);
		stored.Student.StudentName.ShouldBe(generated.Student.StudentName);
		stored.Student.StudentName.ShouldNotContain("MutatedCertificateName");
		stored.Template.HeaderContent.ShouldBe("Stored {{CertificateNo}} for {{StudentName}}");
		stored.Template.RenderedHeaderContent.ShouldBe(generated.Template.RenderedHeaderContent);
		stored.Template.LogoUrl.ShouldBe("https://static.example.test/original-logo.png");
		stored.Template.SchoolAddress.ShouldBe("Original Campus");
		stored.Organization.LogoUrl.ShouldBe("https://static.example.test/original-logo.png");
		stored.Organization.Address.ShouldBe("Original Campus");

		var pdfResponse = await Client.GetAsync($"{BaseUrl}/{generated.CertificateId}/pdf");
		pdfResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await pdfResponse.Content.ReadAsStringAsync());

		var duplicateResponse = await Client.PostAsync($"{BaseUrl}/{generated.CertificateId}/duplicate", content: null);
		duplicateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await duplicateResponse.Content.ReadAsStringAsync());
		var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<Result<StudentCertificateResponseDto>>(JsonOptions);
		duplicate.ShouldNotBeNull();
		duplicate.Data.ShouldNotBeNull();
		duplicate.Data.CertificateId.ShouldNotBe(generated.CertificateId);
		duplicate.Data.CertificateNo.ShouldNotBe(generated.CertificateNo);
		duplicate.Data.Student.StudentName.ShouldBe(generated.Student.StudentName);
		duplicate.Data.CertificateData["StudentName"].ShouldBe(generated.CertificateData["StudentName"]);
		duplicate.Data.CertificateData["Reason"].ShouldBe("Stored snapshot");
		duplicate.Data.CertificateData["CertificateNo"].ShouldBe(duplicate.Data.CertificateNo);
		duplicate.Data.CertificateData["IssueDate"].ShouldBe(duplicate.Data.IssueDate.ToString("yyyy-MM-dd"));
		duplicate.Data.Template.HeaderContent.ShouldBe("Stored {{CertificateNo}} for {{StudentName}}");
		duplicate.Data.Template.RenderedHeaderContent.ShouldContain(duplicate.Data.CertificateNo);
		duplicate.Data.Template.RenderedHeaderContent.ShouldContain(generated.Student.StudentName);
		duplicate.Data.Template.RenderedHeaderContent.ShouldNotContain("MutatedCertificateName");
		duplicate.Data.Template.LogoUrl.ShouldBe("https://static.example.test/original-logo.png");
		duplicate.Data.Organization.Address.ShouldBe("Original Campus");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Generate_Distinct_Certificate_Numbers_For_Repeated_Requests()
	{
		var student = await StudentEnrollmentTestHelper.CreateStudentFixtureAsync(Client, "CertConcurrent");
		var issueDate = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);

		var responses = new List<HttpResponseMessage>();
		for (var index = 0; index < 3; index++)
		{
			responses.Add(await Client.PostAsJsonAsync(
				BaseUrl,
				new CreateStudentCertificateDto
				{
					StudentId = student.StudentId,
					CertificateType = StudentCertificateType.Leaving,
					IssueDate = issueDate,
					CertificateFields = new Dictionary<string, string?>
					{
						["Reason"] = $"Concurrent {index}"
					}
				},
				JsonOptions));
		}

		foreach (var response in responses)
		{
			response.StatusCode.ShouldBeOneOf(new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest }, await response.Content.ReadAsStringAsync());
		}

		var successfulResponses = responses
			.Where(response => response.StatusCode == HttpStatusCode.OK)
			.ToList();
		var conflictResponses = responses
			.Where(response => response.StatusCode == HttpStatusCode.BadRequest)
			.ToList();

		foreach (var conflictResponse in conflictResponses)
		{
			var content = await conflictResponse.Content.ReadAsStringAsync();
			content.ShouldContain("ST:Records:050010");
		}

		var bodies = await Task.WhenAll(successfulResponses.Select(response =>
			response.Content.ReadFromJsonAsync<Result<StudentCertificateResponseDto>>(JsonOptions)));
		var certificateNumbers = bodies
			.Select(body =>
			{
				body.ShouldNotBeNull();
				body.Data.ShouldNotBeNull();
				return body.Data.CertificateNo;
			})
			.ToList();

		(certificateNumbers.Count + conflictResponses.Count).ShouldBe(responses.Count);
		certificateNumbers.Distinct().Count().ShouldBe(certificateNumbers.Count);
		certificateNumbers.ShouldAllBe(number => number.StartsWith("LC-2026-", StringComparison.Ordinal));
	}

	private async Task<StudentCertificateResponseDto> GenerateCertificateAsync(
		Guid studentId,
		StudentCertificateType certificateType,
		DateTimeOffset issueDate,
		string reason)
	{
		var response = await Client.PostAsJsonAsync(
			BaseUrl,
			new CreateStudentCertificateDto
			{
				StudentId = studentId,
				CertificateType = certificateType,
				IssueDate = issueDate,
				CertificateFields = new Dictionary<string, string?>
				{
					["Reason"] = reason
				}
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<Result<StudentCertificateResponseDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Data.ShouldNotBeNull();
		body.Data.CertificateId.ShouldNotBeNull();
		return body.Data;
	}

	private async Task<StudentCertificateResponseDto> GetCertificateAsync(Guid certificateId)
	{
		var response = await Client.GetAsync($"{BaseUrl}/{certificateId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<Result<StudentCertificateResponseDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Data.ShouldNotBeNull();
		return body.Data;
	}

	private async Task UpsertTemplateAsync(
		StudentCertificateType certificateType,
		string headerContent,
		string footerContent,
		string logoUrl,
		string schoolAddress)
	{
		var templateResponse = await Client.PutAsJsonAsync(
			$"{BaseUrl}/templates/{certificateType}",
			new StudentCertificateTemplateDto
			{
				CertificateType = certificateType,
				HeaderContent = headerContent,
				FooterContent = footerContent,
				LogoUrl = logoUrl,
				SchoolAddress = schoolAddress
			},
			JsonOptions);

		templateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await templateResponse.Content.ReadAsStringAsync());
	}

	private async Task RenameStudentAsync(Guid studentId, string firstName)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var student = await studentRepository.GetInquiryForUpdateAsync(studentId)
			              ?? throw new InvalidOperationException($"Student not found: {studentId}");

			student.UpdateGeneralInformation(firstName, student.MiddleName, student.LastName);

			await studentRepository.UpdateAsync(student, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private static async Task ShouldBeScopeDeniedAsync(HttpResponseMessage response)
	{
		var content = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBeOneOf(new[] { HttpStatusCode.BadRequest, HttpStatusCode.Forbidden }, content);
		content.ShouldContain("UserScope:AccessDenied");
	}
}
