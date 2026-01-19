namespace Client.ApplicationStates
{
    public class DepartmentStates
    {
        public Action? GeneralDepartmentAction { get; set; }
        public bool ShowGeneralDepartment { get; set; }
        public void GeneralDepartmentClicked()
        {
            ResetAllDepartments();
            ShowGeneralDepartment = true;
            GeneralDepartmentAction?.Invoke();
        }

        public void ResetAllDepartments()
        {
            ShowGeneralDepartment = false;
        }
    }
}