namespace ShikshaMitra.Academic.HomeworkAssignment;

public enum RubricStatus
{
	Draft = 0,
	Published = 1,
	Archived = 2,
	Superseded = 3
}

public enum RubricEvaluationStatus
{
	Draft = 0,
	Submitted = 1,
	Approved = 2,
	Returned = 3
}

public enum RubricScoringModel
{
	PointsBased = 0,
	WeightedPercentage = 1,
	PerformanceLevels = 2,
	Checklist = 3,
	RatingScale = 4
}

public enum RubricVisibility
{
	Private = 0,
	SelectedTeachers = 1,
	Department = 2,
	Institution = 3
}
