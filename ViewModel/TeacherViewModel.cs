using Entities.Models;

namespace myWebApp.ViewModels.NewFolder
{
    public class TeacherViewModel
    {
        public string? EmployeeName { get; set; }

        public int? EmployeeId { get; set; }

        public string? Status { get; set; }
        public Utilize Utilize { get; set; }
        public int? UtilizeID { get; set; }
        public LeaveStatus LeaveStatus { get; set; }
    }
}
