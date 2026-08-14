using System;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalaryStructureTemplateLifecycle_Tests
{
	[Fact]
	public void Should_Track_Current_Published_Version_Separately_From_Latest_Draft()
	{
		var template = new SalaryStructureTemplate(Guid.NewGuid(), Guid.NewGuid(), "Standard", null);
		template.MarkPublished(1);

		var nextVersion = template.ReserveNextVersion();

		nextVersion.ShouldBe(2);
		template.LatestVersion.ShouldBe(2);
		template.PublishedVersion.ShouldBe(1);
		template.Status.ShouldBe(SalaryTemplateStatus.Published);
	}

	[Fact]
	public void Should_Keep_Published_Version_Immutable()
	{
		var version = new SalaryStructureTemplateVersion(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, []);
		version.Publish(DateTime.UtcNow);

		var exception = Should.Throw<BusinessException>(() => version.SetRules([]));

		exception.Code.ShouldBe("HR:Payroll:SalaryTemplateVersionImmutable");
	}

	[Fact]
	public void Should_Not_Set_Draft_Template_As_Default()
	{
		var template = new SalaryStructureTemplate(Guid.NewGuid(), Guid.NewGuid(), "Draft", null);

		var exception = Should.Throw<BusinessException>(() => template.SetDefault(true));

		exception.Code.ShouldBe("HR:Payroll:SalaryTemplateMustBePublished");
	}
}
