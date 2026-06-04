using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

internal static class DefaultPerformanceTemplateCatalog
{
	public static List<PerformanceTemplateRequest> GetDefaultTemplateRequests() =>
	[
		new()
		{
			Name = "Teacher Appraisal Template",
			StaffCategory = PerformanceStaffCategory.Teacher,
			Description = "Indian school teacher appraisal covering classroom practice, learning outcomes, conduct, and school contribution.",
			Sections =
			[
				DefaultSection("Lesson planning and syllabus completion", 15, 1, ["Lesson plans are timely and aligned", "Syllabus progress is maintained"]),
				DefaultSection("Classroom delivery and student engagement", 20, 2, ["Explains concepts clearly", "Uses inclusive engagement techniques"]),
				DefaultSection("Learning outcomes and assessment quality", 20, 3, ["Improves student outcomes", "Uses fair assessment and correction practices"]),
				DefaultSection("Discipline, parent communication and conduct", 20, 4, ["Maintains classroom discipline", "Communicates constructively with parents"]),
				DefaultSection("School contribution and professional growth", 25, 5, ["Participates in school events", "Completes training and improvement actions"])
			]
		},
		new()
		{
			Name = "Non-Teaching Staff Appraisal Template",
			StaffCategory = PerformanceStaffCategory.Admin,
			Description = "Indian school support staff appraisal covering reliability, quality, communication, compliance, and teamwork.",
			Sections =
			[
				DefaultSection("Attendance and punctuality", 20, 1, ["Maintains reliable attendance", "Reports on time"]),
				DefaultSection("Task completion and quality", 30, 2, ["Completes assigned work", "Maintains accuracy"]),
				DefaultSection("Communication and stakeholder handling", 20, 3, ["Handles parents/students professionally", "Coordinates with team members"]),
				DefaultSection("Process compliance and initiative", 30, 4, ["Follows school processes", "Takes ownership and improves work"])
			]
		}
	];

	private static PerformanceTemplateSectionRequest DefaultSection(string name, decimal weight, int order, IReadOnlyList<string> criteria) => new()
	{
		Name = name,
		Weight = weight,
		DisplayOrder = order,
		Criteria = criteria.Select((criterion, index) => new PerformanceTemplateCriterionRequest
		{
			Name = criterion,
			Weight = Math.Round(100m / criteria.Count, 2),
			DisplayOrder = index + 1,
			EvidenceRequired = false
		}).ToList()
	};
}
