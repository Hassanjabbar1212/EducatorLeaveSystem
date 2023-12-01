using Microsoft.AspNetCore.Mvc;
using Infrastructure.Data;
using Entities.Models;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using myWebApp.ViewModels.Director;
using myWebApp.ViewModels.BookAllocation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using myWebApp.ViewModels.Auth;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Web;
using myWebApp.ViewModels.NewFolder;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using myWebApp.ViewModels.HumanResource;
using Microsoft.IdentityModel.Tokens;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using NuGet.Protocol.Plugins;

namespace myWebApp.Controllers
{

    public class LeaveController : Controller
    {
        private readonly SchoolDbContext _db;

        public LeaveController(SchoolDbContext db)
        {
            _db = db;
        }
        [HttpGet]
        public IActionResult GetEmployeeData(int selectedEmployeeId)
        {
            try
            {

                var employeeData = _db.openings
          .Where(o => o.EmpID == selectedEmployeeId)
          .GroupJoin(
              _db.balances,
              o => o.Id,
              b => b.OpeningID,
              (o, balanceGroup) => new { Opening = o, Balances = balanceGroup }
          )
          .SelectMany(
              ob => ob.Balances.DefaultIfEmpty(),
              (ob, balance) => new
              {
                  EmployeeID = ob.Opening.EmpID,
                  GroupID = ob.Opening.GroupID,
                  TypeID = balance.TypeID,
                  OpeningDays = balance.Days
              }
          )
          .GroupJoin(
              _db.LeaveStatuses
                  .Where(ls => ls.TeacherID == selectedEmployeeId && ls.Status == true),
              ob => ob.EmployeeID,
              ls => ls.TeacherID,
              (ob, leaveStatusGroup) => new { ob, LeaveStatuses = leaveStatusGroup }
          )
          .SelectMany(
              obls => obls.LeaveStatuses.DefaultIfEmpty(),
              (obls, leaveStatus) => new
              {
                  obls.ob.EmployeeID,
                  obls.ob.GroupID,
                  EmployeeName = _db.Employees.FirstOrDefault(e => e.EmployeeId == obls.ob.EmployeeID).FName,
                  GroupName = _db.LeaveGroups.FirstOrDefault(g => g.Id == obls.ob.GroupID).GroupName,
                  TypeName = _db.LeaveTypes.FirstOrDefault(t => t.Id == obls.ob.TypeID).TypeName,
                  OpeningDays = obls.ob.OpeningDays,
                  UtilizedDays = leaveStatus != null && leaveStatus.TypeID == obls.ob.TypeID ? leaveStatus.Days : 0
              }
          )
          .GroupBy(result => new { result.EmployeeID, result.EmployeeName, result.GroupID, result.GroupName, result.TypeName })
          .Select(group => new
          {
              EmployeeID = group.Key.EmployeeID,
              GroupID = group.Key.GroupID,
              EmployeeName = group.Key.EmployeeName,
              GroupName = group.Key.GroupName,
              TypeName = group.Key.TypeName,
              OpeningDays = group.FirstOrDefault().OpeningDays,
              UtilizedDays = group.Sum(g => g.UtilizedDays)
          })
          .ToList();


                if (employeeData.Any())
                {
                    return Json(new { success = true, EmployeeData = employeeData });
                }
                else
                {
                    // No data found, consider it as an error
                    return Json(new { success = false, message = "No employee data found." });
                }

            }
            catch (Exception ex)
            {
                return Json(new { success = false });
            }
        }

        [HttpGet]
        public IActionResult DAView()
        {
            ViewBag.EmployeeNames = _db.Employees.Select(e => new SelectListItem { Value = e.EmployeeId.ToString(), Text = $"{e.FName} {e.LName}" }).ToList();
            return View();
        }
        [HttpGet]
        public IActionResult TeacherView()
        {
            var claimsPrincipal = HttpContext.User;

            // Retrieve the value of the TeacherID claim
            var teacherIdClaim = claimsPrincipal.FindFirst(ClaimTypes.Sid);

            if (teacherIdClaim != null && int.TryParse(teacherIdClaim.Value, out int teacherId))
            {
                // Use the logged-in user's ID instead of teacherId

                var leaveStatusData = _db.LeaveStatuses
                    .Where(ls => ls.TeacherID == teacherId)
                    .Select(ls => new TeacherViewModel
                    {
                        EmployeeName = _db.Employees.FirstOrDefault(e => e.EmployeeId == ls.EmpID).FName,
                        Status = ls.Status.HasValue ? (ls.Status.Value ? "Approved" : "Rejected") : "Pending",
                        UtilizeID = ls.UtilizeID
                    })
                    .ToList();

                // Now leaveStatusData contains the Status and Employee Name based on the logged-in user's ID

                return View(leaveStatusData);
            }




            // Handle the case where the TeacherID claim is not present or parsing fails
            // You can customize this part based on your requirements
            return RedirectToAction("Error");



            // Handle the case where employee ID cannot be parsed or claims are not available
            // You might want to redirect to an error page or handle this case appropriately.
        }

        public async Task<IActionResult> OpeningBalance()
        {
            var viewModelList = await _db.Utilizes
        .Join(_db.Employees,
              u => u.EmpID,
              e => e.EmployeeId,
              (u, e) => new { Utilize = u, Employee = e })
        .Join(_db.LeaveGroups,
              ue => ue.Utilize.GroupID,
              lg => lg.Id,
              (ue, lg) => new { ue.Utilize, ue.Employee, LeaveGroup = lg })
        .Join(_db.LeaveTypes,
              uelg => uelg.Utilize.TypeID,
              lt => lt.Id,
              (uelg, lt) => new UtilizeViewModel
              {
                  EmployeeName = uelg.Employee.FName,
                  Substitute = _db.Employees
                                     .Where(emp => emp.EmployeeId == uelg.Utilize.SubstituteID)
                                     .Select(emp => emp.FName)
                                     .FirstOrDefault(),
                  GroupName = uelg.LeaveGroup.GroupName,
                  TypeName = lt.TypeName,
                  Reason = uelg.Utilize.Reason,
                  Days = uelg.Utilize.Days,
                  Year = uelg.Utilize.Year,
                  Id = uelg.Utilize.Id,
                  EmployeeID = uelg.Utilize.EmpID,
                  TypeID = uelg.Utilize.TypeID

              })
         .Where(u => !_db.LeaveStatuses.Any(ls => ls.UtilizeID == u.Id))
        .ToListAsync();

            return View(viewModelList);

        }


        [HttpPost]
        public IActionResult ProcessApproval(int utilizeId, bool isApproved, int EmployeeID, int TypeID, int Days)
        {
            try
            {
                var claimsPrincipal = HttpContext.User;

                // Retrieve the value of the Sid claim
                var employeeIdClaim = claimsPrincipal.FindFirst(ClaimTypes.Sid);

                if (employeeIdClaim != null)
                {
                    string employeeIdString = employeeIdClaim.Value;

                    if (int.TryParse(employeeIdString, out int employeeId))
                    {
                        if (ModelState.IsValid)
                        {
                            // Assuming you have a Utilize record with the given utilizeId
                            var utilize = _db.Utilizes.Find(utilizeId);

                            if (utilize != null)
                            {
                                // Create a new LeaveStatus record
                                var leaveStatus = new LeaveStatus
                                {
                                    EmpID = employeeId,
                                    UtilizeID = utilizeId,
                                    Status = isApproved,
                                    TeacherID = EmployeeID,
                                    TypeID = TypeID,
                                    Days = Days,
                                };

                                // Add the LeaveStatus record to the database
                                _db.LeaveStatuses.Add(leaveStatus);

                                // Save changes to the database
                                _db.SaveChanges();

                                return Json(new { success = true, message = "SuccessFully Approved/Rejected" });
                            }
                            else
                            {
                                return Json(new { success = false, message = "Utilize record not found." });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception or handle it appropriately
                return Json(new { success = false, message = ex.Message });
            }

            return Json(new { success = false, message = "Invalid data or processing error." });
        }

        [HttpGet]
        public IActionResult Utilize()
        {
            ViewBag.GroupName = new SelectList(_db.LeaveGroups, "Id", "GroupName");
            ViewBag.TypeName = new SelectList(_db.LeaveTypes, "Id", "TypeName");
            ViewBag.EmployeeName = new SelectList(_db.Employees, "EmployeeId", "FName");
            // Get the logged-in user's ID
            var claimsPrincipal = HttpContext.User;
            var employeeIdClaim = claimsPrincipal.FindFirst(ClaimTypes.Sid);

            if (employeeIdClaim != null && int.TryParse(employeeIdClaim.Value, out int loggedInEmployeeId))
            {
                // Use the logged-in user's ID instead of selectedEmployeeId

                var employeeData = _db.openings
                    .Where(o => o.EmpID == loggedInEmployeeId) // Use the logged-in user's ID
                    .GroupJoin(
                        _db.balances,
                        o => o.Id,
                        b => b.OpeningID,
                        (o, balanceGroup) => new { Opening = o, Balances = balanceGroup }
                    )
                    .SelectMany(
                        ob => ob.Balances.DefaultIfEmpty(),
                        (ob, balance) => new
                        {
                            EmployeeID = ob.Opening.EmpID,
                            GroupID = ob.Opening.GroupID,
                            TypeID = balance.TypeID,
                            OpeningDays = balance.Days
                        }
                    )
                    .GroupJoin(
                        _db.LeaveStatuses
                            .Where(ls => ls.TeacherID == loggedInEmployeeId && ls.Status == true),
                        ob => ob.EmployeeID,
                        ls => ls.TeacherID,
                        (ob, leaveStatusGroup) => new { ob, LeaveStatuses = leaveStatusGroup }
                    )
                    .SelectMany(
                        obls => obls.LeaveStatuses.DefaultIfEmpty(),
                        (obls, leaveStatus) => new
                        {
                            obls.ob.EmployeeID,
                            obls.ob.GroupID,
                            EmployeeName = _db.Employees.FirstOrDefault(e => e.EmployeeId == obls.ob.EmployeeID).FName,
                            GroupName = _db.LeaveGroups.FirstOrDefault(g => g.Id == obls.ob.GroupID).GroupName,
                            TypeName = _db.LeaveTypes.FirstOrDefault(t => t.Id == obls.ob.TypeID).TypeName,
                            OpeningDays = obls.ob.OpeningDays,
                            UtilizedDays = leaveStatus != null && leaveStatus.TypeID == obls.ob.TypeID ? leaveStatus.Days : 0
                        }
                    )
                    .GroupBy(result => new { result.EmployeeID, result.EmployeeName, result.GroupID, result.GroupName, result.TypeName })
                    .Select(group => new
                    {
                        EmployeeID = group.Key.EmployeeID,
                        GroupID = group.Key.GroupID,
                        EmployeeName = group.Key.EmployeeName,
                        GroupName = group.Key.GroupName,
                        TypeName = group.Key.TypeName,
                        OpeningDays = group.FirstOrDefault().OpeningDays,
                        UtilizedDays = group.Sum(g => g.UtilizedDays)
                    })
                    .ToList();


                var remainingDays = employeeData.Select(e => new { e.EmployeeID, RemainingDays = e.OpeningDays - e.UtilizedDays });

                if (remainingDays.Any(rd => rd.RemainingDays > 0))
                {
                    // Proceed with the logic to display the view
                    return View();
                }
                else
                {
                    // Show an error message indicating no remaining days
                    TempData["ErrorMessage"] = "No remaining days available.";
                    // Redirect to an error view or display an error message
                    // ...
                }
            }
            var successMessage = TempData["SuccessMessage"] as string;

            // Pass the success message to the view, e.g., using ViewBag
            ViewBag.SuccessMessage = successMessage;


            return RedirectToAction("Not Found");


        }

        [HttpPost]
        public IActionResult Utilize(Utilize utilize)
        {
            var claimsPrincipal = HttpContext.User;

            // Retrieve the value of the Sid claim
            var employeeIdClaim = claimsPrincipal.FindFirst(ClaimTypes.Sid);

            if (employeeIdClaim != null)
            {
                string employeeIdString = employeeIdClaim.Value;

                if (int.TryParse(employeeIdString, out int employeeId))
                {
                    if (ModelState.IsValid)
                    {
                        // Set the EmployeeID of the utilize object before adding it to the database
                        utilize.EmpID = employeeId;
                        utilize.Year = DateTime.Now;
                        _db.Add(utilize);
                        _db.SaveChanges();

                        TempData["SuccessMessage"] = "Submitted successfully.";
                        return RedirectToAction("Utilize");
                    }
                }
            }

            // If the Sid claim is not found or cannot be converted to an integer, return to the view
            return View(utilize);
        }





        [HttpGet("api/leavegroup/{groupId}/leavetypes")]
        public IActionResult GetLeaveTypesByGroup(int groupId)
        {
            var leaveTypes = _db.LeaveTypes
                .Where(leaveType => leaveType.LeaveGroupId == groupId)
                .Select(leaveType => new
                {
                    leaveTypeId = leaveType.Id,
                    leaveTypeName = leaveType.TypeName
                })
                .ToList();

            return Json(leaveTypes);
        }

        [HttpPost]
        public IActionResult SubmitOpening([FromBody] List<LeaveOperation> requestData)
        {
            try
            {
                if (requestData != null && requestData.Any())
                {
                    // Save data to the Opening table for the first item
                    var openingData = new Opening
                    {
                        EmpID = requestData.First().EmpID,
                        GroupID = requestData.First().GroupID
                        // Add other properties as needed
                    };

                    _db.openings.Add(openingData);
                    _db.SaveChanges();

                    // Now, openingData.Id will have the generated ID (assuming it's an identity column)

                    // Save data to the Balance table for each item
                    foreach (var leaveOperation in requestData)
                    {
                        var balanceData = new Balance
                        {
                            TypeID = leaveOperation.leaveTypeId,
                            Days = leaveOperation.daysValue,
                            OpeningID = openingData.Id // Use the generated ID from the Opening table
                        };

                        _db.balances.Add(balanceData);
                    }

                    _db.SaveChanges();

                    return Ok(new { Message = "Data received and stored successfully." });
                }

                return BadRequest(new { Message = "Invalid data received." });
            }
            catch (Exception ex)
            {
                // Log the exception for debugging purposes
                Console.Error.WriteLine($"Exception: {ex}");

                // Handle any exceptions and return an error response
                return BadRequest(new { Message = "An error occurred while processing the data." });
            }
        }


        public async Task<IActionResult> Opening()
        {
            ViewBag.GroupName = new SelectList(_db.LeaveGroups, "Id", "GroupName");

            ViewBag.TypeName = new SelectList(_db.LeaveTypes, "Id", "TypeName");

            ViewBag.EmplyeeName = new SelectList(_db.Employees, "EmployeeId", "FName");

            return View();
        }

        public IActionResult Index()
        {
            var leaveTypesWithGroups = _db.LeaveGroups
                .Select(group => new OpeningViewModel
                {
                    GroupName = group.GroupName,
                    LeaveTypes = _db.LeaveTypes
                        .Where(leaveType => leaveType.LeaveGroupId == group.Id)
                        .Select(leaveType => new LeaveType
                        {
                            Id = leaveType.Id,
                            TypeName = leaveType.TypeName,
                            Days = leaveType.Days
                        })
                        .ToList()
                })
                .ToList();

            return View(leaveTypesWithGroups);
        }

        public async Task<IActionResult> LeaveGroup()
        {
            ViewBag.GroupName = new SelectList(_db.LeaveGroups, "Id", "GroupName"); //Leave Group

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitGroup(string groupName)
        {
            if (!string.IsNullOrEmpty(groupName))
            {
                var leaveGroup = new LeaveGroup { GroupName = groupName };
                _db.LeaveGroups.Add(leaveGroup);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("LeaveGroup");
        }

        [HttpPost]
        public async Task<IActionResult> Submit([Bind("Id", "LeaveTypes")] LeaveGroupViewModel leaveGroupViewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Create a new LeaveGroup
                    var leaveGroup = new LeaveGroup
                    {
                        Id = leaveGroupViewModel.Id
                    };

                    //_db.LeaveGroups.Add(leaveGroup);
                    //await _db.SaveChangesAsync();

                    // Add LeaveTypes
                    if (leaveGroupViewModel.LeaveTypes != null)
                    {
                        foreach (var leaveType in leaveGroupViewModel.LeaveTypes)
                        {
                            leaveType.LeaveGroupId = leaveGroup.Id;
                            _db.LeaveTypes.Add(leaveType);
                        }

                        await _db.SaveChangesAsync();
                    }
                    TempData["SuccessMessage"] = "Data submitted successfully.";
                    return RedirectToAction("Index");


                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An error occurred while saving the data.");
            }

            return BadRequest(new { error = "Error submitting data" });
        }
        [HttpGet]
        public async Task<IActionResult> Leave_Edit(int id)
        {
            if (id == null || id == 0)
            {
                return View();
            }
            var leaveType = await _db.LeaveTypes.FirstOrDefaultAsync(m => m.Id == id);
            if (leaveType == null)
            {
                return NotFound();
            }
            return View(leaveType);

        }
        // Handle the POST request for updating data
        [HttpPost]
        public IActionResult Leave_Edit(LeaveType type)
        {
            _db.LeaveTypes.Update(type);
            _db.SaveChanges();
            return RedirectToAction("Index");
        }


        [HttpGet]

        public IActionResult Delete(int id)
        {
            LeaveType type = _db.LeaveTypes.Find(id);
            if (type == null)
            {
                return NotFound();
            }
            return View(type);
        }

        [HttpPost]
        public IActionResult DeleteConfirmed(int id)
        {
            LeaveType type = _db.LeaveTypes.Find(id);
            if (type == null)
            {
                return NotFound();
            }

            _db.LeaveTypes.Remove(type);
            _db.SaveChanges();
            TempData["success"] = "Leave type deleted!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult remaningdays()
        {
            var claimsprincipal = HttpContext.User;
            var employeeidclaim = claimsprincipal.FindFirst(ClaimTypes.Sid);

            if (employeeidclaim != null && int.TryParse(employeeidclaim.Value, out int loggedinemployeeid))
            {
                // use the logged-in user's id instead of selectedemployeeid

                var employeedata = _db.openings
      .Where(o => o.EmpID == loggedinemployeeid)
      .GroupJoin(
          _db.balances,
          o => o.Id,
          b => b.OpeningID,
          (o, balancegroup) => new { opening = o, balances = balancegroup }
      )
      .SelectMany(
          ob => ob.balances.DefaultIfEmpty(),
          (ob, balance) => new
          {
              employeeid = ob.opening.EmpID,
              groupid = ob.opening.GroupID,
              typeid = balance.TypeID,
              openingdays = balance.Days
          }
      )
      .GroupJoin(
          _db.LeaveStatuses
              .Where(ls => ls.TeacherID == loggedinemployeeid && ls.Status == true),
          ob => ob.employeeid,
          ls => ls.TeacherID,
          (ob, leavestatusgroup) => new { ob, leavestatuses = leavestatusgroup }
      )
      .SelectMany(
          obls => obls.leavestatuses.DefaultIfEmpty(),
          (obls, leavestatus) => new RemainingDaysViewModel
          {
              EmployeeID = obls.ob.employeeid,
              GroupID = obls.ob.groupid,
              EmployeeName = _db.Employees.FirstOrDefault(e => e.EmployeeId == obls.ob.employeeid).FName,
              GroupName = _db.LeaveGroups.FirstOrDefault(g => g.Id == obls.ob.groupid).GroupName,
              TypeName = _db.LeaveTypes.FirstOrDefault(t => t.Id == obls.ob.typeid).TypeName,
              OpeningDays = obls.ob.openingdays,
              UtilizedDays = leavestatus != null && leavestatus.TypeID == obls.ob.typeid ? leavestatus.Days ?? 0 : 0

          }
      )
      .GroupBy(result => new { result.EmployeeID, result.EmployeeName, result.GroupID, result.GroupName, result.TypeName })
      .Select(group => new RemainingDaysViewModel
      {
          EmployeeID = group.Key.EmployeeID,
          GroupID = group.Key.GroupID,
          EmployeeName = group.Key.EmployeeName,
          GroupName = group.Key.GroupName,
          TypeName = group.Key.TypeName,
          OpeningDays = group.FirstOrDefault().OpeningDays,
          UtilizedDays = group.Sum(g => g.UtilizedDays),
          RemainingDays = group.FirstOrDefault().OpeningDays - group.Sum(g => g.UtilizedDays)
      })
      .ToList();
                return View(employeedata);







                // Handle the POST request for deleting data
                //[HttpPost]
                //public async Task<IActionResult> DeleteData(int id)
                //{
                //    var dataToDelete = await _db.LeaveTypes.FindAsync(id);

                //    if (dataToDelete == null)
                //    {
                //        return NotFound("Data not found");
                //    }

                //    _db.LeaveTypes.Remove(dataToDelete);

                //    try
                //    {
                //        await _db.SaveChangesAsync();
                //        return Ok("Data deleted successfully");
                //    }
                //    catch (DbUpdateException)
                //    {
                //        return StatusCode(500, "An error occurred while deleting the data.");
                //    }
                //}

                //}
                //return View();



            }
            return View();
        }
    }
}



