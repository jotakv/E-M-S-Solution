using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerLibrary.Helpers;
using System.Text.Json;
using ServerLibrary.Data.DTO;

namespace ServerLibrary.Data
{
    public static class DevelopmentDataSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider
                .GetService<ILoggerFactory>()?
                .CreateLogger(nameof(DevelopmentDataSeeder));

            logger?.LogInformation("Applying migrations...");
            await context.Database.MigrateAsync();

            await SeedAsync(context, logger);
        }

        public static async Task SeedAsync(
            AppDbContext context,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var seedData = await LoadSeedDataAsync();

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                logger?.LogInformation("Starting seeding process...");

                var roles = await EnsureRolesAsync(context, seedData, logger, cancellationToken);
                var users = await SeedUsersOnlyAsync(context, seedData, logger, cancellationToken);
                await SeedUserRolesAsync(context, seedData, users, roles, logger, cancellationToken);

                var generalDepartments = await SeedGeneralDepartmentsAsync(context, seedData, logger, cancellationToken);
                var departments = await SeedDepartmentsAsync(context, seedData, generalDepartments, logger, cancellationToken);
                var branches = await SeedBranchesAsync(context, seedData, departments, logger, cancellationToken);

                var countries = await SeedCountriesAsync(context, seedData, logger, cancellationToken);
                var cities = await SeedCitiesAsync(context, seedData, countries, logger, cancellationToken);
                var towns = await SeedTownsAsync(context, seedData, cities, logger, cancellationToken);

                var overtimeTypes = await SeedOvertimeTypesAsync(context, seedData, logger, cancellationToken);
                var sanctionTypes = await SeedSanctionTypesAsync(context, seedData, logger, cancellationToken);
                var vacationTypes = await SeedVacationTypesAsync(context, seedData, logger, cancellationToken);

                var employees = await SeedEmployeesAsync(context, seedData, branches, towns, logger, cancellationToken);

                await SeedDoctorsAsync(context, seedData, employees, logger, cancellationToken);
                await SeedOvertimesAsync(context, seedData, employees, overtimeTypes, logger, cancellationToken);
                await SeedSanctionsAsync(context, seedData, employees, sanctionTypes, logger, cancellationToken);
                await SeedVacationsAsync(context, seedData, employees, vacationTypes, logger, cancellationToken);
                await SeedEmployeeNotesAsync(context, employees, logger, cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger?.LogInformation("Seeding completed successfully.");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Seeding failed. Rolling back...");
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        #region Load JSON

        private static async Task<SeedData> LoadSeedDataAsync()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "development-seed.json");

            if (!File.Exists(path))
                throw new FileNotFoundException("Seed file not found", path);

            var json = await File.ReadAllTextAsync(path);

            return JsonSerializer.Deserialize<SeedData>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

        #endregion

        #region Generic Seed Helpers

        // Usado por entidades cuyo nombre es globalmente único (Country, GeneralDepartment, OvertimeType, etc.)
        private static async Task<Dictionary<string, T>> AddIfNotExistsAsync<T>(
            DbSet<T> dbSet,
            IEnumerable<T> items,
            Func<T, string?> keySelector,
            ILogger? logger,
            CancellationToken ct) where T : class
        {
            var existing = await dbSet.ToListAsync(ct);
            var dict = existing.ToDictionary(x => keySelector(x)!, StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                var key = keySelector(item)!;

                if (!dict.ContainsKey(key))
                {
                    await dbSet.AddAsync(item, ct);
                    dict[key] = item;
                }
            }

            return dict;
        }

        // Usado por entidades cuya unicidad depende de un padre (City, Town, Department, Branch)
        // La clave compuesta tiene formato "ParentName|EntityName"
        private static async Task<Dictionary<string, T>> AddIfNotExistsWithCompositeKeyAsync<T>(
            DbSet<T> dbSet,
            IEnumerable<(string CompositeKey, T Entity)> items,
            ILogger? logger,
            CancellationToken ct) where T : class
        {
            var existing = await dbSet.ToListAsync(ct);

            // El diccionario de existentes se construye fuera de este helper
            // porque cada entidad necesita su propia lógica de composite key
            var dict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

            foreach (var (compositeKey, entity) in items)
            {
                if (!dict.ContainsKey(compositeKey))
                {
                    await dbSet.AddAsync(entity, ct);
                    dict[compositeKey] = entity;
                }
            }

            return dict;
        }

        private static string CompositeKey(params string[] parts)
            => string.Join("|", parts);

        #endregion

        #region Roles & Users

        private static async Task<Dictionary<string, SystemRole>> EnsureRolesAsync(
            AppDbContext context,
            SeedData seedData,
            ILogger? logger,
            CancellationToken ct)
        {
            var existing = await context.SystemRoles.ToListAsync(ct);
            var dict = existing.ToDictionary(x => x.Name!, StringComparer.OrdinalIgnoreCase);

            foreach (var roleName in seedData.Roles)
            {
                if (!dict.ContainsKey(roleName))
                {
                    var role = new SystemRole { Name = roleName };
                    context.SystemRoles.Add(role);
                    dict[roleName] = role;
                    logger?.LogInformation("Added role {Role}", roleName);
                }
            }

            return dict;
        }

        private static async Task<Dictionary<string, ApplicationUser>> SeedUsersOnlyAsync(
            AppDbContext context,
            SeedData seedData,
            ILogger? logger,
            CancellationToken ct)
        {
            var existing = await context.ApplicationUsers
                .ToDictionaryAsync(x => x.Email!, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var dto in seedData.Users)
            {
                if (!existing.ContainsKey(dto.Email))
                {
                    var user = new ApplicationUser
                    {
                        Fullname = dto.Fullname,
                        Email = dto.Email,
                        Password = BCrypt.Net.BCrypt.HashPassword(dto.Password)
                    };

                    context.ApplicationUsers.Add(user);
                    existing[dto.Email] = user;
                    logger?.LogInformation("Added user {Email}", dto.Email);
                }
            }

            return existing;
        }

        private static async Task SeedUserRolesAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, ApplicationUser> users,
            Dictionary<string, SystemRole> roles,
            ILogger? logger,
            CancellationToken ct)
        {
            var existingInDb = await context.UserRoles
                .Select(x => new { x.UserId, x.RoleId })
                .ToListAsync(ct);

            var addedInMemory = new HashSet<(int UserId, int RoleId)>();

            foreach (var dto in seedData.Users)
            {
                if (!roles.TryGetValue(dto.Role, out var role))
                    throw new Exception($"Role not found: {dto.Role}");

                if (!users.TryGetValue(dto.Email, out var user))
                    throw new Exception($"User not found: {dto.Email}");

                var alreadyInDb = existingInDb.Any(x => x.UserId == user.Id && x.RoleId == role.Id);
                var alreadyInMemory = addedInMemory.Contains((user.Id, role.Id));

                if (!alreadyInDb && !alreadyInMemory)
                {
                    context.UserRoles.Add(new UserRole { User = user, Role = role });
                    addedInMemory.Add((user.Id, role.Id));
                    logger?.LogInformation("Assigned role {Role} to user {Email}", dto.Role, dto.Email);
                }
            }
        }

        #endregion

        #region Organization

        private static async Task<Dictionary<string, GeneralDepartment>> SeedGeneralDepartmentsAsync(
            AppDbContext context,
            SeedData seedData,
            ILogger? logger,
            CancellationToken ct)
        {
            return await AddIfNotExistsAsync(
                context.GeneralDepartments,
                seedData.GeneralDepartments.Select(x => new GeneralDepartment { Name = x.Name }),
                x => x.Name,
                logger,
                ct);
        }

        // Clave compuesta: "GeneralDepartmentName|DepartmentName"
        private static async Task<Dictionary<string, Department>> SeedDepartmentsAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, GeneralDepartment> generalDepartments,
            ILogger? logger,
            CancellationToken ct)
        {
            var existingInDb = await context.Departments
                .Include(d => d.GeneralDepartment)
                .ToListAsync(ct);

            var dict = existingInDb.ToDictionary(
                d => CompositeKey(d.GeneralDepartment!.Name!, d.Name!),
                StringComparer.OrdinalIgnoreCase);

            foreach (var dto in seedData.Departments)
            {
                if (!generalDepartments.TryGetValue(dto.GeneralDepartment, out var parent))
                    throw new Exception(
                        $"Department '{dto.Name}' references GeneralDepartment '{dto.GeneralDepartment}' " +
                        $"which was not found. Available: [{string.Join(", ", generalDepartments.Keys)}]");

                var key = CompositeKey(dto.GeneralDepartment, dto.Name);

                if (!dict.ContainsKey(key))
                {
                    var department = new Department { Name = dto.Name, GeneralDepartment = parent };
                    context.Departments.Add(department);
                    dict[key] = department;
                    logger?.LogInformation("Added department {Department} under {GeneralDepartment}", dto.Name, dto.GeneralDepartment);
                }
            }

            return dict;
        }

        // Clave compuesta: "GeneralDepartmentName|DepartmentName|BranchName"
        private static async Task<Dictionary<string, Branch>> SeedBranchesAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, Department> departments,
            ILogger? logger,
            CancellationToken ct)
        {
            var existingInDb = await context.Branches
                .Include(b => b.Department)
#pragma warning disable CS8602
                    .ThenInclude(d => d.GeneralDepartment)
#pragma warning restore CS8602
                .ToListAsync(ct);

            var dict = existingInDb.ToDictionary(
                b => CompositeKey(b.Department!.GeneralDepartment!.Name!, b.Department.Name!, b.Name!),
                StringComparer.OrdinalIgnoreCase);

            foreach (var dto in seedData.Branches)
            {
                // El diccionario de departments usa clave compuesta "GeneralDepartment|Department"
                // por lo que buscamos por nombre simple dentro de los valores
                var deptEntry = departments.FirstOrDefault(kvp =>
                    kvp.Key.EndsWith($"|{dto.Department}", StringComparison.OrdinalIgnoreCase));

                if (deptEntry.Value is null)
                    throw new Exception(
                        $"Branch '{dto.Name}' references Department '{dto.Department}' " +
                        $"which was not found. Available: [{string.Join(", ", departments.Keys)}]");

                var generalDeptName = deptEntry.Key.Split('|')[0];
                var key = CompositeKey(generalDeptName, dto.Department, dto.Name);

                if (!dict.ContainsKey(key))
                {
                    var branch = new Branch { Name = dto.Name, Department = deptEntry.Value };
                    context.Branches.Add(branch);
                    dict[key] = branch;
                    logger?.LogInformation("Added branch {Branch} under {Department}", dto.Name, dto.Department);
                }
            }

            return dict;
        }

        private static async Task<Dictionary<string, Employee>> SeedEmployeesAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, Branch> branches,
            Dictionary<string, Town> towns,
            ILogger? logger,
            CancellationToken ct)
        {
            var existing = await context.Employees
                .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var dto in seedData.Employees)
            {
                if (existing.ContainsKey(dto.Name))
                    continue;

                // Branches usa clave compuesta — buscamos por el sufijo que contiene el nombre del branch
                var branch = branches.Values.FirstOrDefault(b =>
                    string.Equals(b.Name, dto.Branch, StringComparison.OrdinalIgnoreCase));

                if (branch is null)
                    throw new Exception(
                        $"Employee '{dto.Name}' references Branch '{dto.Branch}' " +
                        $"which was not found.");

                // Towns usa clave compuesta — buscamos por el sufijo que contiene el nombre del town
                var town = towns.Values.FirstOrDefault(t =>
                    string.Equals(t.Name, dto.Town, StringComparison.OrdinalIgnoreCase));

                if (town is null)
                    throw new Exception(
                        $"Employee '{dto.Name}' references Town '{dto.Town}' " +
                        $"which was not found.");

                var employee = new Employee
                {
                    Name = dto.Name,
                    CivilId = dto.CivilId,
                    FileNumber = dto.FileNumber,
                    JobName = dto.JobName,
                    Address = dto.Address,
                    TelephoneNumber = dto.TelephoneNumber,
                    Branch = branch,
                    Town = town,
                    Other = dto.Other,
                    Photo = BuildAvatar(dto.Name, dto.BackgroundColor, dto.ForegroundColor)
                };

                context.Employees.Add(employee);
                existing[dto.Name] = employee;
                logger?.LogInformation("Added employee {Name}", dto.Name);
            }

            return existing;
        }

        #endregion

        #region Location

        private static async Task<Dictionary<string, Country>> SeedCountriesAsync(
            AppDbContext context,
            SeedData seedData,
            ILogger? logger,
            CancellationToken ct)
        {
            return await AddIfNotExistsAsync(
                context.Countries,
                seedData.Countries.Select(x => new Country
                {
                    Name = x.Name,
                    Code2 = x.Code2,
                    Source = "Mock",
                    FlagUrl = $"https://flagcdn.com/w80/{x.Code2.ToLower()}.png",
                    LastSyncedAtUtc = DateTime.UtcNow
                }),
                x => x.Name,
                logger,
                ct);
        }

        // Clave compuesta: "CountryName|CityName"
        private static async Task<Dictionary<string, City>> SeedCitiesAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, Country> countries,
            ILogger? logger,
            CancellationToken ct)
        {
            var existingInDb = await context.Cities
                .Include(c => c.Country)
                .ToListAsync(ct);

            var dict = existingInDb.ToDictionary(
                c => CompositeKey(c.Country!.Name!, c.Name!),
                StringComparer.OrdinalIgnoreCase);

            foreach (var dto in seedData.Cities)
            {
                if (!countries.TryGetValue(dto.Country, out var country))
                    throw new Exception(
                        $"City '{dto.Name}' references Country '{dto.Country}' " +
                        $"which was not found. Available: [{string.Join(", ", countries.Keys)}]");

                var key = CompositeKey(dto.Country, dto.Name);

                if (!dict.ContainsKey(key))
                {
                    var city = new City { Name = dto.Name, Country = country };
                    context.Cities.Add(city);
                    dict[key] = city;
                    logger?.LogInformation("Added city {City} in {Country}", dto.Name, dto.Country);
                }
            }

            return dict;
        }

        // Clave compuesta: "CountryName|CityName|TownName"
        private static async Task<Dictionary<string, Town>> SeedTownsAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, City> cities,
            ILogger? logger,
            CancellationToken ct)
        {
            var existingInDb = await context.Towns
                .Include(t => t.City)
#pragma warning disable CS8602
                    .ThenInclude(c => c.Country)
#pragma warning restore CS8602
                .ToListAsync(ct);

            var dict = existingInDb.ToDictionary(
                t => CompositeKey(t.City!.Country!.Name!, t.City.Name!, t.Name!),
                StringComparer.OrdinalIgnoreCase);

            foreach (var dto in seedData.Towns)
            {
                // Lookup exacto con clave compuesta "CountryName|CityName"
                // TownDto incluye Country para resolver ciudades homónimas en distintos países
                var cityKey = CompositeKey(dto.Country, dto.City);

                if (!cities.TryGetValue(cityKey, out var city))
                    throw new Exception(
                        $"Town '{dto.Name}' references City '{dto.City}' in Country '{dto.Country}' " +
                        $"which was not found. Available cities: [{string.Join(", ", cities.Keys)}]");

                var key = CompositeKey(dto.Country, dto.City, dto.Name);

                if (!dict.ContainsKey(key))
                {
                    var town = new Town { Name = dto.Name, City = city };
                    context.Towns.Add(town);
                    dict[key] = town;
                    logger?.LogInformation("Added town {Town} in {City}, {Country}", dto.Name, dto.City, dto.Country);
                }
            }

            return dict;
        }

        #endregion

        #region Records

        private static async Task SeedDoctorsAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, Employee> employees,
            ILogger? logger,
            CancellationToken ct)
        {
            var existing = await context.Doctors
                .Select(x => new { x.EmployeeId, x.Date })
                .ToListAsync(ct);

            var addedInMemory = new HashSet<(string EmployeeName, DateTime Date)>();

            foreach (var dto in seedData.Doctors)
            {
                if (!employees.TryGetValue(dto.Employee, out var employee))
                    throw new Exception($"Employee not found: {dto.Employee}");

                var alreadyInDb = existing.Any(x => x.EmployeeId == employee.Id && x.Date == dto.Date);
                var alreadyInMemory = addedInMemory.Contains((dto.Employee, dto.Date));

                if (alreadyInDb || alreadyInMemory) continue;

                context.Doctors.Add(new Doctor
                {
                    Employee = employee,
                    Date = dto.Date,
                    MedicalDiagnose = dto.MedicalDiagnose,
                    MedicalRecommendation = dto.MedicalRecommendation
                });

                addedInMemory.Add((dto.Employee, dto.Date));
                logger?.LogInformation("Added doctor record for {Employee}", dto.Employee);
            }
        }

        private static async Task SeedOvertimesAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, Employee> employees,
            Dictionary<string, OvertimeType> types,
            ILogger? logger,
            CancellationToken ct)
        {
            var existing = await context.Overtimes
                .Select(x => new { x.EmployeeId, x.StartDate })
                .ToListAsync(ct);

            var addedInMemory = new HashSet<(string EmployeeName, DateTime StartDate)>();

            foreach (var dto in seedData.Overtimes)
            {
                if (!employees.TryGetValue(dto.Employee, out var employee))
                    throw new Exception($"Employee not found: {dto.Employee}");

                if (!types.TryGetValue(dto.Type, out var type))
                    throw new Exception($"OvertimeType not found: {dto.Type}");

                var alreadyInDb = existing.Any(x => x.EmployeeId == employee.Id && x.StartDate == dto.Date);
                var alreadyInMemory = addedInMemory.Contains((dto.Employee, dto.Date));

                if (alreadyInDb || alreadyInMemory) continue;

                context.Overtimes.Add(new Overtime
                {
                    Employee = employee,
                    StartDate = dto.Date,
                    EndDate = dto.Date.AddDays(1),
                    OvertimeType = type
                });

                addedInMemory.Add((dto.Employee, dto.Date));
                logger?.LogInformation("Added overtime for {Employee}", dto.Employee);
            }
        }

        private static async Task SeedSanctionsAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, Employee> employees,
            Dictionary<string, SanctionType> types,
            ILogger? logger,
            CancellationToken ct)
        {
            var existing = await context.Sanctions
                .Select(x => new { x.EmployeeId, x.Date })
                .ToListAsync(ct);

            var addedInMemory = new HashSet<(string EmployeeName, DateTime Date)>();

            foreach (var dto in seedData.Sanctions)
            {
                if (!employees.TryGetValue(dto.Employee, out var employee))
                    throw new Exception($"Employee not found: {dto.Employee}");

                if (!types.TryGetValue(dto.Type, out var type))
                    throw new Exception($"SanctionType not found: {dto.Type}");

                var alreadyInDb = existing.Any(x => x.EmployeeId == employee.Id && x.Date == dto.Date);
                var alreadyInMemory = addedInMemory.Contains((dto.Employee, dto.Date));

                if (alreadyInDb || alreadyInMemory) continue;

                context.Sanctions.Add(new Sanction
                {
                    Employee = employee,
                    SanctionType = type,
                    Date = dto.Date,
                    PunishmentDate = dto.PunishmentDate,
                    Punishment = dto.Punishment
                });

                addedInMemory.Add((dto.Employee, dto.Date));
                logger?.LogInformation("Added sanction for {Employee}", dto.Employee);
            }
        }

        private static async Task SeedVacationsAsync(
            AppDbContext context,
            SeedData seedData,
            Dictionary<string, Employee> employees,
            Dictionary<string, VacationType> types,
            ILogger? logger,
            CancellationToken ct)
        {
            var existing = await context.Vacations
                .Select(x => new { x.EmployeeId, x.StartDate })
                .ToListAsync(ct);

            var addedInMemory = new HashSet<(string EmployeeName, DateTime StartDate)>();

            foreach (var dto in seedData.Vacations)
            {
                if (!employees.TryGetValue(dto.Employee, out var employee))
                    throw new Exception($"Employee not found: {dto.Employee}");

                if (!types.TryGetValue(dto.Type, out var type))
                    throw new Exception($"VacationType not found: {dto.Type}");

                var alreadyInDb = existing.Any(x => x.EmployeeId == employee.Id && x.StartDate == dto.StartDate);
                var alreadyInMemory = addedInMemory.Contains((dto.Employee, dto.StartDate));

                if (alreadyInDb || alreadyInMemory) continue;

                context.Vacations.Add(new Vacation
                {
                    Employee = employee,
                    VacationType = type,
                    StartDate = dto.StartDate,
                    NumberOfDays = dto.NumberOfDays
                });

                addedInMemory.Add((dto.Employee, dto.StartDate));
                logger?.LogInformation("Added vacation for {Employee}", dto.Employee);
            }
        }

        #endregion

        #region Types

        private static Task<Dictionary<string, OvertimeType>> SeedOvertimeTypesAsync(
            AppDbContext context, SeedData seedData, ILogger? logger, CancellationToken ct)
            => AddIfNotExistsAsync(context.OvertimeTypes,
                seedData.OvertimeTypes.Select(x => new OvertimeType { Name = x.Name }),
                x => x.Name, logger, ct);

        private static Task<Dictionary<string, SanctionType>> SeedSanctionTypesAsync(
            AppDbContext context, SeedData seedData, ILogger? logger, CancellationToken ct)
            => AddIfNotExistsAsync(context.SanctionTypes,
                seedData.SanctionTypes.Select(x => new SanctionType { Name = x.Name }),
                x => x.Name, logger, ct);

        private static Task<Dictionary<string, VacationType>> SeedVacationTypesAsync(
            AppDbContext context, SeedData seedData, ILogger? logger, CancellationToken ct)
            => AddIfNotExistsAsync(context.VacationTypes,
                seedData.VacationTypes.Select(x => new VacationType { Name = x.Name }),
                x => x.Name, logger, ct);

        #endregion

        #region Avatar

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

        #endregion

        #region EmployeeNotes

        private static async Task SeedEmployeeNotesAsync(
            AppDbContext context,
            Dictionary<string, Employee> employees,
            ILogger? logger,
            CancellationToken ct)
        {
            // Only re-seed if we have fewer than 200 notes (allows topping up after JSON expansion)
            if (await context.EmployeeNotes.CountAsync(ct) >= 200)
                return;

            var empList = employees.Values.ToList();
            if (empList.Count == 0) return;

            var now = DateTime.UtcNow;
            var rng = new Random(42);

            var noteTemplates = new (string Text, string Label, float MinScore, float MaxScore)[]
            {
                // Positive
                ("Employee delivered the project ahead of schedule and received strong peer feedback.", "Positive", 0.75f, 0.95f),
                ("Consistently exceeds performance targets. A reliable team contributor.", "Positive", 0.78f, 0.92f),
                ("Excellent communication during the client presentation this quarter.", "Positive", 0.76f, 0.94f),
                ("Demonstrated great initiative on the new system rollout.", "Positive", 0.80f, 0.95f),
                ("Team members have praised this employee's collaborative approach.", "Positive", 0.75f, 0.90f),
                ("Received outstanding feedback from department review.", "Positive", 0.77f, 0.93f),
                ("Proactively identified a process bottleneck and proposed a solution.", "Positive", 0.79f, 0.94f),
                ("Mentored two junior staff members effectively this quarter.", "Positive", 0.76f, 0.91f),
                ("Achieved all KPIs and received a commendation from the department head.", "Positive", 0.82f, 0.96f),
                // Neutral
                ("Standard performance review completed. No significant concerns raised.", "Neutral", 0.40f, 0.60f),
                ("Attendance and punctuality are within acceptable range.", "Neutral", 0.42f, 0.58f),
                ("Mid-year check-in completed. Goals partially met.", "Neutral", 0.43f, 0.57f),
                ("Routine task completion observed. No notable positive or negative trends.", "Neutral", 0.41f, 0.59f),
                ("Employee noted some difficulty adapting to recent process changes.", "Neutral", 0.44f, 0.56f),
                ("Performance meets baseline expectations for the role.", "Neutral", 0.43f, 0.57f),
                ("Quarterly review documented. Development plan updated.", "Neutral", 0.40f, 0.60f),
                // Negative
                ("Employee reported feeling overwhelmed. Attendance has been declining this month.", "Negative", 0.10f, 0.32f),
                ("Multiple deadlines missed without prior communication to the team lead.", "Negative", 0.08f, 0.28f),
                ("Received formal complaint from a colleague regarding communication style.", "Negative", 0.09f, 0.28f),
                ("Performance has dropped significantly over the past quarter.", "Negative", 0.10f, 0.30f),
                ("Repeated tardiness flagged by the department manager.", "Negative", 0.08f, 0.28f),
                ("Escalation raised due to non-compliance with company policy.", "Negative", 0.07f, 0.26f),
                ("Team morale impacted by this employee's attitude during meetings.", "Negative", 0.09f, 0.27f),
                ("Second formal warning issued for conduct issues.", "Negative", 0.06f, 0.24f),
            };

            // ── Targeted high-risk notes for Kevin Walsh and Daniel Smith ──────────
            var highRiskNegatives = new string[]
            {
                "Multiple deadlines missed without prior communication to the team lead.",
                "Received formal complaint from a colleague regarding communication style.",
                "Performance has dropped significantly over the past quarter.",
                "Repeated tardiness flagged by the department manager.",
                "Escalation raised due to non-compliance with company policy.",
                "Second formal warning issued for conduct issues.",
            };

            foreach (var riskName in new[] { "Kevin Walsh", "Daniel Smith" })
            {
                if (!employees.TryGetValue(riskName, out var riskEmp)) continue;

                for (int i = 0; i < 6; i++)
                {
                    context.EmployeeNotes.Add(new BaseLibrary.Entities.EmployeeNote
                    {
                        EmployeeId      = riskEmp.Id,
                        NoteText        = highRiskNegatives[i],
                        SentimentLabel  = "Negative",
                        SentimentScore  = 0.08f + (float)rng.NextDouble() * 0.18f,
                        CreatedAt       = now.AddDays(-(i * 12 + rng.Next(1, 8))).AddHours(-rng.Next(0, 8)),
                        CreatedByUserId = "seed"
                    });
                }
            }

            // ── General pool: ~200 notes across all employees ────────────────────
            var schedule = new List<(int DaysAgo, string Label)>();

            // Last 30 days — 50 notes
            for (int i = 0; i < 50; i++)
                schedule.Add((rng.Next(1, 30), PickLabel(rng)));

            // 31-90 days — 60 notes
            for (int i = 0; i < 60; i++)
                schedule.Add((rng.Next(31, 90), PickLabel(rng)));

            // 91-365 days — 55 notes
            for (int i = 0; i < 55; i++)
                schedule.Add((rng.Next(91, 365), PickLabel(rng)));

            // Over 1 year — 35 notes
            for (int i = 0; i < 35; i++)
                schedule.Add((rng.Next(366, 600), PickLabel(rng)));

            int empIdx = 0;
            foreach (var (daysAgo, label) in schedule)
            {
                var emp       = empList[empIdx % empList.Count];
                var templates = noteTemplates.Where(t => t.Label == label).ToArray();
                var template  = templates[rng.Next(0, templates.Length)];

                context.EmployeeNotes.Add(new BaseLibrary.Entities.EmployeeNote
                {
                    EmployeeId      = emp.Id,
                    NoteText        = template.Text,
                    SentimentLabel  = template.Label,
                    SentimentScore  = template.MinScore + (float)rng.NextDouble() * (template.MaxScore - template.MinScore),
                    CreatedAt       = now.AddDays(-daysAgo).AddHours(-rng.Next(0, 8)),
                    CreatedByUserId = "seed"
                });

                empIdx++;
            }

            logger?.LogInformation("Seeded EmployeeNotes.");
        }

        private static string PickLabel(Random rng)
        {
            var roll = rng.NextDouble();
            return roll < 0.55 ? "Positive" : roll < 0.80 ? "Neutral" : "Negative";
        }

        #endregion
    }
}