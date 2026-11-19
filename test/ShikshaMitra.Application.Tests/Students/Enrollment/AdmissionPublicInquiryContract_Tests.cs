using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPublicInquiryContract_Tests
{
	[Fact]
	public void Public_optional_request_id_is_a_nullable_uuid_without_a_required_validator()
	{
		var property = typeof(SubmitPublicAdmissionInquiryDto).GetProperty("RequestId");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(Guid?));
		property.GetCustomAttributes(typeof(RequiredAttribute), true).ShouldBeEmpty();
		var id = Guid.NewGuid();
		var input = JsonSerializer.Deserialize<SubmitPublicAdmissionInquiryDto>($"{{\"requestId\":\"{id}\"}}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		property.GetValue(input).ShouldBe(id);
		typeof(SubmitPublicAdmissionInquiryDto).GetProperties().Select(item => item.Name).ShouldNotContain("ExistingFamilyId");
	}
}
