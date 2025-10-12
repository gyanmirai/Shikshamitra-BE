using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Examination.Setting
{
	public class GradeRange : Entity<Guid>
	{
		public Guid GradeScaleId { get; private set; }
		public float MinMarks { get; private set; }
		public float MaxMarks { get; private set; }
		public GradeType Grade { get; private set; }
		public GradeScale GradeScale { get; private set; }

		private GradeRange() { }

		public GradeRange(Guid id, Guid gradeScaleId, float minMarks, float maxMarks, GradeType grade)
			: base(id)
		{
			SetGradeScale(gradeScaleId);
			SetMarksRange(minMarks, maxMarks);
			SetGrade(grade);
		}

		public void SetGradeScale(Guid gradeScaleId)
		{
			if (gradeScaleId == Guid.Empty)
				throw new BusinessException("GradeRange:GradeScaleRequired");

			GradeScaleId = gradeScaleId;
		}

		public void SetMarksRange(float minMarks, float maxMarks)
		{
			if (minMarks < 0 || maxMarks < 0)
				throw new BusinessException("GradeRange:NegativeMarksNotAllowed");

			if (minMarks >= maxMarks)
				throw new BusinessException("GradeRange:InvalidRange")
					.WithData("MinMarks", minMarks)
					.WithData("MaxMarks", maxMarks);

			MinMarks = minMarks;
			MaxMarks = maxMarks;
		}

		public void SetGrade(GradeType grade)
		{
			Grade = grade;
		}
	}
}
