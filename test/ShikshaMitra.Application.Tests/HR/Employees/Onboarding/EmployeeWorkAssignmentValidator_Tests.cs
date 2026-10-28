using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicOrganizationOptions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class EmployeeWorkAssignmentValidator_Tests
{
	[Theory]
	[InlineData(EmployeeWorkAssignmentModes.SchoolWide)]
	[InlineData(EmployeeWorkAssignmentModes.DecideLater)]
	public async Task Agnostic_assignment_needs_no_academic_setup_or_access_service(string mode)
	{
		var validator = new EmployeeWorkAssignmentValidator(null!, null!);
		var result = await validator.ValidateAsync(new() { Mode = mode });
		result.OrganizationIds.ShouldBeEmpty();
		result.AcademicYearId.ShouldBeNull();
		result.Mode.ShouldBe(mode);
	}

	[Theory]
	[InlineData("tenantWide", false)]
	[InlineData(EmployeeWorkAssignmentModes.SchoolWide, true)]
	[InlineData(EmployeeWorkAssignmentModes.DecideLater, true)]
	[InlineData(EmployeeWorkAssignmentModes.SpecificOrganizations, false)]
	public async Task Invalid_mode_and_shape_are_rejected_before_lookup(string mode, bool includeId)
	{
		var validator = new EmployeeWorkAssignmentValidator(null!, null!);
		await Should.ThrowAsync<AbpValidationException>(() => validator.ValidateAsync(new()
		{
			Mode = mode, OrganizationIds = includeId ? [Guid.NewGuid()] : []
		}));
	}

	[Fact]
	public async Task Duplicate_organizations_are_rejected()
	{
		var id = Guid.NewGuid();
		await Should.ThrowAsync<AbpValidationException>(() => new EmployeeWorkAssignmentValidator(null!, null!).ValidateAsync(new()
		{
			Mode = EmployeeWorkAssignmentModes.SpecificOrganizations, AcademicYearId = Guid.NewGuid(), OrganizationIds = [id, id]
		}));
	}

	[Fact]
	public async Task Validates_against_actor_catalogue_each_time_and_refreshes_server_labels()
	{
		var tenantId = Guid.NewGuid();
		var yearId = Guid.NewGuid();
		var first = Guid.NewGuid();
		var second = Guid.NewGuid();
		var years = Substitute.For<IAcademicYearRepository>();
		var year = new AcademicYear(yearId, tenantId, "2026-27", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31));
		years.FindAsync(yearId).Returns(year);
		var catalogue = Substitute.For<IAcademicOrganizationOptionsAppService>();
		var options = new ListResultDto<AcademicOrganizationOptionDto>([
			new() { Id = first, Label = "CBSE - English - Primary", AcademicYearId = yearId },
			new() { Id = second, Label = "GSEB - Gujarati - Secondary", AcademicYearId = yearId }
		]);
		catalogue.GetListAsync(Arg.Is<AcademicOrganizationOptionsInput>(x => x.AcademicYearId == yearId)).Returns(options);
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(tenantId);
		using var services = new ServiceCollection().AddSingleton(tenant).BuildServiceProvider();
		var validator = new EmployeeWorkAssignmentValidator(catalogue, years) { LazyServiceProvider = new AbpLazyServiceProvider(services) };
		var input = new EmployeeWorkAssignmentDto { Mode = EmployeeWorkAssignmentModes.SpecificOrganizations, AcademicYearId = yearId, OrganizationIds = [first, second] };
		var result = await validator.ValidateAsync(input);
		result.OrganizationLabels.ShouldBe(["CBSE - English - Primary", "GSEB - Gujarati - Secondary"]);
		// A deleted, foreign-year, foreign-tenant or out-of-scope stage is absent from this catalogue.
		catalogue.GetListAsync(Arg.Any<AcademicOrganizationOptionsInput>()).Returns(new ListResultDto<AcademicOrganizationOptionDto>([]));
		await Should.ThrowAsync<AbpValidationException>(() => validator.ValidateAsync(input));
		await catalogue.Received(2).GetListAsync(Arg.Any<AcademicOrganizationOptionsInput>());
	}

	[Fact]
	public async Task Foreign_tenant_or_deleted_year_is_rejected_before_catalogue()
	{
		var yearId = Guid.NewGuid();
		var years = Substitute.For<IAcademicYearRepository>();
		years.FindAsync(yearId).Returns(new AcademicYear(yearId, Guid.NewGuid(), "Foreign", new DateTime(2026, 1, 1), new DateTime(2027, 1, 1)));
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(Guid.NewGuid());
		using var services = new ServiceCollection().AddSingleton(tenant).BuildServiceProvider();
		var validator = new EmployeeWorkAssignmentValidator(null!, years) { LazyServiceProvider = new AbpLazyServiceProvider(services) };
		var input = new EmployeeWorkAssignmentDto { Mode = EmployeeWorkAssignmentModes.SpecificOrganizations, AcademicYearId = yearId, OrganizationIds = [Guid.NewGuid()] };
		await Should.ThrowAsync<AbpValidationException>(() => validator.ValidateAsync(input));
		years.FindAsync(yearId).Returns((AcademicYear?)null);
		await Should.ThrowAsync<AbpValidationException>(() => validator.ValidateAsync(input));
	}
}
