using Entities.Models;

namespace myWebApp.ViewModels.NewFolder
{
    public class OpeningViewModel
    {
        public int Id { get; set; }
       
        public string GroupName { get; set; }
       
        public List<LeaveType> LeaveTypes { get; set; }
    }
}
