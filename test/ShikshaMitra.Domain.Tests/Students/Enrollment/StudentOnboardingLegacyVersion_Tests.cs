using System;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentOnboardingLegacyVersion_Tests
{
    [Theory]
    [InlineData("grNumber")]
    [InlineData("rollNumber")]
    public void Legacy_active_rules_must_preserve_history_and_publish_a_saved_child_draft(string section)
    {
        var original = "{\"id\":\"legacy\",\"isActive\":true,\"startingNumber\":1}";
        string Config(string rows) => "{\"" + section + "\":{\"configurations\":[" + rows + "]}}";
        var settings = new StudentOnboardingSettings(Guid.NewGuid(), null, null, 1, Config(original));
        settings.Update(1, Config(original));
        Should.Throw<BusinessException>(() => settings.Update(1, "{}"));
        Should.Throw<BusinessException>(() => settings.Update(1, Config(original.Replace("startingNumber\":1", "startingNumber\":20"))));
        Should.Throw<BusinessException>(() => settings.Update(1, Config(original.Replace("isActive\":true", "isActive\":false"))));
        var child = "{\"id\":\"next\",\"status\":\"draft\",\"isActive\":false,\"previousVersionId\":\"legacy\",\"version\":2,\"startingNumber\":10}";
        settings.Update(1, Config(original + "," + child));
        var superseded = original.Replace("isActive\":true", "isActive\":false,\"status\":\"superseded\"");
        settings.Update(1, Config(superseded + "," + child.Replace("status\":\"draft\",\"isActive\":false", "status\":\"published\",\"isActive\":true")));
        settings.ConfigurationJson.ShouldContain("superseded");
    }
    [Theory]
    [InlineData("grNumber")]
    [InlineData("rollNumber")]
    public void New_statusless_active_rule_must_first_be_saved_as_draft(string section)
    {
        var settings = new StudentOnboardingSettings(Guid.NewGuid(), null, null, 1, "{}");
        string Config(string status) => "{\"" + section + "\":{\"configurations\":[{\"id\":\"new\",\"isActive\":true" + status + "}]}}";
        Should.Throw<BusinessException>(() => settings.Update(1, Config("")));
        Should.Throw<BusinessException>(() => settings.Update(1, Config(",\"status\":\"published\"")));
        settings.Update(1, Config(",\"status\":\"draft\""));
        settings.Update(1, Config(",\"status\":\"published\""));
        settings.ConfigurationJson.ShouldContain("published");
    }

    [Theory]
    [InlineData("grNumber")]
    [InlineData("rollNumber")]
    public void Academic_year_override_preserves_school_published_history_but_rejects_new_active_ids(string section)
    {
        string Config(string rows) => "{\"" + section + "\":{\"configurations\":[" + rows + "]}}";
        var school = new StudentOnboardingSettings(Guid.NewGuid(), null, null, 1, "{}");
        var draft = "{\"id\":\"existing\",\"status\":\"draft\",\"isActive\":false}";
        school.Update(1, Config(draft));
        var published = draft.Replace("draft", "published").Replace("false", "true");
        school.Update(1, Config(published));
        var yearId = Guid.NewGuid();
        var inherited = StudentOnboardingSettings.CreateAcademicYearOverride(Guid.NewGuid(), school, yearId, 1, Config(published));
        inherited.AcademicYearId.ShouldBe(yearId);
        inherited.ConfigurationJson.ShouldBe(school.ConfigurationJson);
        Should.Throw<BusinessException>(() => StudentOnboardingSettings.CreateAcademicYearOverride(Guid.NewGuid(), school, yearId, 1,
            Config(published + ",{\"id\":\"new\",\"isActive\":true}")));
        Should.Throw<BusinessException>(() => StudentOnboardingSettings.CreateAcademicYearOverride(Guid.NewGuid(), school, yearId, 1, "{}"));
    }

}
