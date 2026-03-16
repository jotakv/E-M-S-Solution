using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerLibrary.Helpers;

namespace ServerLibrary.Data
{
    public static class DevelopmentDataSeeder
    {
        private static readonly DateTime DemoCountrySyncedAtUtc = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider
                .GetService<ILoggerFactory>()?
                .CreateLogger(nameof(DevelopmentDataSeeder));

            logger?.LogInformation("Applying migrations before development demo seeding.");
            await context.Database.MigrateAsync();

            await SeedAsync(context, logger);
        }

        public static async Task SeedAsync(
            AppDbContext context,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            if (await HasMeaningfulDataAsync(context, cancellationToken))
            {
                logger?.LogInformation(
                    "Skipping development demo seeding because meaningful data already exists.");
                return;
            }

            IDbContextTransaction? transaction = null;

            if (context.Database.IsRelational())
            {
                transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            }

            try
            {
                logger?.LogInformation("Seeding development demo data.");

                var roles = await EnsureRolesAsync(context, cancellationToken);
                await SeedUsersAsync(context, roles, cancellationToken);

                var generalDepartments = await SeedGeneralDepartmentsAsync(context, cancellationToken);
                var departments = await SeedDepartmentsAsync(context, generalDepartments, cancellationToken);
                var branches = await SeedBranchesAsync(context, departments, cancellationToken);

                var countries = await SeedCountriesAsync(context, cancellationToken);
                var cities = await SeedCitiesAsync(context, countries, cancellationToken);
                var towns = await SeedTownsAsync(context, cities, cancellationToken);

                var overtimeTypes = await SeedOvertimeTypesAsync(context, cancellationToken);
                var sanctionTypes = await SeedSanctionTypesAsync(context, cancellationToken);
                var vacationTypes = await SeedVacationTypesAsync(context, cancellationToken);

                var employees = await SeedEmployeesAsync(context, branches, towns, cancellationToken);

                await SeedDoctorsAsync(context, employees, cancellationToken);
                await SeedOvertimesAsync(context, employees, overtimeTypes, cancellationToken);
                await SeedSanctionsAsync(context, employees, sanctionTypes, cancellationToken);
                await SeedVacationsAsync(context, employees, vacationTypes, cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                logger?.LogInformation("Development demo data seeded successfully.");
            }
            catch
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        private static async Task<bool> HasMeaningfulDataAsync(
            AppDbContext context,
            CancellationToken cancellationToken)
        {
            return await context.ApplicationUsers.AnyAsync(cancellationToken)
                || await context.GeneralDepartments.AnyAsync(cancellationToken)
                || await context.Departments.AnyAsync(cancellationToken)
                || await context.Branches.AnyAsync(cancellationToken)
                || await context.Countries.AnyAsync(cancellationToken)
                || await context.Cities.AnyAsync(cancellationToken)
                || await context.Towns.AnyAsync(cancellationToken)
                || await context.OvertimeTypes.AnyAsync(cancellationToken)
                || await context.SanctionTypes.AnyAsync(cancellationToken)
                || await context.VacationTypes.AnyAsync(cancellationToken)
                || await context.Employees.AnyAsync(cancellationToken)
                || await context.Doctors.AnyAsync(cancellationToken)
                || await context.Overtimes.AnyAsync(cancellationToken)
                || await context.Sanctions.AnyAsync(cancellationToken)
                || await context.Vacations.AnyAsync(cancellationToken);
        }

        private static async Task<Dictionary<string, SystemRole>> EnsureRolesAsync(
            AppDbContext context,
            CancellationToken cancellationToken)
        {
            var roles = await context.SystemRoles
                .Where(role => role.Name == Constants.Admin || role.Name == Constants.User)
                .ToListAsync(cancellationToken);

            var existingRoles = roles.ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);
            var rolesAdded = false;

            if (!existingRoles.ContainsKey(Constants.Admin))
            {
                context.SystemRoles.Add(new SystemRole { Name = Constants.Admin });
                rolesAdded = true;
            }

            if (!existingRoles.ContainsKey(Constants.User))
            {
                context.SystemRoles.Add(new SystemRole { Name = Constants.User });
                rolesAdded = true;
            }

            if (rolesAdded)
            {
                await context.SaveChangesAsync(cancellationToken);
                roles = await context.SystemRoles
                    .Where(role => role.Name == Constants.Admin || role.Name == Constants.User)
                    .ToListAsync(cancellationToken);
            }

            return roles.ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task SeedUsersAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, SystemRole> roles,
            CancellationToken cancellationToken)
        {
            var demoUsers = new[]
            {
                new DemoUserSeed("System Administrator", "admin@ems.local", "Admin123!", Constants.Admin),
                new DemoUserSeed("Olivia Carter", "hr@ems.local", "User123!", Constants.User),
                new DemoUserSeed("Ethan Brooks", "manager@ems.local", "User123!", Constants.User)
            };

            var applicationUsers = demoUsers
                .Select(user => new ApplicationUser
                {
                    Fullname = user.Fullname,
                    Email = user.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(user.Password)
                })
                .ToList();

            await context.ApplicationUsers.AddRangeAsync(applicationUsers, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var userRoles = applicationUsers
                .Zip(demoUsers, (user, seed) => new UserRole
                {
                    UserId = user.Id,
                    RoleId = roles[seed.RoleName].Id
                })
                .ToList();

            await context.UserRoles.AddRangeAsync(userRoles, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task<Dictionary<string, GeneralDepartment>> SeedGeneralDepartmentsAsync(
            AppDbContext context,
            CancellationToken cancellationToken)
        {
            var items = new[]
            {
                new GeneralDepartment { Name = "Information Technology" },
                new GeneralDepartment { Name = "Human Resources" },
                new GeneralDepartment { Name = "Sales" },
                new GeneralDepartment { Name = "Marketing" },
                new GeneralDepartment { Name = "Finance" },
                new GeneralDepartment { Name = "Operations" }
            };

            await context.GeneralDepartments.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, Department>> SeedDepartmentsAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, GeneralDepartment> generalDepartments,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new NameWithParentSeed("Infrastructure", "Information Technology"),
                new NameWithParentSeed("Quality Assurance", "Information Technology"),
                new NameWithParentSeed("Recruitment", "Human Resources"),
                new NameWithParentSeed("Employee Relations", "Human Resources"),
                new NameWithParentSeed("Corporate Sales", "Sales"),
                new NameWithParentSeed("Digital Marketing", "Marketing"),
                new NameWithParentSeed("Accounting", "Finance"),
                new NameWithParentSeed("Logistics", "Operations")
            };

            var items = seeds
                .Select(seed => new Department
                {
                    Name = seed.Name,
                    GeneralDepartmentId = generalDepartments[seed.ParentName].Id
                })
                .ToList();

            await context.Departments.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, Branch>> SeedBranchesAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, Department> departments,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new NameWithParentSeed("Dublin Tech Hub", "Infrastructure"),
                new NameWithParentSeed("Dublin QA Center", "Quality Assurance"),
                new NameWithParentSeed("Madrid Talent Office", "Recruitment"),
                new NameWithParentSeed("Barcelona People Office", "Employee Relations"),
                new NameWithParentSeed("London Sales Branch", "Corporate Sales"),
                new NameWithParentSeed("Berlin Marketing Studio", "Digital Marketing"),
                new NameWithParentSeed("Paris Finance Desk", "Accounting"),
                new NameWithParentSeed("Amsterdam Operations Hub", "Logistics")
            };

            var items = seeds
                .Select(seed => new Branch
                {
                    Name = seed.Name,
                    DepartmentId = departments[seed.ParentName].Id
                })
                .ToList();

            await context.Branches.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, Country>> SeedCountriesAsync(
            AppDbContext context,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new CountrySeed("Ireland", "IE"),
                new CountrySeed("Spain", "ES"),
                new CountrySeed("United Kingdom", "GB"),
                new CountrySeed("Germany", "DE"),
                new CountrySeed("France", "FR"),
                new CountrySeed("Netherlands", "NL")
            };

            var items = seeds
                .Select(seed => new Country
                {
                    Name = seed.Name,
                    Code2 = seed.Code2,
                    Source = "Mock",
                    FlagUrl = $"https://flagcdn.com/w80/{seed.Code2.ToLowerInvariant()}.png",
                    LastSyncedAtUtc = DemoCountrySyncedAtUtc
                })
                .ToList();

            await context.Countries.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, City>> SeedCitiesAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, Country> countries,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new NameWithParentSeed("Dublin", "Ireland"),
                new NameWithParentSeed("Madrid", "Spain"),
                new NameWithParentSeed("London", "United Kingdom"),
                new NameWithParentSeed("Berlin", "Germany"),
                new NameWithParentSeed("Paris", "France"),
                new NameWithParentSeed("Amsterdam", "Netherlands")
            };

            var items = seeds
                .Select(seed => new City
                {
                    Name = seed.Name,
                    CountryId = countries[seed.ParentName].Id
                })
                .ToList();

            await context.Cities.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, Town>> SeedTownsAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, City> cities,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new NameWithParentSeed("Dublin", "Dublin"),
                new NameWithParentSeed("Madrid", "Madrid"),
                new NameWithParentSeed("London", "London"),
                new NameWithParentSeed("Berlin", "Berlin"),
                new NameWithParentSeed("Paris", "Paris"),
                new NameWithParentSeed("Amsterdam", "Amsterdam")
            };

            var items = seeds
                .Select(seed => new Town
                {
                    Name = seed.Name,
                    CityId = cities[seed.ParentName].Id
                })
                .ToList();

            await context.Towns.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, OvertimeType>> SeedOvertimeTypesAsync(
            AppDbContext context,
            CancellationToken cancellationToken)
        {
            var items = new[]
            {
                new OvertimeType { Name = "Regular Overtime" },
                new OvertimeType { Name = "Weekend Overtime" },
                new OvertimeType { Name = "Holiday Overtime" },
                new OvertimeType { Name = "Night Shift Overtime" },
                new OvertimeType { Name = "Emergency Overtime" }
            };

            await context.OvertimeTypes.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, SanctionType>> SeedSanctionTypesAsync(
            AppDbContext context,
            CancellationToken cancellationToken)
        {
            var items = new[]
            {
                new SanctionType { Name = "Verbal Warning" },
                new SanctionType { Name = "Written Warning" },
                new SanctionType { Name = "Final Warning" },
                new SanctionType { Name = "Suspension" },
                new SanctionType { Name = "Policy Violation" }
            };

            await context.SanctionTypes.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, VacationType>> SeedVacationTypesAsync(
            AppDbContext context,
            CancellationToken cancellationToken)
        {
            var items = new[]
            {
                new VacationType { Name = "Annual Leave" },
                new VacationType { Name = "Sick Leave" },
                new VacationType { Name = "Maternity Leave" },
                new VacationType { Name = "Paternity Leave" },
                new VacationType { Name = "Unpaid Leave" }
            };

            await context.VacationTypes.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<Dictionary<string, Employee>> SeedEmployeesAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, Branch> branches,
            IReadOnlyDictionary<string, Town> towns,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new EmployeeSeed(
                    "Kevin Walsh",
                    "CIV-001",
                    "EMP-001",
                    "Infrastructure Engineer",
                    "14 River Liffey Quay, Dublin",
                    "+353 1 555 0101",
                    "Dublin Tech Hub",
                    "Dublin",
                    "Azure infrastructure lead for the platform squad.",
                    "#1F3C88",
                    "#F6F1E9"),
                new EmployeeSeed(
                    "Laura Garcia",
                    "CIV-002",
                    "EMP-002",
                    "Talent Acquisition Specialist",
                    "28 Gran Via, Madrid",
                    "+34 91 555 0102",
                    "Madrid Talent Office",
                    "Madrid",
                    "Focuses on technical recruitment across Iberia.",
                    "#8A1538",
                    "#FFF4E6"),
                new EmployeeSeed(
                    "Daniel Smith",
                    "CIV-003",
                    "EMP-003",
                    "Account Executive",
                    "52 Bishopsgate, London",
                    "+44 20 5550 0103",
                    "London Sales Branch",
                    "London",
                    "Owns enterprise accounts in the UK market.",
                    "#0B6E4F",
                    "#F4F1DE"),
                new EmployeeSeed(
                    "Sofia Martinez",
                    "CIV-004",
                    "EMP-004",
                    "People Operations Partner",
                    "91 Calle de Alcala, Madrid",
                    "+34 91 555 0104",
                    "Barcelona People Office",
                    "Madrid",
                    "Supports onboarding, policy updates, and employee relations.",
                    "#D35400",
                    "#FFF7ED"),
                new EmployeeSeed(
                    "Marta Fischer",
                    "CIV-005",
                    "EMP-005",
                    "Marketing Campaign Manager",
                    "18 Friedrichstrasse, Berlin",
                    "+49 30 555 0105",
                    "Berlin Marketing Studio",
                    "Berlin",
                    "Leads digital campaign planning for central Europe.",
                    "#6C3483",
                    "#F8F4FF"),
                new EmployeeSeed(
                    "Lucas Bernard",
                    "CIV-006",
                    "EMP-006",
                    "Financial Analyst",
                    "7 Rue de Rivoli, Paris",
                    "+33 1 55 50 0106",
                    "Paris Finance Desk",
                    "Paris",
                    "Owns monthly forecasting and budget tracking.",
                    "#2E4053",
                    "#F4F6F7"),
                new EmployeeSeed(
                    "Emma Johnson",
                    "CIV-007",
                    "EMP-007",
                    "QA Analyst",
                    "5 Spencer Dock, Dublin",
                    "+353 1 555 0107",
                    "Dublin QA Center",
                    "Dublin",
                    "Maintains regression coverage for release candidates.",
                    "#146356",
                    "#F2F7F2"),
                new EmployeeSeed(
                    "Noah de Vries",
                    "CIV-008",
                    "EMP-008",
                    "Operations Coordinator",
                    "33 Singel, Amsterdam",
                    "+31 20 555 0108",
                    "Amsterdam Operations Hub",
                    "Amsterdam",
                    "Coordinates vendors, logistics planning, and office services.",
                    "#1B4F72",
                    "#EEF6FB")
            };

            var items = seeds
                .Select(seed => new Employee
                {
                    Name = seed.Name,
                    CivilId = seed.CivilId,
                    FileNumber = seed.FileNumber,
                    JobName = seed.JobName,
                    Address = seed.Address,
                    TelephoneNumber = seed.TelephoneNumber,
                    Photo = BuildAvatar(seed.Name, seed.BackgroundColor, seed.ForegroundColor),
                    Other = seed.Other,
                    BranchId = branches[seed.BranchName].Id,
                    TownId = towns[seed.TownName].Id
                })
                .ToList();

            await context.Employees.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task SeedDoctorsAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, Employee> employees,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new DoctorSeed(
                    "Kevin Walsh",
                    new DateTime(2026, 1, 15),
                    "Mild lower back strain after an extended maintenance shift.",
                    "Physiotherapy stretches twice daily and no heavy lifting for one week."),
                new DoctorSeed(
                    "Laura Garcia",
                    new DateTime(2026, 1, 20),
                    "Seasonal flu symptoms with moderate fatigue.",
                    "Rest for 48 hours, hydration, and remote work on recovery days."),
                new DoctorSeed(
                    "Daniel Smith",
                    new DateTime(2026, 2, 3),
                    "Migraine episode triggered by travel fatigue.",
                    "Reduce screen exposure for 24 hours and resume work gradually."),
                new DoctorSeed(
                    "Marta Fischer",
                    new DateTime(2026, 2, 10),
                    "Wrist tendon irritation from repetitive keyboard use.",
                    "Use ergonomic support and avoid long typing sessions for five days."),
                new DoctorSeed(
                    "Emma Johnson",
                    new DateTime(2026, 2, 18),
                    "Routine health review with no critical findings.",
                    "Continue regular exercise and schedule the next check-up in six months.")
            };

            var items = seeds
                .Select(seed => new Doctor
                {
                    EmployeeId = employees[seed.EmployeeName].Id,
                    Date = seed.Date,
                    MedicalDiagnose = seed.MedicalDiagnose,
                    MedicalRecommendation = seed.MedicalRecommendation
                })
                .ToList();

            await context.Doctors.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedOvertimesAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, Employee> employees,
            IReadOnlyDictionary<string, OvertimeType> overtimeTypes,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new ChildTypeSeed("Kevin Walsh", "Weekend Overtime", new DateTime(2026, 3, 1)),
                new ChildTypeSeed("Laura Garcia", "Regular Overtime", new DateTime(2026, 3, 2)),
                new ChildTypeSeed("Daniel Smith", "Holiday Overtime", new DateTime(2026, 3, 3)),
                new ChildTypeSeed("Sofia Martinez", "Night Shift Overtime", new DateTime(2026, 3, 4)),
                new ChildTypeSeed("Emma Johnson", "Emergency Overtime", new DateTime(2026, 3, 5)),
                new ChildTypeSeed("Noah de Vries", "Weekend Overtime", new DateTime(2026, 3, 6))
            };

            var items = seeds
                .Select(seed =>
                {
                    var overtimeType = overtimeTypes[seed.TypeName];

                    return new Overtime
                    {
                        EmployeeId = employees[seed.EmployeeName].Id,
                        StartDate = seed.Date,
                        EndDate = seed.Date.AddDays(1),
                        OvertimeTypeld = overtimeType.Id,
                        OvertimeType = overtimeType
                    };
                })
                .ToList();

            await context.Overtimes.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedSanctionsAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, Employee> employees,
            IReadOnlyDictionary<string, SanctionType> sanctionTypes,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new SanctionSeed(
                    "Daniel Smith",
                    "Verbal Warning",
                    new DateTime(2026, 1, 10),
                    new DateTime(2026, 1, 12),
                    "Coaching session and attendance review."),
                new SanctionSeed(
                    "Sofia Martinez",
                    "Written Warning",
                    new DateTime(2026, 1, 18),
                    new DateTime(2026, 1, 20),
                    "Formal reminder to follow approval workflows."),
                new SanctionSeed(
                    "Marta Fischer",
                    "Policy Violation",
                    new DateTime(2026, 2, 5),
                    new DateTime(2026, 2, 7),
                    "Mandatory retraining on campaign compliance policies."),
                new SanctionSeed(
                    "Lucas Bernard",
                    "Verbal Warning",
                    new DateTime(2026, 2, 12),
                    new DateTime(2026, 2, 13),
                    "Documented coaching on reporting deadlines."),
                new SanctionSeed(
                    "Noah de Vries",
                    "Final Warning",
                    new DateTime(2026, 2, 20),
                    new DateTime(2026, 2, 24),
                    "Final written warning tied to repeated process deviations.")
            };

            var items = seeds
                .Select(seed => new Sanction
                {
                    EmployeeId = employees[seed.EmployeeName].Id,
                    SanctionTypeId = sanctionTypes[seed.SanctionTypeName].Id,
                    Date = seed.Date,
                    PunishmentDate = seed.PunishmentDate,
                    Punishment = seed.Punishment
                })
                .ToList();

            await context.Sanctions.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedVacationsAsync(
            AppDbContext context,
            IReadOnlyDictionary<string, Employee> employees,
            IReadOnlyDictionary<string, VacationType> vacationTypes,
            CancellationToken cancellationToken)
        {
            var seeds = new[]
            {
                new VacationSeed("Kevin Walsh", "Annual Leave", new DateTime(2026, 4, 1), 5),
                new VacationSeed("Laura Garcia", "Sick Leave", new DateTime(2026, 4, 3), 2),
                new VacationSeed("Emma Johnson", "Annual Leave", new DateTime(2026, 4, 8), 7),
                new VacationSeed("Marta Fischer", "Unpaid Leave", new DateTime(2026, 4, 10), 3),
                new VacationSeed("Lucas Bernard", "Paternity Leave", new DateTime(2026, 4, 12), 10),
                new VacationSeed("Sofia Martinez", "Annual Leave", new DateTime(2026, 4, 15), 4)
            };

            var items = seeds
                .Select(seed => new Vacation
                {
                    EmployeeId = employees[seed.EmployeeName].Id,
                    VacationTypeId = vacationTypes[seed.VacationTypeName].Id,
                    StartDate = seed.StartDate,
                    NumberOfDays = seed.NumberOfDays
                })
                .ToList();

            await context.Vacations.AddRangeAsync(items, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static string BuildAvatar(string fullName, string backgroundColor, string foregroundColor)
        {
            var initials = string.Concat(
                fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(part => char.ToUpperInvariant(part[0])));

            var svg =
                "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 120 120'>" +
                $"<rect width='120' height='120' rx='24' fill='{backgroundColor}' />" +
                $"<text x='50%' y='52%' text-anchor='middle' dominant-baseline='middle' fill='{foregroundColor}' " +
                "font-family='Segoe UI, Arial, sans-serif' font-size='40' font-weight='700'>" +
                $"{initials}</text></svg>";

            return $"data:image/svg+xml;utf8,{Uri.EscapeDataString(svg)}";
        }

        private sealed record DemoUserSeed(string Fullname, string Email, string Password, string RoleName);
        private sealed record NameWithParentSeed(string Name, string ParentName);
        private sealed record CountrySeed(string Name, string Code2);
        private sealed record EmployeeSeed(
            string Name,
            string CivilId,
            string FileNumber,
            string JobName,
            string Address,
            string TelephoneNumber,
            string BranchName,
            string TownName,
            string Other,
            string BackgroundColor,
            string ForegroundColor);
        private sealed record DoctorSeed(
            string EmployeeName,
            DateTime Date,
            string MedicalDiagnose,
            string MedicalRecommendation);
        private sealed record ChildTypeSeed(string EmployeeName, string TypeName, DateTime Date);
        private sealed record SanctionSeed(
            string EmployeeName,
            string SanctionTypeName,
            DateTime Date,
            DateTime PunishmentDate,
            string Punishment);
        private sealed record VacationSeed(
            string EmployeeName,
            string VacationTypeName,
            DateTime StartDate,
            int NumberOfDays);
    }
}
