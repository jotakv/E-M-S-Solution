# Bugs Found, Fixed, and Notes

This document records every bug found in the EMS project, why each occurred, how it was fixed, and any additional observations.

---

## Bug 1 — Employee Context Menu Dialog Too Narrow
**File:** `Client/Pages/OtherPages/ContextMenu.razor`
**Branch:** `fix/employee-menu-admin-role`

### What was wrong
The Syncfusion `<SfDialog>` for the employee context menu had `Width="100px"`. The dialog shows 7 action items (View, Edit, Delete, Add Vacation, Add Overtime, Add Health, Add Sanction) plus icons and a header. 100 px is far too narrow — all content was visually crammed and the dialog was broken.

### Root cause
Likely a typo during initial development; `100px` was probably meant to be `200px` or `220px`.

### Fix
Changed `Width="100px"` → `Width="220px"` which gives the dialog enough room to display all menu items properly.

---

## Bug 2 — Redundant `DialogEvents OnOpen` in ContextMenu
**File:** `Client/Pages/OtherPages/ContextMenu.razor`
**Branch:** `fix/employee-menu-admin-role`

### What was wrong
```razor
<DialogEvents OnOpen="OpenContextMenu"></DialogEvents>
```
The `OnOpen` event fires **after** Blazor has already set `IsVisible = true` and started opening the dialog. The handler `OpenContextMenu()` then sets `IsVisible = true` again and calls `StateHasChanged()` — a redundant double render during the dialog's own open cycle. In certain Syncfusion versions this can cause visual glitches or unexpected re-renders.

### Root cause
The `OpenContextMenu()` method (called by the parent via `@ref`) correctly sets `IsVisible = true`. The `OnOpen` binding was added as a secondary trigger but is entirely unnecessary.

### Fix
Removed the `<DialogEvents OnOpen="OpenContextMenu">` element entirely.

---

## Bug 3 — Unused `SfDialog? OpenDialog` Field in ContextMenu
**File:** `Client/Pages/OtherPages/ContextMenu.razor`
**Branch:** `fix/employee-menu-admin-role`

### What was wrong
```csharp
SfDialog? OpenDialog;
```
This field was declared but never assigned or used anywhere in the component.

### Root cause
Leftover from a previous refactoring attempt.

### Fix
Removed the unused field.

---

## Bug 4 — Admin Menu Visible to All Users (RBAC Broken)
**File:** `Client/Layout/NavMenu.razor`
**Branch:** `fix/employee-menu-admin-role`

### What was wrong
The `<AuthorizeView Roles="Admin">` wrapper that should restrict the Administration menu to admin-only users was **commented out**. This meant every authenticated user (regardless of role) could see and access the Administration → Users menu.

### Root cause
The developer commented out the `AuthorizeView Roles="Admin"` block during debugging or testing (to access the Users page quickly without worrying about roles) and **never uncommented it** before committing. The commented block was:

```razor
@* <AuthorizeView Roles="Admin" Context="adminCtx">
    <Authorized> *@
    ... Administration menu ...
@*   </Authorized>
</AuthorizeView> *@
```

### Why the role check itself works
The JWT token embeds the role as `"http://schemas.microsoft.com/ws/2008/06/identity/claims/role"`. Blazor's `AuthorizeView Roles="Admin"` requires the claim to be present as `ClaimTypes.Role` in the `ClaimsPrincipal`. The `CustomAuthenticationStateProvider.cs` already had the correct fix to re-map JWT role claims to `ClaimTypes.Role`:

```csharp
var roleClaims = claims
    .Where(c => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
    .Select(c => new Claim(ClaimTypes.Role, c.Value))
    .ToList();
claims.AddRange(roleClaims);
```

So the token and provider were correct — the only thing missing was the `AuthorizeView` wrapper being uncommented.

### Fix
Uncommented and corrected the `<AuthorizeView Roles="Admin" Context="adminCtx">` wrapper. The `Context="adminCtx"` attribute is required because this is a **nested** `AuthorizeView` (inside the outer `<AuthorizeView>` that guards the whole nav). Without a unique `Context`, Blazor raises compile error `RZ9999: ambiguous context parameter name`.

### Verification
- User with role `"User"` → JWT payload: `"role":"User"` → mapped to `ClaimTypes.Role="User"` → Admin menu **hidden** ✓
- User with role `"Admin"` → JWT payload: `"role":"Admin"` → mapped to `ClaimTypes.Role="Admin"` → Admin menu **visible** ✓

---

## Bug 5 — Duplicate `app.UseHttpsRedirection()` in Server Pipeline
**File:** `Server/Program.cs`
**Branch:** `fix/employee-menu-admin-role`

### What was wrong
```csharp
var app = builder.Build();

// Important: CORS must come **before** Authentication/Authorization
app.UseHttpsRedirection();   // ← DUPLICATE (wrong position)

if (app.Environment.IsDevelopment()) { ... }

app.UseHttpsRedirection();   // ← correct position
app.UseCors("AllowBlazorWasm");
```

`UseHttpsRedirection()` was registered **twice** in the middleware pipeline. The first call appeared before the dev tools setup with an incorrect comment about CORS ordering. Calling it twice does not cause a crash, but it adds an unnecessary middleware step on every request and the misleading comment about CORS could confuse future developers.

### Root cause
A copy-paste error or accidental duplication during development.

### Fix
Removed the first (incorrectly placed) `UseHttpsRedirection()` call.

---

## Bug 6 — `TableBanner` Missing `@implements IDisposable`
**File:** `Client/Pages/OtherPages/TableBanner.razor`
**Branch:** `feature/realtime-banner-updates`

### What was wrong
The component declared a `public void Dispose()` method that unsubscribed from `allState.Action`:

```csharp
public void Dispose() => allState.Action -= StateHasChanged;
```

But the file was **missing** `@implements IDisposable` at the top. Without this directive, Blazor's component lifecycle does **not** call `Dispose()` when the component is removed from the render tree. This caused:
- `allState.Action` to accumulate dangling subscriptions across navigation
- `StateHasChanged()` calls on a disposed component (can silently throw `ObjectDisposedException` in some Blazor versions)
- Potential memory leak

### Root cause
The `Dispose()` method was added without the accompanying directive, likely because the developer was not aware that the directive is required for Blazor to call it.

### Fix
Added `@implements IDisposable` at the top of `TableBanner.razor`.

---

## Feature — Real-Time TableBanner Count Updates
**Files:** `Client/ApplicationStates/AllState.cs`, `Client/Pages/OtherPages/TableBanner.razor`, `Client/Pages/ContentPages/EmployeePages/EmployeePage.razor`
**Branch:** `feature/realtime-banner-updates`

### Problem
When a user added Vacation, Overtime, Health (Doctor), or Sanction via the employee context menu dialog, the counts in the `TableBanner` (dashboard header cards) did **not update** until the user manually refreshed the page. The `TableBanner` only loaded data in `OnInitializedAsync`.

### Root cause
`AllState.Action` was only used for navigation events (changing which page is shown). After a successful save in `EmployeePage`, only a dialog was closed — no signal was sent to `TableBanner` to reload its data.

### Solution
1. Added `DataRefreshAction` property and `NotifyDataRefresh()` helper to `AllState`:
   ```csharp
   public Action? DataRefreshAction { get; set; }
   public void NotifyDataRefresh() => DataRefreshAction?.Invoke();
   ```
2. `TableBanner` subscribes to `DataRefreshAction` via a named `HandleDataRefresh()` method that calls `LoadDefaults()` + `StateHasChanged()`, and properly unsubscribes in `Dispose()`.
3. `EmployeePage` calls `allState.NotifyDataRefresh()` after each successful save of Health, Overtime, Sanction, and Vacation.

---

## Feature — Serilog + Seq Structured Logging
**Files:** `Server/Server.csproj`, `Server/Program.cs`, `Server/appsettings.json`
**Branch:** `feature/serilog-seq`

### Packages added
| Package | Version | Purpose |
|---|---|---|
| `Serilog.AspNetCore` | 8.0.3 | Core Serilog integration for ASP.NET Core |
| `Serilog.Sinks.Seq` | 8.0.0 | Ships logs to a Seq server |
| `Serilog.Sinks.Console` | 6.0.0 | Structured console output (dev visibility) |

### Configuration
- Bootstrap logger captures fatal startup errors before the DI container is built
- `UseSerilog()` replaces the default .NET logging pipeline
- `UseSerilogRequestLogging()` adds structured HTTP access logs (replaces noisy IIS-style request logs)
- Seq URL defaults to `http://localhost:5341`, configurable via `appsettings.json → Serilog:SeqUrl`
- Microsoft and EF Core namespaces suppressed at `Warning` to reduce noise

### To use Seq locally
```bash
docker run --name seq -d --restart unless-stopped -e ACCEPT_EULA=Y -p 5341:5341 -p 5342:80 datalust/seq:latest
# Open Seq UI at http://localhost:5342
```

---

## Additional Notes

### Pre-existing Warnings (not introduced by these changes)
The solution has ~60 nullable reference warnings (CS8601, CS8602, CS8603, CS8618) spread across `BaseLibrary`, `ServerLibrary`, and `Client`. These are pre-existing and did not affect functionality. They are noted here for awareness and can be addressed in a future clean-up pass.

### Architecture
The project follows a clean layered architecture:
- `BaseLibrary` — shared DTOs and entities (no dependencies)
- `ServerLibrary` — data access, repositories, EF Core (depends on BaseLibrary)
- `Server` — ASP.NET Core Web API (depends on ServerLibrary)
- `ClientLibrary` — client-side services and auth state (depends on BaseLibrary)
- `Client` — Blazor WebAssembly frontend (depends on ClientLibrary)

### Branch Strategy Used
| Branch | Purpose |
|---|---|
| `fix/employee-menu-admin-role` | Bugs 1–5: ContextMenu width, OnOpen handler, unused field, admin RBAC, duplicate middleware |
| `feature/realtime-banner-updates` | Bug 6 (missing IDisposable) + real-time banner counts |
| `feature/serilog-seq` | Serilog + Seq structured logging |
