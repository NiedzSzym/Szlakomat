using MediatR;
using Microsoft.AspNetCore.Mvc;
using Szlakomat.Products.Api.Contracts.DataGateway;
using Szlakomat.Products.Application.DataGateway.GetAttractionData;

namespace Szlakomat.Products.Api.Controllers;

[ApiController]
[Route("api/data")]
[Produces("application/json")]
public class DataController(ISender mediator) : ControllerBase
{
    [HttpPost("query")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Query(QueryRequest req)
    {
        var cmd = new GetAttractionData(req.Type, req.City, req.Payload);
        var result = await mediator.Send(cmd);
        return result.Fold<IActionResult>(
            failure => failure.Code == "NOT_IMPLEMENTED"
                ? StatusCode(StatusCodes.Status501NotImplemented, new { status = "error", error = failure })
                : BadRequest(new { status = "error", error = failure }),
            success => Ok(success)
        );
    }
}
