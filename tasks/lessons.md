# Engineering Lessons — EMS Solution

## L-001 · Blazor [Parameter] ownership on dialog components

**Problem pattern:**
A dialog component declares `[Parameter] public T Model { get; set; }` AND also creates
a fresh instance of `Model` inside an `OpenDialog()` method. This creates two competing
sources of truth:
1. The parent pushes `Model` via parameter on every render cycle.
2. `OpenDialog()` creates a new instance and points `EditContext` at it.
After `OpenDialog()`, Blazor's `SetParametersAsync` fires on the next render and overwrites
`Model` with whatever the parent passed — but `editContext` is still tracking the OLD
(freshly created) instance. Validation then runs against an empty object even though
the form has data filled in.

**Root cause seen:**
- `DoctorDialog` had `[Parameter] public Doctor Doctor` + `OpenDialog()` creating a fresh Doctor
- `OvertimeDialog` and `VacationDialog` had `[Parameter] public T ItemModel` removed but
  parent `EmployeePage` still passed `ItemModel="..."` → `InvalidOperationException` at render

**Rule: Dialogs own their own model. Never both.**
When a dialog creates/manages its own model internally (via `OpenDialog()` / `OpenDialogForEdit()`),
that field MUST NOT be a `[Parameter]`. Remove `[Parameter]`, remove the parent binding,
use `@ref` to call `OpenDialog(id)` or `OpenDialogForEdit(item)` directly.

**Checklist when adding a dialog:**
- [ ] Is the model owned by the dialog (created in `OpenDialog`)? → not a `[Parameter]`
- [ ] Is the model owned by the parent (passed in and never recreated inside)? → `[Parameter]` is OK
- [ ] `editContext` must always be created from the SAME instance it will track through the form's lifetime
- [ ] After removing `[Parameter]`, grep ALL parents that use that dialog and remove stale bindings

---

## L-002 · Audit all component consumers when removing [Parameter]

**Problem pattern:**
When `[Parameter]` is removed from a dialog property, only the most obvious parent
pages (the dedicated page component, e.g. `VacationPage`) were updated. A second parent
(`EmployeePage`, which hosts all dialogs from the context menu) was missed. The build
succeeded because Blazor does NOT emit a compile error for unknown component attributes
in all cases — it throws at **runtime** instead (`InvalidOperationException`).

**Rule:**
After removing a `[Parameter]` attribute, always run:
```
grep -r "ComponentName" --include="*.razor" .
```
to find every file that uses that component, then verify each one no longer passes
the removed attribute.

---

## L-003 · Syncfusion ValueChange IsInteracted > custom suppress flag

**Problem pattern:**
An attempt was made to add a `_suppressCascade` bool flag to prevent cascade dropdown
clearing when pre-populating values for Edit. This is manual bookkeeping that's easy
to forget or mis-sequence.

**Correct approach:**
Syncfusion `ChangeEventArgs<T, TItem>` carries an `IsInteracted` property.
When `false`, the change was programmatic (value set by code / binding update).
When `true`, the user physically changed the dropdown.

```csharp
public async Task OnCountryValueChange(ChangeEventArgs<int?, Country> args)
{
    if (!args.IsInteracted) return;   // ← suppress programmatic updates
    // cascade logic only runs for real user input
}
```

This is zero-maintenance, idiomatic, and impossible to forget.

---

## L-004 · Merge conflict resolution — always verify both parents of shared components

After resolving merge conflicts and restoring dialog files, verify:
1. The dedicated page (e.g. `VacationPage`) — usually the obvious one to check
2. `EmployeePage` — hosts ALL dialogs via context menu; easy to miss
3. Any other parent that uses the dialog via `@ref`

Build passing does NOT guarantee runtime correctness for Blazor parameter mismatches
in all cases. A quick grep for the dialog component name across all `.razor` files
is the only reliable check.
