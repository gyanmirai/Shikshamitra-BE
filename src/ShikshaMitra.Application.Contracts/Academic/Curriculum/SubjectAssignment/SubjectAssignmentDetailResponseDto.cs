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
        public List<TeacherInfoDto> Teachers { get; set; } = [];
        /// <summary>Legacy compatibility field. New clients should use <see cref="Teachers"/>.</summary>
        public TeacherInfoDto? PrimaryTeacher { get; set; }
        /// <summary>Legacy compatibility field. New clients should use <see cref="Teachers"/>.</summary>
        public List<TeacherInfoDto> SecondaryTeachers { get; set; } = [];
        public string? LearningGoals { get; set; }
        public string? Prerequisites { get; set; }
        public string? Description { get; set; }
        public List<string> Documents { get; set; } = [];

        public int WeeklyPeriods { get; set; }
        public DifficultyLevel DifficultyLevel { get; set; }
        public string? SubjectGroup { get; set; }
        public bool IsAiSuggested { get; set; }
        public bool IsCopiedFromPreviousYear { get; set; }

        /// <summary>Setup health score 0-100</summary>
        public int SetupHealthScore { get; set; }

        /// <summary>Number of chapters/syllabus units</summary>
        public int ChaptersCount { get; set; }

        /// <summary>Last update timestamp</summary>
        public DateTime? LastUpdatedAt { get; set; }
}
