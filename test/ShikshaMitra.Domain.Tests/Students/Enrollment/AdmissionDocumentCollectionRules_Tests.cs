using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionDocumentCollectionRules_Tests
{
	[Fact]
	public void Collection_requires_the_configured_form_and_a_forward_path_in_the_same_workflow()
	{
		var workflow = Guid.NewGuid();
		AdmissionWorkflowStageDefinition Stage(string code, AdmissionWorkflowStageType type, Guid? workflowId = null) =>
			new(Guid.NewGuid(), workflowId ?? workflow, code, code, type, 0, true, false, null, null);
		var form = Stage("FORM", AdmissionWorkflowStageType.Form);
		var otherForm = Stage("OTHER", AdmissionWorkflowStageType.Form);
		var docs = Stage("DOCS", AdmissionWorkflowStageType.DocumentVerification);
		var middle = Stage("MIDDLE", AdmissionWorkflowStageType.Custom);
		var policy = new AdmissionDocumentPolicy { Version = 1, Enabled = true, CollectionFormStageCode = "FORM" };
		AdmissionWorkflowTransitionDefinition[] routes =
		[
			new(Guid.NewGuid(), workflow, form.Id, middle.Id, 0, isDefault: true),
			new(Guid.NewGuid(), workflow, middle.Id, docs.Id, 0, isDefault: true)
		];
		AdmissionDocumentCollectionRules.IsLinkedForm(form, docs, policy, routes).ShouldBeTrue();
		AdmissionDocumentCollectionRules.IsLinkedForm(otherForm, docs, policy, routes).ShouldBeFalse();
		AdmissionDocumentCollectionRules.IsLinkedForm(form, docs, policy with { CollectionFormStageCode = null }, routes).ShouldBeFalse();
		AdmissionDocumentCollectionRules.IsLinkedForm(form, docs, policy, []).ShouldBeFalse();
		AdmissionDocumentCollectionRules.IsLinkedForm(docs, form, policy, routes).ShouldBeFalse();
		var foreignForm = Stage("FORM", AdmissionWorkflowStageType.Form, Guid.NewGuid());
		AdmissionDocumentCollectionRules.IsLinkedForm(foreignForm, docs, policy, routes).ShouldBeFalse();
		AdmissionDocumentCollectionRules.IsLinkedForm(form, docs, policy,
			[new(Guid.NewGuid(), Guid.NewGuid(), form.Id, docs.Id, 0)]).ShouldBeFalse();
	}
}
