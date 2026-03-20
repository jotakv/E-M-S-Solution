using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Contracts;

namespace ServerLibrary.Repositories.Implementations
{
    public class DoctorRepository(
        AppDbContext appDbContext,
        ILogger<DoctorRepository> logger) : IGenericRepositoryInterface<Doctor>
    {
        public async Task<List<Doctor>> GetAll() =>
            await appDbContext.Doctors
                .AsNoTracking()
                .ToListAsync();

        public async Task<Doctor> GetById(int id) =>
            (await appDbContext.Doctors.FirstOrDefaultAsync(eid => eid.EmployeeId == id))!;

        public async Task<GeneralResponse> Insert(Doctor item)
        {
            try
            {
                appDbContext.Doctors.Add(item);
                await Commit();

                logger.LogInformation(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | EmployeeId: {EmployeeId} | Date: {Date} | Diagnose: {Diagnose} | Result: {Result}",
                    "HealthRecordCreate", "Create", "HealthRecord", item.Id, item.EmployeeId, item.Date, item.MedicalDiagnose, "Success");

                return Success();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Exception creating health record for EmployeeId: {EmployeeId}", item.EmployeeId);
                return new GeneralResponse(false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<GeneralResponse> Update(Doctor item)
        {
            var obj = await appDbContext.Doctors
                .FirstOrDefaultAsync(eid => eid.EmployeeId == item.EmployeeId);

            if (obj is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Result: {Result}",
                    "HealthRecordUpdate", "Update", "HealthRecord", item.EmployeeId, "Failure:NotFound");
                return NotFound();
            }

            var changes = new List<object>();
            if (obj.Date != item.Date)
                changes.Add(new { Field = "Date", OldValue = obj.Date, NewValue = item.Date });
            if (obj.MedicalDiagnose != item.MedicalDiagnose)
                changes.Add(new { Field = "MedicalDiagnose", OldValue = obj.MedicalDiagnose, NewValue = item.MedicalDiagnose });
            if (obj.MedicalRecommendation != item.MedicalRecommendation)
                changes.Add(new { Field = "MedicalRecommendation", OldValue = obj.MedicalRecommendation, NewValue = item.MedicalRecommendation });

            obj.MedicalRecommendation = item.MedicalRecommendation;
            obj.MedicalDiagnose       = item.MedicalDiagnose;
            obj.Date                  = item.Date;
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Changes: {@Changes} | Result: {Result}",
                "HealthRecordUpdate", "Update", "HealthRecord", item.EmployeeId, changes, "Success");

            return Success();
        }

        public async Task<GeneralResponse> DeleteById(int id)
        {
            var item = await appDbContext.Doctors
                .FirstOrDefaultAsync(eid => eid.EmployeeId == id);

            if (item is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EmployeeId: {EmployeeId} | Result: {Result}",
                    "HealthRecordDelete", "Delete", "HealthRecord", id, "Failure:NotFound");
                return NotFound();
            }

            appDbContext.Doctors.Remove(item);
            await Commit();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | EmployeeId: {EmployeeId} | Result: {Result}",
                "HealthRecordDelete", "Delete", "HealthRecord", item.Id, id, "Success");

            return Success();
        }

        private async Task Commit() => await appDbContext.SaveChangesAsync();
        private static GeneralResponse NotFound() => new(false, "Sorry data not found");
        private static GeneralResponse Success()  => new(true,  "Process completed");
    }
}
