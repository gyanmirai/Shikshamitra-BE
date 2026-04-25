using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Individual subject details in response
/// </summary>
public class SubjectAssignmentDetailResponseDto
{
        public Guid AssignedSubjectId { get; set; }
        public Guid SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public string? SubjectCode { get; set; }
        public string? Color { get; set; }
        public SubjectType SubjectType { get; set; }
        public Guid? ElectiveGroupId { get; set; }
        public TeacherInfoDto? PrimaryTeacher { get; set; }
        public List<TeacherInfoDto> SecondaryTeachers { get; set; } = [];
        public string? LearningGoals { get; set; }
        public string? Prerequisites { get; set; }
        public string? Description { get; set; }
        public List<string> Documents { get; set; } = [];
}
