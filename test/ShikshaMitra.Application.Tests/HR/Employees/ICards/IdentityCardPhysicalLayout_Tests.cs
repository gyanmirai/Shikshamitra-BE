using ShikshaMitra.Common.IdentityCards;
using System.Text.Json;
using ShikshaMitra.HR.Employees.ICards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Xunit;

namespace ShikshaMitra.Common.IdentityCards;

public class IdentityCardPhysicalLayout_Tests
{
	[Fact]
	public void Explicit_millimetres_determine_size_independent_of_canvas()
	{
		var layout = JsonSerializer.Deserialize<IdentityCardLayout>("""{"width":856,"height":540,"widthMm":85.6,"heightMm":53.98}""", EmployeeICardLayoutJson.Options)!;
		var size = EmployeeICardPdfLayoutResolver.ResolveCardSize(layout);
		size.Width.ShouldBe(85.6f * 72 / 25.4f, 0.01f);
		size.Height.ShouldBe(53.98f * 72 / 25.4f, 0.01f);
	}

	[Fact]
	public void Crowded_sheet_reduces_capacity_without_shrinking_credentials()
	{
		var size = EmployeeICardPdfLayoutResolver.ResolveSheetLayout(10, 250, 200, new());
		size.CardWidth.ShouldBe(250);
		size.CardHeight.ShouldBe(200);
	}

	[Theory]
	[InlineData("""{"pageSize":"Letter","cardsPerPage":8}""")]
	[InlineData("""{"pageSize":"A4","cardsPerPage":11}""")]
	[InlineData("""{"pageSize":"A4","duplexFlip":"sideways"}""")]
	public void Invalid_physical_print_settings_are_rejected(string json)
	{
		Should.Throw<BusinessException>(() => EmployeeICardLayoutSchema.ResolvePrintSettings(json, null));
	}

	[Fact]
	public void Hidden_school_logo_is_not_rendered_as_a_placeholder()
	{
		var template = (EmployeeICardTemplate)System.Activator.CreateInstance(typeof(EmployeeICardTemplate), nonPublic: true)!;
		template.SetProperty("SchoolIdentityLogoMode", (int)SchoolIdentityAssetMode.Hidden);
		EmployeeICardPdfComposer.ShouldRenderElement(template, new() { Type = "logo" }, new System.Collections.Generic.Dictionary<string, bool>()).ShouldBeFalse();
	}

	[Fact]
	public void Elements_cannot_escape_the_canvas_or_repeat_ids()
	{
		Should.Throw<BusinessException>(() => EmployeeICardLayoutValidator.ParseLayout("""{"width":360,"height":230,"front":{"elements":[{"id":"name","type":"text","fieldKey":"name","x":350,"y":0,"w":100,"h":20}]}}"""));
		Should.Throw<BusinessException>(() => EmployeeICardLayoutValidator.ParseLayout("""{"width":360,"height":230,"front":{"elements":[{"id":"name","type":"text","x":0,"y":0,"w":100,"h":20},{"id":"name","type":"text","x":0,"y":30,"w":100,"h":20}]}}"""));
	}
}
