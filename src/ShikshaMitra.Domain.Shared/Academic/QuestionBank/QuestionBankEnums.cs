namespace ShikshaMitra.Academic.QuestionBank;

public enum QuestionApprovalMode
{
    NoApproval = 1,
    HodApproval = 2,
    PrincipalApproval = 3,
    HodThenPrincipal = 4
}

public enum QuestionVisibility
{
    Private = 1,
    Department = 2,
    StandardLevel = 3,
    SchoolWide = 4
}

public enum DuplicateQuestionAction
{
    AllowWithWarning = 1,
    BlockExactDuplicate = 2,
    BlockSimilarDuplicate = 3
}

public enum QuestionType
{
    McqSingleCorrect = 1,
    McqMultipleCorrect = 2,
    TrueFalse = 3,
    FillInTheBlank = 4,
    ShortAnswer = 5,
    LongAnswer = 6,
    MatchTheFollowing = 7,
    AssertionReason = 8,
    CaseStudy = 9,
    DiagramBased = 10,
    Practical = 11
}

public enum DifficultyLevel
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}

public enum BloomLevel
{
    Remembering = 1,
    Understanding = 2,
    Applying = 3,
    Analyzing = 4,
    Evaluating = 5,
    Creating = 6
}

public enum QuestionStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Archived = 5
}

public enum QuestionAttachmentType
{
    Image = 1,
    Diagram = 2,
    Audio = 3,
    Video = 4,
    Document = 5
}

public enum QuestionUsageSource
{
    Homework = 1,
    Assignment = 2,
    Quiz = 3,
    QuestionPaper = 4,
    Worksheet = 5,
    PracticeTest = 6
}
