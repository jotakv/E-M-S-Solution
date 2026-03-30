using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using BaseLibrary.Responses;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using ServerLibrary.Data;
using ServerLibrary.Helpers;
using ServerLibrary.Repositories.Contracts;
using ServiceStack.Text;
using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Constants = ServerLibrary.Helpers.Constants;


namespace ServerLibrary.Repositories.Implementations
{
    public class UserAccountRepository(
        IOptions<JwtSection> config,
        AppDbContext appDbContext,
        ILogger<UserAccountRepository> logger) : IUserAccount
    {
        public async Task<GeneralResponse> CreateAsync(Register user)
        {
            if (user == null)
            {
                logger.LogWarning("Registration attempted with an empty model");
                return new GeneralResponse(false, "Model is empty");
            }

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Email: {Email} | Result: {Result}",
                "UserRegister", "Register", "ApplicationUser", user.Email, "Attempt");

            var checkUser = await FindUserByEmail(user.Email!);
            if (checkUser != null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Email: {Email} | Result: {Result}",
                    "UserRegister", "Register", "ApplicationUser", user.Email, "Failure:EmailTaken");
                return new GeneralResponse(false, "User registered already");
            }

            // Create user
            var applicationUser = await AddToDatabase(new ApplicationUser()
            {
                Fullname = user.Fullname,
                Email    = user.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(user.Password)
            });

            // Ensure roles exist
            var adminRole = await appDbContext.SystemRoles
                .FirstOrDefaultAsync(r => r.Name == Constants.Admin);

            if (adminRole == null)
            {
                adminRole = await AddToDatabase(
                    new SystemRole { Name = Constants.Admin });
            }

            var userRole = await appDbContext.SystemRoles
                .FirstOrDefaultAsync(r => r.Name == Constants.User);

            if (userRole == null)
            {
                userRole = await AddToDatabase(
                    new SystemRole { Name = Constants.User });
            }

            // ALWAYS assign USER role on signup
            await AddToDatabase(new UserRole
            {
                UserId = applicationUser.Id,
                RoleId = userRole.Id
            });

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Email: {Email} | Role: {Role} | Result: {Result}",
                "UserRegister", "Register", "ApplicationUser",
                applicationUser.Id, applicationUser.Email, Constants.User, "Success");

            return new GeneralResponse(true, "Account created!");
        }

        public async Task<LoginResponse> SignInAsync(Login user)
        {
            if (user is null)
            {
                logger.LogWarning("Sign-in attempted with an empty model");
                return new LoginResponse(false, "Model is empty");
            }

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Email: {Email} | Result: {Result}",
                "UserLogin", "Login", "ApplicationUser", user.Email, "Attempt");

            // ── Overall login timer ───────────────────────────────────────────────
            var totalSw = Stopwatch.StartNew();

            // ── Phase 1: Database user lookup ─────────────────────────────────────
            // Slow here → DB connection issue, missing email index, or cold start.
            var lookupSw = Stopwatch.StartNew();
            var applicationUser = await FindUserByEmail(user.Email);
            lookupSw.Stop();
            logger.LogDebug(
                "Login perf phase 1 (user lookup): {ElapsedMs}ms for {Email}",
                lookupSw.ElapsedMilliseconds, user.Email);

            if (applicationUser is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Email: {Email} | Result: {Result}",
                    "UserLogin", "Login", "ApplicationUser", user.Email, "Failure:UserNotFound");
                return new LoginResponse(false, "User not found");
            }

            // ── Phase 2: BCrypt password verification (intentionally CPU-intensive) ──
            // BCrypt work-factor makes brute-force impractical but adds ~100–300ms.
            // If this phase is unexpectedly fast (<50ms), the hash may be weak.
            var bcryptSw = Stopwatch.StartNew();
            var passwordValid = BCrypt.Net.BCrypt.Verify(user.Password, applicationUser.Password);
            bcryptSw.Stop();
            logger.LogDebug(
                "Login perf phase 2 (BCrypt verify): {ElapsedMs}ms",
                bcryptSw.ElapsedMilliseconds);

            if (!passwordValid)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Email: {Email} | Result: {Result}",
                    "UserLogin", "Login", "ApplicationUser", user.Email, "Failure:InvalidPassword");
                return new LoginResponse(false, "Email/Password not valid");
            }

            // ── Phase 3: Role lookup ──────────────────────────────────────────────
            var roleSw = Stopwatch.StartNew();
            var userRole = await appDbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == applicationUser.Id);

            if (userRole is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Result: {Result}",
                    "UserLogin", "Login", "ApplicationUser", applicationUser.Id, "Failure:NoRoleAssigned");
                return new LoginResponse(false, "User has no role assigned");
            }

            var role = await appDbContext.SystemRoles
                .FirstOrDefaultAsync(r => r.Id == userRole.RoleId);

            if (role is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | RoleId: {RoleId} | Result: {Result}",
                    "UserLogin", "Login", "ApplicationUser", applicationUser.Id, userRole.RoleId, "Failure:RoleNotFound");
                return new LoginResponse(false, "Role not found");
            }

            roleSw.Stop();
            logger.LogDebug(
                "Login perf phase 3 (role lookup): {ElapsedMs}ms",
                roleSw.ElapsedMilliseconds);

            // ── Phase 4: Token generation + refresh token persistence ─────────────
            // Slow here → HMAC key derivation issue or DB write bottleneck.
            var tokenSw = Stopwatch.StartNew();
            string jwtToken     = GenerateToken(applicationUser, role.Name);
            string refreshToken = GenerateRefreshToken();

            var refresh = await appDbContext.RefreshTokenInfos
                .FirstOrDefaultAsync(r => r.UserId == applicationUser.Id);

            if (refresh != null)
            {
                refresh.Token = refreshToken;
            }
            else
            {
                await appDbContext.RefreshTokenInfos.AddAsync(
                    new RefreshTokenInfo { UserId = applicationUser.Id, Token = refreshToken });
            }

            await appDbContext.SaveChangesAsync();
            tokenSw.Stop();
            logger.LogDebug(
                "Login perf phase 4 (token gen + persist): {ElapsedMs}ms",
                tokenSw.ElapsedMilliseconds);

            // ── Total summary ─────────────────────────────────────────────────────
            totalSw.Stop();
            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Email: {Email} | Role: {Role} | Result: {Result} | " +
                "Perf: Lookup {LookupMs}ms | BCrypt {BCryptMs}ms | Roles {RolesMs}ms | Token {TokenMs}ms | Total {TotalMs}ms",
                "UserLogin", "Login", "ApplicationUser",
                applicationUser.Id, applicationUser.Email, role.Name, "Success",
                lookupSw.ElapsedMilliseconds, bcryptSw.ElapsedMilliseconds,
                roleSw.ElapsedMilliseconds, tokenSw.ElapsedMilliseconds,
                totalSw.ElapsedMilliseconds);

            if (totalSw.ElapsedMilliseconds > 1000)
            {
                logger.LogWarning(
                    "Slow login for {Email}: total {TotalMs}ms exceeds 1000ms threshold. " +
                    "BCrypt phase was {BCryptMs}ms (expected 100–400ms for work-factor 11).",
                    user.Email, totalSw.ElapsedMilliseconds, bcryptSw.ElapsedMilliseconds);
            }

            return new LoginResponse(true, "Login successfully", jwtToken, refreshToken);
        }


        private string GenerateToken(ApplicationUser user, string role)
        {
            var securitykey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Value.Key!));
            var credentials = new SigningCredentials(securitykey, SecurityAlgorithms.HmacSha256);
            var userclaims  = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name,            user.Fullname!),
                new Claim(ClaimTypes.Email,           user.Email!),
                new Claim("role",            role!),
            };

            var token = new JwtSecurityToken(
                issuer:            config.Value.Issuer,
                audience:          config.Value.Audience,
                claims:            userclaims,
                expires:           DateTime.Now.AddDays(1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<UserRole>    FindUserRole(int userId) => (await appDbContext.UserRoles.FirstOrDefaultAsync(_ => _.UserId == userId))!;
        private async Task<SystemRole>  FindRoleName(int roleId) => (await appDbContext.SystemRoles.FirstOrDefaultAsync(_ => _.Id == roleId))!;

        private static string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        private async Task<ApplicationUser> FindUserByEmail(string email) =>
            // Direct equality comparison — EF translates to WHERE Email = @p0 which
            // uses IX_ApplicationUsers_Email. SQL Server's default CI_AS collation
            // makes this case-insensitive, so ToLower() is unnecessary and harmful
            // (it prevents index usage by forcing a function-based scan).
            (await appDbContext.ApplicationUsers
                .FirstOrDefaultAsync(u => u.Email == email))!;

        private async Task<T> AddToDatabase<T>(T model)
        {
            var result = appDbContext.Add(model!);
            await appDbContext.SaveChangesAsync();
            return (T)result.Entity;
        }

        public async Task<LoginResponse> RefreshTokenAsync(RefreshToken token)
        {
            if (token is null)
            {
                logger.LogWarning("Token refresh attempted with an empty model");
                return new LoginResponse(false, "Model is empty");
            }

            logger.LogDebug(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Result: {Result}",
                "TokenRefresh", "Refresh", "ApplicationUser", "Attempt");

            var findToken = await appDbContext.RefreshTokenInfos
                .FirstOrDefaultAsync(_ => _.Token!.Equals(token.Refreshtoken));

            if (findToken is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | Result: {Result}",
                    "TokenRefresh", "Refresh", "ApplicationUser", "Failure:TokenNotFound");
                return new LoginResponse(false, "Refresh token is required");
            }

            var user = await appDbContext.ApplicationUsers
                .FirstOrDefaultAsync(_ => _.Id == findToken.UserId);

            if (user is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Result: {Result}",
                    "TokenRefresh", "Refresh", "ApplicationUser", findToken.UserId, "Failure:UserNotFound");
                return new LoginResponse(false, "Refresh token could not be generated because user not found");
            }

            var userRole  = await FindUserRole(user.Id);
            var roleName  = await FindRoleName(userRole.RoleId);
            string jwtToken    = GenerateToken(user, roleName.Name!);
            string refreshToken = GenerateRefreshToken();

            var updateRefreshToken = await appDbContext.RefreshTokenInfos
                .FirstOrDefaultAsync(_ => _.UserId == user.Id);

            if (updateRefreshToken is null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Result: {Result}",
                    "TokenRefresh", "Refresh", "ApplicationUser", user.Id, "Failure:NoTokenRecord");
                return new LoginResponse(false, "Refresh token could not be generated because user has not signed in");
            }

            updateRefreshToken.Token = refreshToken;
            await appDbContext.SaveChangesAsync();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Email: {Email} | Result: {Result}",
                "TokenRefresh", "Refresh", "ApplicationUser", user.Id, user.Email, "Success");

            return new LoginResponse(true, "Token refreshed successfully", jwtToken, refreshToken);
        }


        public async Task<List<ManageUser>> GetUsers()
        {
            var allUsers     = await GetApplicationUsers();
            var allUserRoles = await UserRoles();
            var allRoles     = await SystemRoles();

            if (allUsers.Count == 0 || allRoles.Count == 0) return null!;

            var users = new List<ManageUser>();
            foreach (var user in allUsers)
            {
                var userRole = allUserRoles.FirstOrDefault(u => u.UserId == user.Id);
                if (userRole is null) continue; // user exists but has no role assigned

                var roleName = allRoles.FirstOrDefault(u => u.Id == userRole.RoleId);
                if (roleName is null) continue; // role record missing from SystemRoles

                users.Add(new ManageUser() { UserId = user.Id, Name = user.Fullname!, Email = user.Email!, Role = roleName.Name! });
            }
            return users;
        }

        public async Task<GeneralResponse> UpdateUser(ManageUser model)
        {
            if (model == null)
            {
                logger.LogWarning("UpdateUser attempted with an empty model");
                return new GeneralResponse(false, "Model is empty");
            }

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | NewRole: {Role} | Result: {Result}",
                "UserUpdate", "Update", "ApplicationUser", model.UserId, model.Role, "Attempt");

            var user = await appDbContext.ApplicationUsers
                .FirstOrDefaultAsync(u => u.Id == model.UserId);

            if (user == null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Result: {Result}",
                    "UserUpdate", "Update", "ApplicationUser", model.UserId, "Failure:UserNotFound");
                return new GeneralResponse(false, "User not found");
            }

            user.Fullname = model.Name;
            user.Email    = model.Email;

            var role = await appDbContext.SystemRoles
                .FirstOrDefaultAsync(r => r.Name != null && model.Role != null && r.Name.ToLower() == model.Role.ToLower());

            if (role == null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Role: {Role} | Result: {Result}",
                    "UserUpdate", "Update", "ApplicationUser", model.UserId, model.Role, "Failure:RoleNotFound");
                return new GeneralResponse(false, "Role not found");
            }

            var userRole = await appDbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == user.Id);

            if (userRole != null)
            {
                userRole.RoleId = role.Id;
            }

            await appDbContext.SaveChangesAsync();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Email: {Email} | Role: {Role} | Result: {Result}",
                "UserUpdate", "Update", "ApplicationUser", user.Id, user.Email, role.Name, "Success");

            return new GeneralResponse(true, "User updated successfully");
        }

        public async Task<List<SystemRole>> GetRoles() => await SystemRoles();

        private async Task<List<SystemRole>>      SystemRoles()        => await appDbContext.SystemRoles.AsNoTracking().ToListAsync();
        private async Task<List<UserRole>>         UserRoles()          => await appDbContext.UserRoles.AsNoTracking().ToListAsync();
        private async Task<List<ApplicationUser>> GetApplicationUsers() => await appDbContext.ApplicationUsers.AsNoTracking().ToListAsync();

        public async Task<GeneralResponse> DeleteUser(int id)
        {
            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Result: {Result}",
                "UserDelete", "Delete", "ApplicationUser", id, "Attempt");

            var user = await appDbContext.ApplicationUsers
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                logger.LogWarning(
                    "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Result: {Result}",
                    "UserDelete", "Delete", "ApplicationUser", id, "Failure:UserNotFound");
                return new GeneralResponse(false, "User not found");
            }

            appDbContext.ApplicationUsers.Remove(user);
            await appDbContext.SaveChangesAsync();

            logger.LogInformation(
                "Audit — EventName: {EventName} | Action: {Action} | Entity: {Entity} | UserId: {UserId} | Email: {Email} | Result: {Result}",
                "UserDelete", "Delete", "ApplicationUser", id, user.Email, "Success");

            return new GeneralResponse(true, "User successfully deleted");
        }
    }
}
