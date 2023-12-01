namespace myWebApp.ViewModels.NewFolder
{
    public class UtilizeViewModel
    {
        public int Id { get; set; } 
        public int EmployeeID { get; set; }
        public string EmployeeName { get; set; }

        public string GroupName { get; set; }

        public string TypeName { get; set; }

        public int TypeID { get; set; }

        public int GroupID { get; set; }

        public string? Reason { get; set; }

        public int Days { get; set; }

        public int SubstituteID { get; set; }
        public string Substitute { get; set; }
        public DateTime? Year { get; set; }



    }
}
