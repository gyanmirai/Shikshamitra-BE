using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Records_preserve_inherited_questions_and_submitted_answers_from_the_frozen_registration_schema() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(inherited: true);
		var sources = await WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordRegistrationReader>().GetAsync(fixture.Student.Id));
		var source = sources.Single(x => x.ApplicationId == fixture.ApplicationId);

		source.Definition!.Sections.SelectMany(x => x.Fields)
			.Single(x => x.Code == "INHERITED_NOTE").Label.ShouldBe("Learning interests");
		source.OriginalValues["INHERITED_NOTE"].GetString().ShouldBe("Science and drawing");
		source.OriginalValues["BLOOD"].GetString().ShouldBe("B+");
	});
}
