using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public interface IEntranceExamSlotRepository  : IRepository<EntranceExamSlot, Guid>
{

}