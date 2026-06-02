using MediatR;
using Microsoft.AspNetCore.Mvc;
using Szlakomat.Products.Api.Contracts.DataGateway;
using Szlakomat.Products.Application.DataGateway.Common;
using Szlakomat.Products.Application.DataGateway.GetAttractionData;

namespace Szlakomat.Products.Api.Controllers;

[ApiController]
[Route("api/data")]
[Produces("application/json")]
public class DataController(ISender mediator) : ControllerBase
{
    [HttpPost("query")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Query(QueryRequest req)
    {
        var cmd = new GetAttractionData(req.Type, req.City, req.Payload);
        var result = await mediator.Send(cmd);
        return result.Fold<IActionResult>(
            failure => failure.Code switch
            {
                "VALIDATION_ERROR"      => BadRequest(ErrorBody(failure)),
                "PROVIDER_NOT_FOUND"
                or "ATTRACTION_NOT_FOUND" => NotFound(ErrorBody(failure)),
                "PROVIDER_TIMEOUT"      => StatusCode(StatusCodes.Status504GatewayTimeout, ErrorBody(failure)),
                "INTERNAL_ERROR"        => StatusCode(StatusCodes.Status500InternalServerError, ErrorBody(failure)),
                _                       => StatusCode(StatusCodes.Status501NotImplemented, ErrorBody(failure))
            },
            success => Ok(success)
        );
    }

    private static object ErrorBody(ErrorInfo failure) => new { status = "error", error = failure };
}
