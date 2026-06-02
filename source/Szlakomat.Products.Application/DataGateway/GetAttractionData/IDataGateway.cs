using Szlakomat.Products.Application.DataGateway.Common;
using Szlakomat.Products.Domain.Common;

namespace Szlakomat.Products.Application.DataGateway.GetAttractionData;

internal interface IDataGateway
{
    Task<Result<ErrorInfo, QueryResponse>> Query(GetAttractionData request, string routingKey, CancellationToken ct);
}
