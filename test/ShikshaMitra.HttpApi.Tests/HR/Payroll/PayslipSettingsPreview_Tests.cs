using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using QuestPDF.Infrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class PayslipSettingsPreview_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Url = "/api/payroll/payslip-settings/preview";

	[Fact]
	public async Task View_permission_renders_synthetic_pdf_without_persisting_settings()
	{
		using var api = Factory.CreateClient();
		api.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View);
		var before = await api.GetStringAsync("/api/payroll/payslip-settings");
		var response = await api.PostAsJsonAsync(Url, new SalaryDocumentPresentation { Layout = "Compact" });
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
		response.Headers.CacheControl!.NoStore.ShouldBeTrue();
		PayslipPdfTextAssertions.AssertReadable(await response.Content.ReadAsByteArrayAsync(), "Sample Employee (Synthetic)", "SAMPLE-001", "Rs. 38,000");
		(await api.GetStringAsync("/api/payroll/payslip-settings")).ShouldBe(before);
	}

	[Theory]
	[InlineData("__none__")]
	[InlineData(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add)]
	public async Task Missing_view_permission_is_denied(string permissions)
	{
		using var api = Factory.CreateClient();
		api.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permissions);
		(await api.PostAsJsonAsync(Url, new SalaryDocumentPresentation())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Anonymous_host_and_invalid_presentation_are_rejected()
	{
		using var anonymous = Factory.CreateClient();
		anonymous.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		(await anonymous.PostAsJsonAsync(Url, new SalaryDocumentPresentation())).IsSuccessStatusCode.ShouldBeFalse();
		using var host = Factory.CreateClient();
		host.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		(await host.PostAsJsonAsync(Url, new SalaryDocumentPresentation())).IsSuccessStatusCode.ShouldBeFalse();
		(await Client.PostAsJsonAsync(Url, new SalaryDocumentPresentation { Layout = "Invalid" })).IsSuccessStatusCode.ShouldBeFalse();
	}

	[Theory]
	[InlineData("Compact", false)]
	[InlineData("Compact", true)]
	[InlineData("Detailed", false)]
	[InlineData("Detailed", true)]
	public void Sample_uses_canonical_renderer_with_optional_fields_and_long_school_identity(string layout, bool show)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var options = new SalaryDocumentPresentation { Layout = layout, ShowStaffDetails = show, ShowBankDetails = show,
			ShowAttendance = show, ShowEarningsBreakdown = show, ShowZeroDeductions = show };
		var model = PayslipSettingsPreview.Create(string.Join(" ", Enumerable.Repeat("Synthetic School", 6)),
			"Synthetic campus address with a long locality, city, state and postal code", options);
		model.Earnings.Sum(x => x.Amount).ShouldBe(model.NetPay + model.TotalDeductions);
		var pdf = new PayslipPdfService().Generate(model);
		PayslipPdfTextAssertions.AssertReadable(pdf, "Sample Employee (Synthetic)", "Rs. 38,000");
		var artifacts = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		if (!string.IsNullOrWhiteSpace(artifacts))
		{
			Directory.CreateDirectory(artifacts);
			File.WriteAllBytes(Path.Combine(artifacts, $"settings-{layout}-{show}.pdf"), pdf);
		}
	}

	[Fact]
	public void Customization_matrix_keeps_synthetic_identity_and_totals()
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var artifacts = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		foreach (var layout in new[] { "Compact", "Detailed" })
		for (var mask = 0; mask < 32; mask++)
		{
			var options = new SalaryDocumentPresentation { Layout = layout,
				ShowStaffDetails = (mask & 1) != 0, ShowBankDetails = (mask & 2) != 0,
				ShowAttendance = (mask & 4) != 0, ShowEarningsBreakdown = (mask & 8) != 0, ShowZeroDeductions = (mask & 16) != 0 };
			var model = PayslipSettingsPreview.Create("Synthetic Academy", "Sample Road, Sample City, Gujarat, India", options);
			var pdf = new PayslipPdfService().Generate(model);
			PayslipPdfTextAssertions.AssertReadable(pdf, "Sample Employee (Synthetic)", "SAMPLE-001", "Rs. 38,000");
			using var stream = new MemoryStream(pdf);
			using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
			var text = LogicalText(document.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot")?.Elements["/K"]);
			text.Contains("Teaching (sample)", StringComparison.Ordinal).ShouldBe(options.ShowStaffDetails);
			text.Contains("Sample Bank", StringComparison.Ordinal).ShouldBe(options.ShowBankDetails);
			text.Contains("Earnings and attendance", StringComparison.Ordinal).ShouldBe(options.ShowAttendance);
			text.Contains("Basic salary", StringComparison.Ordinal).ShouldBe(options.ShowEarningsBreakdown);
			text.Contains("Other deductions", StringComparison.Ordinal).ShouldBe(options.ShowZeroDeductions);
			if (!string.IsNullOrWhiteSpace(artifacts))
			{
				Directory.CreateDirectory(artifacts);
				File.WriteAllBytes(Path.Combine(artifacts, $"sample-{layout}-{mask}.pdf"), pdf);
			}
		}
	}

	[Fact]
	public void School_logo_bytes_are_embedded_in_the_same_sample_renderer()
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var model = PayslipSettingsPreview.Create("Synthetic Academy", "Sample Road", new()) with
		{
			LogoBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=")
		};
		using var stream = new MemoryStream(new PayslipPdfService().Generate(model));
		using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
		var objects = document.Pages[0].Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/XObject");
		objects.ShouldNotBeNull();
		objects!.Elements.Keys.Any(key => objects.Elements.GetDictionary(key)?.Elements.GetName("/Subtype") == "/Image").ShouldBeTrue();
	}

	private static string LogicalText(PdfItem? item)
	{
		if (item is PdfReference reference) item = reference.Value;
		if (item is PdfArray array) return string.Join("\n", array.Elements.Select(LogicalText));
		if (item is not PdfDictionary node) return string.Empty;
		return node.Elements.GetString("/Alt") + "\n" + LogicalText(node.Elements["/K"]);
	}
}
