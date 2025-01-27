using ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;
using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public interface IInquiryPreferenceRepository  : IRepository<FeeHead, Guid>
{
	
}