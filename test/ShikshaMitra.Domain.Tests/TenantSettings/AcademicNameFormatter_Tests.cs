using System;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class AcademicNameFormatter_Tests
{
	[Theory]
	[InlineData(AcademicTextCase.Default, "  sECondary\tSTAGE  ", "  sECondary\tSTAGE  ")]
	[InlineData(AcademicTextCase.Capitalize, "  sECondary\tSTAGE  ", "Secondary Stage")]
	[InlineData(AcademicTextCase.CamelCase, "  sECondary\tSTAGE  ", "secondaryStage")]
	[InlineData(AcademicTextCase.PascalCase, "  sECondary\tSTAGE  ", "SecondaryStage")]
	[InlineData(AcademicTextCase.Capitalize, "ÉCOLE\u00a0PRIMAIRE", "École Primaire")]
	[InlineData(AcademicTextCase.CamelCase, "ગુજરાતી માધ્યમ", "ગુજરાતીમાધ્યમ")]
	[InlineData(AcademicTextCase.Uppercase, "  sECondary\tSTAGE  ", "  SECONDARY\tSTAGE  ")]
	[InlineData(AcademicTextCase.Uppercase, "école\u00a0κόσμος ßeta", "ÉCOLE\u00a0ΚΌΣΜΟΣ ßETA")]
	[InlineData(AcademicTextCase.Uppercase, "", "")]
	public void Selected_case_formats_words_without_changing_default_input(AcademicTextCase mode, string input, string expected) =>
		AcademicNameFormatter.Format(input, mode).ShouldBe(expected);

	[Fact]
	public void Formatting_is_applied_once_to_raw_input() =>
		AcademicNameFormatter.Format("secondary stage", AcademicTextCase.CamelCase).ShouldBe("secondaryStage");

	[Theory]
	[InlineData(-1)]
	[InlineData(5)]
	public void Unsupported_mode_is_rejected(int mode) =>
		Should.Throw<ArgumentOutOfRangeException>(() => AcademicNameFormatter.Format("Primary", (AcademicTextCase)mode));
}
