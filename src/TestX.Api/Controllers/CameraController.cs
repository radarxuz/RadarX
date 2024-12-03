using Microsoft.AspNetCore.Mvc;
using TestX.Service.Interfaces;

namespace TestX.Api.Controllers;

public class CameraController : BaseController
{
    private readonly ICameraService cameraService;

    /// <summary>
    /// Initializes a new instance of the CameraController.
    /// </summary>
    /// <param name="cameraService">The camera service instance.</param>
    public CameraController(ICameraService cameraService)
    {
        this.cameraService = cameraService;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            return Ok(await cameraService.RetrieveAsync());
        }
        catch (Exception ex)
        {
            // Log the exception as needed
            return StatusCode(500, "An error occurred while retrieving cameras.");
        }
    }
}