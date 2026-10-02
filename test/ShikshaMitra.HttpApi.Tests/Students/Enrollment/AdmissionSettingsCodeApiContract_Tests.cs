using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionSettingsCodeApiContract_Tests
{
	private static readonly Type[] OptionalCodeInputs =
	[
		typeof(CreateUpdateAdmissionCycleDto), typeof(CloneAdmissionCycleDto),
		typeof(CreateUpdateAdmissionWorkflowDto), typeof(CloneAdmissionWorkflowDto),
		typeof(CreateUpdateAdmissionFormDto), typeof(CloneAdmissionFormDto),
		typeof(SaveAdmissionIntakeProfileDto)
	];

	public static IEnumerable<object[]> InputTypes() => OptionalCodeInputs.Select(type => new object[] { type });

	public static IEnumerable<object[]> OptionalCodeBodies() => OptionalCodeInputs.SelectMany(type =>
		new[] { "{}", "{\"code\":null}", "{\"code\":\"\"}" }
			.Select(body => new object[] { type, body }));

	[Theory]
	[MemberData(nameof(InputTypes))]
	public void Settings_inputs_should_not_implicitly_require_code_in_mvc_metadata(Type inputType)
	{
		using var services = CreateMvcServices();
		var metadata = services.GetRequiredService<IModelMetadataProvider>()
			.GetMetadataForProperties(inputType).Single(property => property.PropertyName == "Code");
		metadata.IsRequired.ShouldBeFalse();
	}

	[Theory]
	[MemberData(nameof(OptionalCodeBodies))]
	public void Omitted_null_or_empty_code_should_not_produce_mvc_required_errors(Type inputType, string body)
	{
		using var services = CreateMvcServices();
		var model = JsonSerializer.Deserialize(body, inputType, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		inputType.GetProperty("Name")!.SetValue(model, "Fresh admission");
		var context = new ActionContext(new DefaultHttpContext { RequestServices = services },
			new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
		services.GetRequiredService<IObjectModelValidator>().Validate(context, null, string.Empty, model);
		context.ModelState.TryGetValue("Code", out var codeState);
		(codeState?.Errors.Count ?? 0).ShouldBe(0);
	}

	[Fact]
	public void Optional_code_should_keep_existing_supplied_code_length_and_format_validation()
	{
		var longCode = new string('A', 65);
		new CreateUpdateAdmissionCycleDtoValidator().Validate(new CreateUpdateAdmissionCycleDto
		{
			Name = "Admission", Code = longCode, AcademicYearId = Guid.NewGuid(),
			StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31)
		}).Errors.ShouldContain(error => error.PropertyName == "Code");
		new CloneAdmissionCycleDtoValidator().Validate(new CloneAdmissionCycleDto
		{
			Name = "Admission", Code = longCode, AcademicYearId = Guid.NewGuid(), SourceCycleId = Guid.NewGuid(),
			StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31)
		}).Errors.ShouldContain(error => error.PropertyName == "Code");
		foreach (var invalidCode in new[] { longCode, "invalid code" })
		{
			new CreateUpdateAdmissionWorkflowDtoValidator().Validate(new CreateUpdateAdmissionWorkflowDto
			{
				Name = "Fresh admission", Code = invalidCode
			}).Errors.ShouldContain(error => error.PropertyName == "Code");
			new CloneAdmissionWorkflowDtoValidator().Validate(new CloneAdmissionWorkflowDto
			{
				Name = "Fresh admission", Code = invalidCode
			}).Errors.ShouldContain(error => error.PropertyName == "Code");
			new SaveAdmissionIntakeProfileDtoValidator().Validate(new SaveAdmissionIntakeProfileDto
			{
				Name = "General inquiry", Code = invalidCode, ConfigurationJson = "{}"
			}).Errors.ShouldContain(error => error.PropertyName == "Code");
		}
	}

	private static ServiceProvider CreateMvcServices()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddControllers();
		return services.BuildServiceProvider();
	}
}
