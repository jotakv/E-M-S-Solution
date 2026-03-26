# Post-Merge Runtime Fix Plan

## Root Cause Audit

### RC-1 · CRITICAL · UI Error State (Blazor InvalidOperationException)
`EmployeePage` passes `ItemModel="Overtime"` and `ItemModel="Vacation"` as Blazor
component parameters to `OvertimeDialog` and `VacationDialog`. Those dialogs had
`[Parameter]` removed from `ItemModel` in this session. Blazor throws at render:
> InvalidOperationException: Object of type 'OvertimeDialog' does not have a property
> matching the name 'ItemModel'.

This is what triggers the red Blazor error UI visible to the user.

### RC-2 · MEDIUM · DoctorDialog editContext mismatch (Add Health broken)
`DoctorDialog` has `[Parameter] public Doctor Doctor`. `OpenDialog()` creates a fresh
`Doctor` instance and points `editContext` at it. Then Blazor's parameter binding pushes
the parent's `Doctor` object DOWN, overwriting the dialog's field — but `editContext` still
tracks the OLD instance. On Save, `editContext.Validate()` validates the stale empty
Doctor → "Required" errors fire even though the form has data. Add Health from EmployeePage
always fails validation silently.

### RC-3 · INFO · RabbitMQ [WRN] in VS console
When Docker isn't running, `RabbitMqEventBus` and `EmsAuditConsumer` log Warning-level
messages. These are NOT errors — they're intentional graceful degradation. No code fix
needed; just start Docker (`docker compose up -d`) for local dev.

---

## Tasks

- [x] Write plan to tasks/todo.md
- [x] Fix RC-1: Remove stale `ItemModel` parameter bindings in EmployeePage
- [x] Fix RC-1: Clean up now-redundant CloseDialog() calls in SaveOvertimeEvent / SaveVacationEvent
- [x] Fix RC-2: Remove `[Parameter]` from Doctor in DoctorDialog; add OpenDialogForEdit()
- [x] Fix RC-2: Remove Doctor="Doctor" binding from EmployeePage and DoctorPage
- [x] Fix RC-2: Update DoctorPage.EditClicked to use OpenDialogForEdit(item)
- [x] Build verification — 0 errors, 0 warnings ✅
- [x] Document lessons in tasks/lessons.md

---

## Review Notes
(to be filled after implementation)
