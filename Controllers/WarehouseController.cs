using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Tutorial9.Model;
using Tutorial9.Services;

namespace Tutorial9.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IDbService _dbService;

    public WarehouseController(IDbService dbService)
    {
        _dbService = dbService;
    }

    [HttpPost]
    public async Task<IActionResult> AddProductToWarehouse([FromBody] ProductWarehouseRequestDTO requestDto)
    {
        if (requestDto.Amount <= 0)
            return BadRequest("Amount must be greater than 0");

        try
        {
            int id = await _dbService.AddProductToWarehouseAsync(requestDto);
            return Ok(new { IdProductWarehouse = id });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("procedure")]
    public async Task<IActionResult> CallProcedureWithParams([FromBody] ProductWarehouseRequestDTO requestDto)
    {
        try
        {
            int resultId = await _dbService.ProcedureAsync(requestDto);
            return Ok(new { IdProductWarehouse = resultId });
        }
        catch (SqlException ex)
        {
            return BadRequest($"SQL error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }
}