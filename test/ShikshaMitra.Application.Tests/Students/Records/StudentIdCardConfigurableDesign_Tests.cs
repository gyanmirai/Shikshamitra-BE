using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.SchoolIdentity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public void Configurable_student_hidden_logo_suppresses_the_logo_element()
	{
		var card = new StudentIdCardDto
		{
			TemplateSnapshot = new()
			{
				SchemaVersion = 2,
				Logo = new() { Mode = SchoolIdentityAssetMode.Hidden },
				LayoutJson = """{"width":428,"height":270,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[{"id":"logo","type":"logo","x":20,"y":20,"w":50,"h":50}]}}"""
			}
		};
		StudentIdCardDesign.Artwork(card, null, null).FrontSvg.ShouldNotContain("LOGO");
	}

	[Fact]
	public Task Icard_templates_schema_two_roundtrips_and_issued_duplex_snapshots_survive_edits() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await IcardLibraryFixtureAsync(1);
		var student = fixture.Students.Single();
		var design = IcardDesign("Configurable classic identity", fixture.Year, student.Id);
		design.SchemaVersion = 2;
		design.PresetKey = "classic-identity";
		design.BackSideEnabled = true;
		design.LayoutJson = """{"orientation":"Horizontal","width":428,"height":270,"widthMm":85.6,"heightMm":53.98,"front":{"elements":[{"id":"name","type":"text","fieldKey":"StudentName","x":20,"y":20,"w":388,"h":60,"style":{"fontSize":14}}]},"back":{"elements":[{"id":"return","type":"text","fieldKey":"static","value":"Return to the school office.","x":20,"y":20,"w":388,"h":60,"style":{"fontSize":14}}]}}""";
		design.PrintSettingsJson = """{"pageSize":"Card","units":"mm","cardsPerPage":8,"sideMode":"both"}""";
		var saved = await fixture.Service.CreateTemplateAsync(design);
		saved.SchemaVersion.ShouldBe(2); saved.LayoutJson.ShouldBe(design.LayoutJson); saved.PrintSettingsJson.ShouldBe(design.PrintSettingsJson); saved.BackSideEnabled.ShouldBeTrue();
		var issued = (await fixture.Service.IssueAsync(new() { TemplateId = saved.Id, StudentIds = [student.Id], IdempotencyKey = "configurable-classic-duplex" })).Single();
		issued.TemplateSnapshot.LayoutJson.ShouldBe(design.LayoutJson);
		saved.LayoutJson = saved.LayoutJson!.Replace("Return to the school office.", "Updated office instructions.");
		await fixture.Service.UpdateTemplateAsync(saved.Id!.Value, saved);
		var frozen = (await fixture.Service.GetHistoryAsync(student.Id)).Single();
		frozen.TemplateSnapshot.LayoutJson.ShouldBe(design.LayoutJson);
		var pdf = (await fixture.Service.GetPdfAsync(issued.Id!.Value)).Content;
		Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page\b").Count.ShouldBe(2);
	});
}
