using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeSecureAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeeController : ControllerBase
{
    [HttpGet]
    [Authorize] // Requires valid token
    public IActionResult GetEmployees()
    {
        var employees = new[]
        {
            new { Id = 1, Name = "Abhishek Panda", Role = "Engineer" },
            new { Id = 2, Name = "Ravi Kumar", Role = "Manager" }
        };
        return Ok(employees);
    }
}
