using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmOfficialPolicyCatalog
{
	public static readonly DateTime VerifiedAt = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

	public static bool IsCurrent(AdmissionCrmPolicyPack existing, CreateAdmissionCrmPolicyPackDto baseline)
	{
		var sources = AdmissionCrmPolicyMapper.Deserialize<IReadOnlyList<AdmissionCrmPolicySourceDto>>(existing.SourcesJson) ?? [];
		var rules = AdmissionCrmPolicyMapper.Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(existing.RulesJson) ?? [];
		return sources.Any(source => source.VerifiedAt >= VerifiedAt) &&
			baseline.Rules.All(expected => rules.Any(actual =>
				actual.Code.Equals(expected.Code, StringComparison.OrdinalIgnoreCase)));
	}

	public static void Validate(CreateAdmissionCrmPolicyPackDto input)
	{
		if (input.Sources.Count == 0 || input.Rules.Count == 0)
			throw new BusinessException("AdmissionCrm:PolicySourcesAndRulesRequired");
		if (input.Sources.Any(source =>
			!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
			throw new BusinessException("AdmissionCrm:OfficialHttpsPolicySourceRequired");
		if (input.Sources.Any(source => source.VerifiedAt > DateTime.UtcNow.AddMinutes(5)))
			throw new BusinessException("AdmissionCrm:InvalidPolicySourceVerificationDate");
		var codes = input.Rules.Select(rule => rule.Code.Trim().ToUpperInvariant()).ToList();
		if (codes.Count != codes.Distinct().Count())
			throw new BusinessException("AdmissionCrm:DuplicatePolicyRuleCode");
		if (input.Rules.Any(rule => string.IsNullOrWhiteSpace(rule.SourceUrl) ||
			input.Sources.All(source => !source.Url.Equals(rule.SourceUrl, StringComparison.OrdinalIgnoreCase))))
			throw new BusinessException("AdmissionCrm:PolicyRuleSourceNotRegistered");
	}

	public static IReadOnlyList<CreateAdmissionCrmPolicyPackDto> Build()
	{
		const string cbseUrl = "https://www.cbse.gov.in/cbsenew/admission.html";
		const string gujaratRteUrl = "https://rte.orpgujarat.com/Content/Docs/ActRules/age_limit.pdf";
		const string gujaratRteRulesUrl = "https://rte.orpgujarat.com/Home/Rules";
		const string gsebUrl = "https://studentserv.gseb.org/";
		const string apaarUrl = "https://apaar.education.gov.in/";
		var reviewDueAt = new DateTime(2027, 1, 11, 0, 0, 0, DateTimeKind.Utc);
		AdmissionCrmPolicySourceDto Source(string authority, string title, string url, DateTime effectiveAt, string note) => new()
		{
			Authority = authority, Title = title, Url = url, PublishedOrEffectiveAt = effectiveAt,
			VerifiedAt = VerifiedAt, ApplicabilityNote = note
		};
		AdmissionCrmPolicyRuleDto Rule(string code, string name, AdmissionCrmPolicyRuleType type,
			AdmissionCrmPolicyRuleSeverity severity, string description, string authority, string sourceUrl,
			string? desiredClass = null, string? evidence = null) => new()
		{
			Code = code, Name = name, Type = type, Severity = severity, Description = description,
			DesiredClass = desiredClass, RequiredEvidenceCode = evidence, ManualVerificationRequired = true,
			SourceAuthority = authority, SourceUrl = sourceUrl
		};

		return
		[
			new CreateAdmissionCrmPolicyPackDto
			{
				Code = "CBSE-ADMISSION-CORE", Name = "CBSE admission governance baseline",
				Jurisdiction = "India and applicable State or Union Territory", Board = "CBSE",
				EffectiveFrom = new DateTime(1995, 1, 31, 0, 0, 0, DateTimeKind.Utc), ReviewDueAt = reviewDueAt,
				Sources = [Source("Central Board of Secondary Education", "Admission of students to a school and transfer or migration", cbseUrl,
					new DateTime(1995, 1, 31, 0, 0, 0, DateTimeKind.Utc),
					"Current official CBSE admission publication; annual circulars and the applicable State or Union Territory rules must also be reviewed.")],
				Rules =
				[
					Rule("CBSE-STATE-AGE", "State-governed age eligibility", AdmissionCrmPolicyRuleType.AgeEligibility,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Verify the applicable State or Union Territory age rule; this baseline does not impose a national CBSE cutoff.", "CBSE", cbseUrl),
					Rule("CBSE-QUALIFYING-EXAM", "Qualifying examination eligibility", AdmissionCrmPolicyRuleType.QualifyingExamination,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Verify the recognised qualifying examination for the class sought.", "CBSE", cbseUrl, evidence: "QUALIFYING_EXAM"),
					Rule("CBSE-TRANSFER-CERTIFICATE", "Transfer certificate verification", AdmissionCrmPolicyRuleType.DocumentEvidence,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Verify the authenticated transfer certificate where the applicant attended another school.", "CBSE", cbseUrl, evidence: "TRANSFER_CERTIFICATE"),
					Rule("CBSE-LATE-ADMISSION-IX-PLUS", "Class IX and above late-admission approval", AdmissionCrmPolicyRuleType.AdmissionDeadline,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Review the current deadline and competent-authority approval requirements before late admission in Class IX or above.", "CBSE", cbseUrl),
					Rule("CBSE-CLASS-X-DIRECT", "Class X direct-admission restriction", AdmissionCrmPolicyRuleType.ClassEligibility,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Verify Class IX regular study, result, transfer circumstances, and any required post-facto approval.", "CBSE", cbseUrl, "Class X"),
					Rule("CBSE-CLASS-XII-DIRECT", "Class XII direct-admission restriction", AdmissionCrmPolicyRuleType.ClassEligibility,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Verify Class XI regular study, result, transfer circumstances, and any required post-facto approval.", "CBSE", cbseUrl, "Class XII"),
					Rule("CBSE-ADMISSION-REGISTER", "Admission register entry", AdmissionCrmPolicyRuleType.AdmissionRegister,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Verify entry in the prescribed admission register before activation is treated as complete.", "CBSE", cbseUrl),
					Rule("CBSE-PERMANENT-NUMBER", "Successive permanent admission number", AdmissionCrmPolicyRuleType.PermanentNumber,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Allocate a successive admission number and restore the original number for a returning student.", "CBSE", cbseUrl)
				]
			},
			new CreateAdmissionCrmPolicyPackDto
			{
				Code = "GUJARAT-RTE-2025", Name = "Gujarat RTE governed admission baseline",
				Jurisdiction = "Gujarat", Board = "Gujarat RTE",
				EffectiveFrom = new DateTime(2025, 5, 28, 0, 0, 0, DateTimeKind.Utc), ReviewDueAt = reviewDueAt,
				Sources =
				[
					Source("Government of Gujarat Education Department", "Right of Children to Free and Compulsory Education Amendment Rules, 2025", gujaratRteUrl,
						new DateTime(2025, 5, 28, 0, 0, 0, DateTimeKind.Utc), "Official age-limit amendment; cycle adoption must set the applicable June 1 cutoff date."),
					Source("Gujarat Department of Primary Education", "RTE rules, regulations and advertisements", gujaratRteRulesUrl,
						VerifiedAt, "Official publication index; review current allotment, evidence, and deadline notices for each cycle.")
				],
				Rules =
				[
					new AdmissionCrmPolicyRuleDto
					{
						Code = "GJ-RTE-STD1-AGE", Name = "Standard I age on June 1", Type = AdmissionCrmPolicyRuleType.AgeEligibility,
						Severity = AdmissionCrmPolicyRuleSeverity.ApprovalRequired, Description = "Set the cycle-specific June 1 cutoff and verify that the child is at least six years old.",
						DesiredClass = "Standard I", MinimumAgeYears = 6, ManualVerificationRequired = true,
						SourceAuthority = "Government of Gujarat Education Department", SourceUrl = gujaratRteUrl
					},
					Rule("GJ-RTE-CWSN-RELAXATION", "CwSN age-relaxation review", AdmissionCrmPolicyRuleType.AgeEligibility,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Verify disability evidence and the applicable relaxation before overriding the standard age evaluation.", "Government of Gujarat Education Department", gujaratRteUrl, "Standard I", "CWSN_EVIDENCE"),
					Rule("GJ-RTE-ALLOTMENT", "Official RTE allotment verification", AdmissionCrmPolicyRuleType.Allotment,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Verify the current-cycle authority allotment reference and school mapping.", "Gujarat Department of Primary Education", gujaratRteRulesUrl, evidence: "RTE_ALLOTMENT"),
					Rule("GJ-RTE-CYCLE-NOTICES", "Current-cycle RTE notices", AdmissionCrmPolicyRuleType.AdmissionDeadline,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Review current official deadlines, documents, and exceptions before publishing the cycle adoption.", "Gujarat Department of Primary Education", gujaratRteRulesUrl)
				]
			},
			new CreateAdmissionCrmPolicyPackDto
			{
				Code = "GUJARAT-BOARD-READINESS", Name = "Gujarat Board admission readiness baseline",
				Jurisdiction = "Gujarat", Board = "GSHSEB", EffectiveFrom = VerifiedAt, ReviewDueAt = reviewDueAt,
				Sources = [Source("Gujarat Secondary and Higher Secondary Education Board", "GSHSEB student services and official instructions", gsebUrl,
					VerifiedAt, "Governed review pointer; schools must configure current class-specific GSHSEB instructions rather than infer requirements from this baseline.")],
				Rules =
				[
					Rule("GSHSEB-PRIOR-RECORD", "Previous academic record verification", AdmissionCrmPolicyRuleType.QualifyingExamination,
						AdmissionCrmPolicyRuleSeverity.Warning, "Verify previous marksheet or result evidence against the current class and route requirements.", "GSHSEB", gsebUrl, evidence: "PREVIOUS_MARKSHEET"),
					Rule("GSHSEB-MIGRATION-REVIEW", "Migration certificate applicability", AdmissionCrmPolicyRuleType.DocumentEvidence,
						AdmissionCrmPolicyRuleSeverity.Warning, "Review whether the current route and board change require migration evidence.", "GSHSEB", gsebUrl, evidence: "MIGRATION_CERTIFICATE"),
					Rule("GSHSEB-CURRENT-INSTRUCTIONS", "Current GSHSEB instruction review", AdmissionCrmPolicyRuleType.AuthorityApproval,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Record review of the applicable current GSHSEB instructions before final activation.", "GSHSEB", gsebUrl)
				]
			},
			new CreateAdmissionCrmPolicyPackDto
			{
				Code = "APAAR-IDENTITY-READINESS", Name = "APAAR, PEN, and UDISE+ identity readiness baseline",
				Jurisdiction = "India", Board = "Ministry of Education", EffectiveFrom = VerifiedAt, ReviewDueAt = reviewDueAt,
				Sources = [Source("Ministry of Education and Digital India Corporation", "Official APAAR service and FAQs", apaarUrl,
					VerifiedAt, "Identity-readiness guidance; this pack does not make APAAR a universal admission prerequisite.")],
				Rules =
				[
					Rule("APAAR-PEN-REQUIRED", "PEN available for APAAR generation", AdmissionCrmPolicyRuleType.GovernmentIdentifier,
						AdmissionCrmPolicyRuleSeverity.Warning, "Record and verify the student's PEN before an APAAR generation handoff.", "Ministry of Education", apaarUrl, evidence: "PEN"),
					Rule("APAAR-DEMOGRAPHIC-MATCH", "UDISE+ and Aadhaar demographic match", AdmissionCrmPolicyRuleType.GovernmentIdentifier,
						AdmissionCrmPolicyRuleSeverity.Warning, "Verify the UDISE+ student name against the Aadhaar name and resolve discrepancies before generation.", "Ministry of Education", apaarUrl, evidence: "IDENTITY_MATCH"),
					Rule("APAAR-MINOR-CONSENT", "Parental consent for a minor", AdmissionCrmPolicyRuleType.Consent,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Preserve voluntary parental consent before requesting APAAR generation for a minor.", "Ministry of Education", apaarUrl, evidence: "APAAR_PARENT_CONSENT")
				]
			}
		];
	}
}
