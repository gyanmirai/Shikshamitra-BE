using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormPrintAdvancedSettings_Tests
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private const string Advanced = """
		{"paper":{"orientation":"landscape","margins":{"top":10,"right":11,"bottom":12,"left":13}},
		"border":{"enabled":true,"placement":"every","style":"double","thickness":"medium","inset":4,"corners":"rounded","sectionBorders":true,"fieldBorders":false,"dividers":true},
		"style":{"palette":"school-blue","sectionFill":"#EEEEFF","borderColor":"#123456","textColor":"#111111","fontFamily":"serif","baseSize":10,"titleSize":18,"labelWeight":"semibold"},
		"branding":{"showLogo":true,"showBanner":false,"bannerPlacement":"selected","bannerPages":[1,3],"logo":{"x":0,"y":8,"width":14,"height":84,"lockAspectRatio":true}},
		"header":{"continuation":"compact","schoolName":true,"address":true,"phone":true,"email":true,"website":false,"board":true,"medium":true,"class":true,"academicYear":true,"title":"Application form","subtitle":"School intake","alignment":"center","registrationNumber":true,"date":true},
		"photo":{"placement":"header-top-right","alignment":"center","width":35,"height":45,"caption":"Student photograph","useSaved":true},
		"footer":{"placement":"every","alignment":"right","pageNumbers":true,"registrationReference":true,"schoolName":false,"formTitle":true},
		"flow":{"stepTitles":true,"sectionNumbers":true},
		"sections":{"DETAILS":{"columns":2,"spacing":"spacious","fullWidth":true,"startNewPage":true,"keepHeading":true,"repeatTableHeadings":true,"blankRows":3}},
		"fields":{"NAME":{"width":"full","labelPosition":"above","answerStyle":"line","writingLines":2,"showRequired":true,"showHelp":false,"showConditional":true}},
		"content":{"showInstructions":true,"instructionsPlacement":"section","instructionSection":"DETAILS","showChecklist":true,"checklistPlacement":"last","showDeclaration":true,"signaturePlacement":"last","parentSignature":true,"officeSignature":false,"parentSignatureImage":"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=","officeSignatureImage":"data:image/jpeg;base64,/9j/","signatureDate":true,"showOfficeUse":true,"officePlacement":"last","documentChecks":true,"remarks":true,"verificationDate":true,"stamp":true,"translations":{"en":{"instructions":"Use capital letters.","checklist":["Birth certificate"],"declaration":"I confirm the details.","parentLabel":"Parent signature","officeLabel":"School office"},"gu":{"instructions":"વિગતો ભરો"}}},
		"copies":{"unanswered":"dash","selectedChoices":"mark"}}
		""";

	[Theory]
	[InlineData("A4")]
	[InlineData("A5")]
	[InlineData("LETTER")]
	[InlineData("LEGAL")]
	public void Advanced_print_settings_roundtrip_all_tabs_and_supported_papers(string paper)
	{
		var form = Form();
		AdmissionFormMetadataStore.WriteExperience(form, Input(Advanced, paper));
		var saved = Settings(form);
		saved["paperSize"]!.GetValue<string>().ShouldBe(paper);
		JsonNode.DeepEquals(saved["advanced"], JsonNode.Parse(Advanced)).ShouldBeTrue();
	}

	[Fact]
	public void Omitted_or_null_advanced_retains_saved_settings_but_empty_object_resets()
	{
		var form = Form();
		var input = Input(Advanced);
		AdmissionFormMetadataStore.WriteExperience(form, input);
		AdmissionFormMetadataStore.WriteExperience(form, new());
		JsonNode.DeepEquals(Settings(form)["advanced"], JsonNode.Parse(Advanced)).ShouldBeTrue();
		AdmissionFormMetadataStore.WriteExperience(form, Input("null"));
		JsonNode.DeepEquals(Settings(form)["advanced"], JsonNode.Parse(Advanced)).ShouldBeTrue();
		AdmissionFormMetadataStore.WriteExperience(form, Input("{}"));
		Settings(form)["advanced"]!.AsObject().Count.ShouldBe(0);
	}

	[Theory]
	[InlineData("{\"paper\":{\"orientation\":\"diagonal\"}}")]
	[InlineData("{\"paper\":{\"margins\":{\"top\":4,\"right\":10,\"bottom\":10,\"left\":10}}}")]
	[InlineData("{\"border\":{\"placement\":\"last\"}}")]
	[InlineData("{\"border\":{\"inset\":26}}")]
	[InlineData("{\"style\":{\"baseSize\":15}}")]
	[InlineData("{\"style\":{\"sectionFill\":\"red\"}}")]
	[InlineData("{\"branding\":{\"logo\":{\"x\":90,\"y\":0,\"width\":20,\"height\":30}}}")]
	[InlineData("{\"branding\":{\"banner\":{\"x\":0,\"y\":0,\"width\":4,\"height\":30}}}")]
	[InlineData("{\"branding\":{\"bannerPlacement\":\"last\"}}")]
	[InlineData("{\"branding\":{\"bannerPlacement\":\"selected\",\"bannerPages\":[]}}")]
	[InlineData("{\"branding\":{\"bannerPlacement\":\"selected\",\"bannerPages\":[0,2]}}")]
	[InlineData("{\"branding\":{\"bannerPlacement\":\"selected\",\"bannerPages\":[2,2]}}")]
	[InlineData("{\"header\":{\"alignment\":\"justify\"}}")]
	[InlineData("{\"photo\":{\"width\":19}}")]
	[InlineData("{\"photo\":{\"alignment\":\"justify\"}}")]
	[InlineData("{\"footer\":{\"placement\":\"middle\"}}")]
	[InlineData("{\"sections\":{\"DETAILS\":{\"columns\":5}}}")]
	[InlineData("{\"sections\":{\"DETAILS\":{\"blankRows\":31}}}")]
	[InlineData("{\"fields\":{\"NAME\":{\"writingLines\":9}}}")]
	[InlineData("{\"fields\":{\"NAME\":{\"answerStyle\":\"html\"}}}")]
	[InlineData("{\"fields\":{\"NAME\":null}}")]
	[InlineData("{\"content\":{\"instructionsPlacement\":\"unknown\"}}")]
	[InlineData("{\"content\":{\"translations\":{\"en\":null}}}")]
	[InlineData("{\"content\":{\"parentSignatureImage\":\"data:text/plain;base64,AA==\"}}")]
	[InlineData("{\"content\":{\"officeSignatureImage\":\"data:image/png;base64,not base64!\"}}")]
	[InlineData("{\"copies\":{\"unanswered\":\"guess\"}}")]
	public void Invalid_advanced_settings_are_rejected_before_metadata_changes(string advanced)
	{
		var form = Form();
		Should.Throw<BusinessException>(() => AdmissionFormMetadataStore.WriteExperience(form, Input(advanced)))
			.Code.ShouldBe(AdmissionFormErrorCodes.InvalidDefinition);
		form.ExtraProperties.ShouldBeEmpty();
	}

	[Fact]
	public void Long_text_and_large_collections_are_bounded()
	{
		foreach (var advanced in new[]
		{
			new JsonObject { ["header"] = new JsonObject { ["title"] = new string('x', 129) } },
			new JsonObject { ["content"] = new JsonObject { ["translations"] = new JsonObject { ["en"] = new JsonObject { ["instructions"] = new string('x', 2001) } } } },
			new JsonObject { ["content"] = new JsonObject { ["parentSignatureImage"] = "data:image/png;base64," + Convert.ToBase64String(new byte[5 * 1024 * 1024 + 1]) } },
			new JsonObject { ["content"] = new JsonObject { ["translations"] = new JsonObject { ["en"] = new JsonObject { ["checklist"] = new JsonArray(System.Linq.Enumerable.Range(0, 21).Select(index => (JsonNode?)JsonValue.Create($"Item {index}")).ToArray()) } } } },
			new JsonObject { ["sections"] = Collection(129) },
			new JsonObject { ["fields"] = Collection(513) }
		})
			Should.Throw<BusinessException>(() => AdmissionFormMetadataStore.WriteExperience(Form(), Input(advanced.ToJsonString())));
	}

	[Fact]
	public void Nullable_optional_groups_are_safe_and_advanced_snapshots_are_independent()
	{
		var source = Form();
		AdmissionFormMetadataStore.WriteExperience(source, Input("""{"paper":null,"branding":null,"sections":null,"fields":null,"content":{"translations":null}}"""));
		AdmissionFormMetadataStore.WriteExperience(source, Input(Advanced));
		var copied = Form();
		AdmissionFormMetadataStore.CopyExperience(source, copied);
		var definition = AdmissionFormAppService.Map(source, [], []);
		definition.Sections = [new() { Id = Guid.NewGuid(), FormDefinitionId = definition.Id, Code = "DETAILS", Name = "Details" }];
		var instance = new AdmissionFormInstance(Guid.NewGuid(), null, Guid.NewGuid(), definition.Id, definition.Version);
		AdmissionEffectiveFormStore.Freeze(instance, new() { Definition = definition });
		AdmissionFormMetadataStore.WriteExperience(source, Input("{}"));
		JsonNode.DeepEquals(Settings(copied)["advanced"], JsonNode.Parse(Advanced)).ShouldBeTrue();
		var frozen = JsonSerializer.SerializeToNode(AdmissionEffectiveFormStore.Read(instance)!.Definition, JsonOptions)!;
		JsonNode.DeepEquals(frozen["printSettings"]!["advanced"], JsonNode.Parse(Advanced)).ShouldBeTrue();
		AdmissionFormVersionComparer.Compare(definition, AdmissionFormAppService.Map(source, [], []))
			.ShouldContain("Print settings changed");
	}

	private static JsonObject Collection(int count)
	{
		var result = new JsonObject();
		for (var index = 0; index < count; index++) result[$"CODE_{index}"] = new JsonObject();
		return result;
	}
	private static AdmissionFormDefinition Form() => new(Guid.NewGuid(), null, "Registration", "REG", AdmissionFormLayoutType.SinglePage);
	private static CreateUpdateAdmissionFormDto Input(string advanced, string paper = "A4") => JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>(
		"{\"printSettings\":{\"paperSize\":\"" + paper + "\",\"advanced\":" + advanced + "}}", JsonOptions)!;
	private static JsonNode Settings(AdmissionFormDefinition form) => JsonSerializer.SerializeToNode(AdmissionFormAppService.Map(form, [], []), JsonOptions)!["printSettings"]!;
}
