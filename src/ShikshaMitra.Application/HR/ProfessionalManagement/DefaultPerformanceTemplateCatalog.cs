using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

internal static class DefaultPerformanceTemplateCatalog
{
	public static List<PerformanceTemplateRequest> GetDefaultTemplateRequests() =>
	[
		TeacherTemplate(
			"CBSE Teacher Appraisal - SQAAF Ready",
			PerformanceBoardType.CBSE,
			"CBSE annual teacher appraisal mapped to SQAAF domains, classroom practice, assessment quality, learner support, beneficiary feedback, and professional development."),
		TeacherTemplate(
			"GSEB Teacher Appraisal - Gujarat Schools",
			PerformanceBoardType.GSEB,
			"Gujarat school teacher appraisal covering lesson delivery, Gujarati/English medium support, student progress, parent communication, and school improvement actions.",
			"જીએસઈબી શિક્ષક મૂલ્યાંકન"),
		NonTeachingTemplate(
			"CBSE / GSEB Non-Teaching Staff Appraisal",
			PerformanceBoardType.GenericIndian,
			"Indian school support staff appraisal covering attendance, service quality, compliance, parent handling, records, and school operations."),
		LeadershipTemplate(),
		ClassroomObservationTemplate(),
		ProbationTemplate()
	];

	private static PerformanceTemplateRequest TeacherTemplate(string name, PerformanceBoardType boardType, string description, string? gujaratiName = null) => new()
	{
		Name = name,
		GujaratiName = gujaratiName,
		BoardType = boardType,
		StaffCategory = PerformanceStaffCategory.Teacher,
		Description = description,
		Sections =
		[
			Section("Curriculum planning and syllabus progress", "પાઠયક્રમ આયોજન", PerformanceSqaaDomain.CurriculumPedagogyAssessment, 15, 1,
				Criterion("Annual and weekly lesson plans are timely and board-aligned", "વાર્ષિક અને સાપ્તાહિક પાઠ આયોજન સમયસર છે", true),
				Criterion("Syllabus completion and remediation are tracked with evidence", "પાઠ્યક્રમ પૂર્ણતા અને પુનરાવર્તન નોંધાયેલ છે", true)),
			Section("Pedagogy and classroom engagement", "વર્ગખંડ શીખવણ", PerformanceSqaaDomain.CurriculumPedagogyAssessment, 25, 2,
				Criterion("Explains concepts clearly using child-centred methods", "વિદ્યાર્થી કેન્દ્રિત પદ્ધતિથી સ્પષ્ટ સમજાવે છે", false),
				Criterion("Uses inclusive engagement, activity, and questioning techniques", "સમાવેશી પ્રવૃત્તિ અને પ્રશ્નોત્તરીનો ઉપયોગ કરે છે", false),
				Criterion("Maintains classroom discipline with dignity and care", "શિસ્ત સંવેદનશીલ રીતે જાળવે છે", false)),
			Section("Assessment quality and student outcomes", "મૂલ્યાંકન અને પરિણામ", PerformanceSqaaDomain.CurriculumPedagogyAssessment, 20, 3,
				Criterion("Uses fair assessments, timely correction, and feedback", "યોગ્ય મૂલ્યાંકન અને સમયસર પ્રતિસાદ આપે છે", true),
				Criterion("Tracks learning gaps and supports improvement plans", "શીખવાની ખામીઓ ઓળખી સુધારણા કરે છે", true)),
			Section("Student well-being and parent communication", "વિદ્યાર્થી સુખાકારી", PerformanceSqaaDomain.InclusivenessWellBeing, 15, 4,
				Criterion("Identifies learner needs and supports inclusion", "વિદ્યાર્થી જરૂરિયાતો ઓળખે છે", false),
				Criterion("Communicates constructively with parents and guardians", "વાલીઓ સાથે રચનાત્મક સંવાદ કરે છે", false)),
			Section("Professional growth and school contribution", "વ્યાવસાયિક વિકાસ", PerformanceSqaaDomain.HumanResources, 25, 5,
				Criterion("Completes training, peer learning, and development actions", "તાલીમ અને વિકાસ પ્રવૃત્તિ પૂર્ણ કરે છે", true),
				Criterion("Contributes to assemblies, clubs, events, duties, and school processes", "શાળા પ્રવૃત્તિઓમાં યોગદાન આપે છે", false))
		]
	};

	private static PerformanceTemplateRequest NonTeachingTemplate(string name, PerformanceBoardType boardType, string description) => new()
	{
		Name = name,
		BoardType = boardType,
		StaffCategory = PerformanceStaffCategory.Admin,
		Description = description,
		Sections =
		[
			Section("Attendance, punctuality and reliability", null, PerformanceSqaaDomain.HumanResources, 20, 1,
				Criterion("Maintains dependable attendance and reporting discipline", null, false),
				Criterion("Responds to school priorities without repeated follow-up", null, false)),
			Section("Work quality and record accuracy", null, PerformanceSqaaDomain.ManagementSchoolProcesses, 30, 2,
				Criterion("Completes assigned work accurately and on time", null, true),
				Criterion("Maintains registers, files, and ERP records correctly", null, true)),
			Section("Stakeholder handling and communication", null, PerformanceSqaaDomain.BeneficiarySatisfaction, 20, 3,
				Criterion("Handles students, parents, vendors, and staff courteously", null, false),
				Criterion("Escalates issues to the right person at the right time", null, false)),
			Section("Compliance, safety and teamwork", null, PerformanceSqaaDomain.ManagementSchoolProcesses, 30, 4,
				Criterion("Follows school policies, safety instructions, and confidentiality rules", null, true),
				Criterion("Supports teamwork and process improvement", null, false))
		]
	};

	private static PerformanceTemplateRequest LeadershipTemplate() => new()
	{
		Name = "Principal / Coordinator Review",
		BoardType = PerformanceBoardType.GenericIndian,
		StaffCategory = PerformanceStaffCategory.PrincipalVicePrincipal,
		Description = "Leadership appraisal for principal, vice-principal, HOD, coordinator, and school management roles.",
		Sections =
		[
			Section("Academic leadership and monitoring", null, PerformanceSqaaDomain.LeadershipGovernance, 30, 1,
				Criterion("Reviews lesson plans, classroom practice, and academic progress", null, true),
				Criterion("Uses assessment data for corrective action", null, true)),
			Section("Teacher development and mentoring", null, PerformanceSqaaDomain.HumanResources, 25, 2,
				Criterion("Conducts meaningful observations and feedback discussions", null, true),
				Criterion("Tracks teacher training and development plans", null, true)),
			Section("Governance, compliance and school processes", null, PerformanceSqaaDomain.ManagementSchoolProcesses, 25, 3,
				Criterion("Maintains audit-ready records and follows school policies", null, true),
				Criterion("Improves operational process discipline", null, false)),
			Section("Stakeholder satisfaction and well-being", null, PerformanceSqaaDomain.BeneficiarySatisfaction, 20, 4,
				Criterion("Responds to parent/student concerns fairly", null, false),
				Criterion("Promotes inclusion, safety, and school culture", null, false))
		]
	};

	private static PerformanceTemplateRequest ClassroomObservationTemplate() => new()
	{
		Name = "Classroom Observation Template",
		BoardType = PerformanceBoardType.GenericIndian,
		StaffCategory = PerformanceStaffCategory.Teacher,
		Description = "Focused classroom walkthrough template for academic coordinators and principals.",
		Sections =
		[
			Section("Lesson objective and preparation", null, PerformanceSqaaDomain.CurriculumPedagogyAssessment, 25, 1,
				Criterion("Lesson objective is visible, age-appropriate, and aligned", null, true)),
			Section("Teaching-learning process", null, PerformanceSqaaDomain.CurriculumPedagogyAssessment, 35, 2,
				Criterion("Teacher uses activity, questioning, examples, and checks for understanding", null, false),
				Criterion("Students are engaged and participating", null, false)),
			Section("Classroom management and inclusion", null, PerformanceSqaaDomain.InclusivenessWellBeing, 20, 3,
				Criterion("Classroom routines, discipline, and inclusion are handled constructively", null, false)),
			Section("Assessment and follow-up", null, PerformanceSqaaDomain.CurriculumPedagogyAssessment, 20, 4,
				Criterion("Teacher records gaps and follow-up actions", null, true))
		]
	};

	private static PerformanceTemplateRequest ProbationTemplate() => new()
	{
		Name = "Probation / Contract Renewal Review",
		BoardType = PerformanceBoardType.GenericIndian,
		StaffCategory = PerformanceStaffCategory.Teacher,
		Description = "Simple confirmation and renewal review for new teachers or contract staff.",
		Sections =
		[
			Section("Role readiness", null, PerformanceSqaaDomain.HumanResources, 35, 1,
				Criterion("Understands school expectations, timetable, and responsibilities", null, false),
				Criterion("Demonstrates basic role competence", null, true)),
			Section("Conduct and reliability", null, PerformanceSqaaDomain.HumanResources, 30, 2,
				Criterion("Maintains attendance, punctuality, and professional conduct", null, false)),
			Section("Classroom or work quality", null, PerformanceSqaaDomain.CurriculumPedagogyAssessment, 35, 3,
				Criterion("Shows progress based on observation and reviewer feedback", null, true))
		]
	};

	private static PerformanceTemplateSectionRequest Section(
		string name,
		string? gujaratiName,
		PerformanceSqaaDomain sqaaDomain,
		decimal weight,
		int order,
		params PerformanceTemplateCriterionRequest[] criteria) => new()
	{
		Name = name,
		GujaratiName = gujaratiName,
		SqaaDomain = sqaaDomain,
		Weight = weight,
		DisplayOrder = order,
		Criteria = criteria
			.Select((criterion, index) =>
			{
				var criterionWeight = Math.Round(100m / criteria.Length, 2);
				if (index == criteria.Length - 1)
				{
					criterionWeight = 100m - Math.Round(criterionWeight * (criteria.Length - 1), 2);
				}

				criterion.DisplayOrder = index + 1;
				criterion.Weight = criterionWeight;
				criterion.SqaaDomain = sqaaDomain;
				return criterion;
			})
			.ToList()
	};

	private static PerformanceTemplateCriterionRequest Criterion(
		string name,
		string? gujaratiName,
		bool evidenceRequired,
		PerformanceSqaaDomain sqaaDomain = PerformanceSqaaDomain.HumanResources) => new()
	{
		Name = name,
		GujaratiName = gujaratiName,
		SqaaDomain = sqaaDomain,
		EvidenceRequired = evidenceRequired
	};
}
