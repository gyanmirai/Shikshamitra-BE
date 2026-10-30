using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionFormInheritanceService_Tests
{
	[Fact]
	public async Task Cross_tenant_source_is_not_loaded_even_when_the_id_is_known()
	{
		var school = Guid.NewGuid();
		var source = Form(Guid.NewGuid(), "Inquiry", AdmissionFormPurpose.Inquiry);
		var registration = Registration(school, source.Id);
		var service = Service(school, [registration, source]);
		await Should.ThrowAsync<EntityNotFoundException>(() => service.ComposeAsync(registration.Id, source.Id));
	}

	[Fact]
	public async Task Unpublished_source_is_rejected_before_it_can_be_frozen()
	{
		var school = Guid.NewGuid();
		var source = Form(school, "Inquiry", AdmissionFormPurpose.Inquiry, publish: false);
		var registration = Registration(school, source.Id);
		var error = await Should.ThrowAsync<BusinessException>(() => Service(school, [registration, source]).ComposeAsync(registration.Id, source.Id));
		error.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.IncompatibleSource);
	}

	[Fact]
	public async Task Exact_source_version_requires_explicit_approval()
	{
		var school = Guid.NewGuid();
		var approved = Form(school, "Inquiry", AdmissionFormPurpose.Inquiry);
		var otherVersion = Form(school, "Inquiry", AdmissionFormPurpose.Inquiry);
		var registration = Registration(school, approved.Id);
		var result = await Service(school, [registration, approved, otherVersion]).ComposeAsync(registration.Id, otherVersion.Id);
		result.Issues.ShouldContain(issue => issue.Code == "SourceNotApproved");
	}

	[Fact]
	public async Task Source_discovery_reports_only_offerings_within_the_users_scope()
	{
		var school = Guid.NewGuid();
		var registration = Form(school, "Registration", AdmissionFormPurpose.Registration);
		var sourceA = Form(school, "InquiryA", AdmissionFormPurpose.Inquiry);
		var sourceB = Form(school, "InquiryB", AdmissionFormPurpose.Inquiry);
		var year = Guid.NewGuid();
		var cycle = new AdmissionCycle(Guid.NewGuid(), school, "Cycle", "CYCLE", year, DateTime.Today, DateTime.Today.AddMonths(2));
		var allowed = Offering(school, cycle.Id, sourceA.Id, registration.Id, "Allowed route");
		var outside = Offering(school, cycle.Id, sourceB.Id, registration.Id, "Outside route");
		var service = Service(school, [registration, sourceA, sourceB], [allowed, outside], [cycle],
			new() { AcademicYearIds = [year], WildcardDivisionStandardIds = [allowed.StandardId] });
		var result = await service.GetSourcesAsync(registration.Id, new PagedResultRequestDto { MaxResultCount = 20 });
		result.Items.Count.ShouldBe(1);
		result.Items.Single().InquiryFormDefinitionId.ShouldBe(sourceA.Id);
		result.Items.Single().Connections.Single().OfferingName.ShouldBe("Allowed route");
	}

	[Fact]
	public async Task Two_inquiries_using_one_registration_keep_their_own_labels_and_definition()
	{
		var school = Guid.NewGuid();
		var sourceA = Form(school, "InquiryA", AdmissionFormPurpose.Inquiry);
		var sourceB = Form(school, "InquiryB", AdmissionFormPurpose.Inquiry);
		var registration = Registration(school, sourceA.Id, sourceB.Id);
		var aSection = Section(sourceA);
		var bSection = Section(sourceB);
		var service = Service(school, [registration, sourceA, sourceB], sourceSections: [aSection, bSection],
			sourceFields: [Field(sourceA, aSection, "Date of birth"), Field(sourceB, bSection, "DOB")]);
		var a = await service.ComposeAsync(registration.Id, sourceA.Id);
		var b = await service.ComposeAsync(registration.Id, sourceB.Id);
		a.Definition.Id.ShouldBe(registration.Id);
		b.Definition.Id.ShouldBe(registration.Id);
		a.Definition.Sections.SelectMany(section => section.Fields).Single().Label.ShouldBe("Date of birth");
		b.Definition.Sections.SelectMany(section => section.Fields).Single().Label.ShouldBe("DOB");
	}

	private static AdmissionFormDefinition Form(Guid tenant, string code, AdmissionFormPurpose purpose, bool publish = true)
	{
		var form = new AdmissionFormDefinition(Guid.NewGuid(), tenant, code, code, AdmissionFormLayoutType.SinglePage, purpose: purpose);
		if (publish) form.Publish(null);
		return form;
	}

	[Fact]
	public async Task Captured_archived_source_is_discoverable_and_reviewable_after_offering_selects_a_newer_inquiry()
	{
		var school = Guid.NewGuid();
		var registration = Form(school, "Registration", AdmissionFormPurpose.Registration);
		var draft = new AdmissionFormDefinition(Guid.NewGuid(), school, "Registration", registration.Code, AdmissionFormLayoutType.SinglePage,
			2, registration.Id, purpose: AdmissionFormPurpose.Registration);
		var olderInquiry = Form(school, "InquiryOld", AdmissionFormPurpose.Inquiry);
		olderInquiry.Archive();
		var currentInquiry = Form(school, "InquiryNew", AdmissionFormPurpose.Inquiry);
		var cycle = new AdmissionCycle(Guid.NewGuid(), school, "Cycle", "CYCLE", Guid.NewGuid(), DateTime.Today, DateTime.Today.AddMonths(2));
		var offering = Offering(school, cycle.Id, currentInquiry.Id, registration.Id, "Route with captured history");
		var captured = new AdmissionInquiry(Guid.NewGuid(), school, "INQ-OLD", cycle.Id, offering.Id, "Applicant", "9990000000", null, AdmissionInquirySource.Website, null);
		captured.RecordFormResponseSnapshot("{}", olderInquiry.Id);
		var section = Section(olderInquiry);
		var service = Service(school, [registration, draft, olderInquiry, currentInquiry], [offering], [cycle],
			sourceSections: [section], sourceFields: [Field(olderInquiry, section, "Old birth-date label")], sourceInquiries: [captured]);

		var sources = await service.GetSourcesAsync(draft.Id, new PagedResultRequestDto { MaxResultCount = 20 });
		var historical = sources.Items.Single(source => source.InquiryFormDefinitionId == olderInquiry.Id);
		historical.IsApproved.ShouldBeFalse();
		historical.Connections.Single().OfferingId.ShouldBe(offering.Id);
		var preview = await service.PreviewAsync(new() { RegistrationFormDefinitionId = draft.Id, InquiryFormDefinitionId = olderInquiry.Id });
		preview.IsValid.ShouldBeTrue();
		preview.Definition.Sections.SelectMany(item => item.Fields).Single().Label.ShouldBe("Old birth-date label");
	}

	[Fact]
	public async Task Archived_source_without_captured_history_cannot_be_newly_approved()
	{
		var school = Guid.NewGuid();
		var registration = Form(school, "Registration", AdmissionFormPurpose.Registration, publish: false);
		var archived = Form(school, "UnrelatedInquiry", AdmissionFormPurpose.Inquiry);
		archived.Archive();
		var service = Service(school, [registration, archived]);
		var error = await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(new()
			{ RegistrationFormDefinitionId = registration.Id, InquiryFormDefinitionId = archived.Id }));
		error.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.IncompatibleSource);
	}

	[Fact]
	public async Task Saving_an_archived_id_in_an_unpublished_policy_does_not_prove_approved_history()
	{
		var school = Guid.NewGuid();
		var registration = Form(school, "Registration", AdmissionFormPurpose.Registration, publish: false);
		var archived = Form(school, "UnrelatedInquiry", AdmissionFormPurpose.Inquiry);
		archived.Archive();
		AdmissionFormInheritanceMetadataStore.Write(registration, new()
			{ Inheritance = new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = archived.Id }] } });
		var service = Service(school, [registration, archived]);
		await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(new()
			{ RegistrationFormDefinitionId = registration.Id, InquiryFormDefinitionId = archived.Id }));
		(await service.ValidatePolicyAsync(registration.Id)).ShouldContain(issue => issue.Code == "InvalidSource");
	}

	[Fact]
	public async Task Caller_draft_cannot_self_approve_an_unrelated_archived_source()
	{
		var school = Guid.NewGuid();
		var registration = Form(school, "Registration", AdmissionFormPurpose.Registration, publish: false);
		var archived = Form(school, "UnrelatedInquiry", AdmissionFormPurpose.Inquiry);
		archived.Archive();
		var service = Service(school, [registration, archived]);
		var error = await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(new()
		{
			RegistrationFormDefinitionId = registration.Id, InquiryFormDefinitionId = archived.Id,
			Draft = new() { Code = registration.Code, ConcurrencyStamp = registration.ConcurrencyStamp,
				Inheritance = new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = archived.Id }] } }
		}));
		error.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.IncompatibleSource);
	}

	[Fact]
	public async Task Unsaved_custom_field_identity_is_stable_across_source_previews_without_client_identity()
	{
		var school = Guid.NewGuid();
		var source = Form(school, "Inquiry", AdmissionFormPurpose.Inquiry);
		var section = Section(source);
		var service = Service(school, [source], sourceSections: [section], sourceFields: [Field(source, section, "DOB")]);
		var fieldId = Guid.NewGuid();
		var input = new AdmissionFormInheritancePreviewInput { InquiryFormDefinitionId = source.Id,
			Draft = new() { Sections = [new() { Code = "EXTRA", Name = "Extra", Fields = [new()
				{ Id = fieldId, Code = "QUESTION", Label = "Question", BindingType = AdmissionFormBindingType.Custom, BindingKey = "QUESTION" }] }] } };
		var first = await service.PreviewAsync(input);
		var second = await service.PreviewAsync(input);
		first.Definition.Sections.SelectMany(item => item.Fields).Single(field => field.Code == "QUESTION").FieldIdentity.ShouldBe("custom:" + fieldId.ToString("D"));
		second.Definition.Sections.SelectMany(item => item.Fields).Single(field => field.Code == "QUESTION").FieldIdentity.ShouldBe("custom:" + fieldId.ToString("D"));
	}

	private static AdmissionFormDefinition Registration(Guid tenant, params Guid[] sources)
	{
		var form = Form(tenant, "Registration", AdmissionFormPurpose.Registration, publish: false);
		AdmissionFormInheritanceMetadataStore.Write(form, new() { Inheritance = new() { Enabled = true,
			Sources = sources.Select(id => new AdmissionFormInheritanceSourceDto { InquiryFormDefinitionId = id }).ToList() } });
		form.Publish(null);
		return form;
	}

	private static AdmissionOffering Offering(Guid tenant, Guid cycle, Guid inquiry, Guid registration, string name)
	{
		var offering = new AdmissionOffering(Guid.NewGuid(), tenant, cycle, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid().ToString("N"), true, null, null, name);
		offering.ConfigureReferences(Guid.NewGuid(), inquiry, registration);
		return offering;
	}
	private static AdmissionFormSectionDefinition Section(AdmissionFormDefinition form) => new(Guid.NewGuid(), form.Id, "DETAILS", "Details", 0, null);
	private static AdmissionFormFieldDefinition Field(AdmissionFormDefinition form, AdmissionFormSectionDefinition section, string label) =>
		new(Guid.NewGuid(), form.Id, section.Id, "DOB", label, AdmissionFormFieldType.Date, AdmissionFormBindingType.System,
			"Applicant.DateOfBirth", false, false, 0, null, null, null, null, null);

	private static AdmissionFormInheritanceService Service(Guid tenantId, List<AdmissionFormDefinition> sourceForms,
		List<AdmissionOffering>? sourceOfferings = null, List<AdmissionCycle>? sourceCycles = null, UserScopeContext? userScope = null,
		List<AdmissionFormSectionDefinition>? sourceSections = null, List<AdmissionFormFieldDefinition>? sourceFields = null, List<AdmissionInquiry>? sourceInquiries = null)
	{
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(tenantId);
		var scope = Substitute.For<IUserScopeAccessService>(); scope.GetCurrentUserScopeAsync().Returns(userScope ?? new() { IsAdmin = true });
		var applicationScope = new AdmissionApplicationScopeAccessService(Repository(sourceOfferings ?? []), Repository(sourceCycles ?? []),
			Repository(new List<Division>()), scope, Repository(new List<AdmissionApplication>()), null!);
		var provider = new ServiceCollection().AddSingleton(tenant).AddSingleton<IAsyncQueryableExecuter>(new AsyncQueryableExecuter([]))
			.AddSingleton(Repository(sourceInquiries ?? [])).BuildServiceProvider();
		applicationScope.LazyServiceProvider = new AbpLazyServiceProvider(provider);
		return new(Repository(sourceForms), Repository(sourceSections ?? []), Repository(sourceFields ?? []), applicationScope)
		{ LazyServiceProvider = new AbpLazyServiceProvider(provider) };
	}

	private static IRepository<T, Guid> Repository<T>(List<T> rows) where T : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(rows.AsQueryable());
		repository.FindAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => rows.FirstOrDefault(call.Arg<Expression<Func<T, bool>>>().Compile()));
		repository.GetListAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => rows.Where(call.Arg<Expression<Func<T, bool>>>().Compile()).ToList());
		return repository;
	}
}
