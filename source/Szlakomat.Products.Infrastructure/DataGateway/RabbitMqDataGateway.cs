// TODO: zastąpić realną implementacją RabbitMQ gdy infrastruktura kolejki gotowa
using Szlakomat.Products.Application.DataGateway.Common;
using Szlakomat.Products.Application.DataGateway.GetAttractionData;
using Szlakomat.Products.Domain.Common;

namespace Szlakomat.Products.Infrastructure.DataGateway;

internal sealed class RabbitMqDataGateway : IDataGateway
{
    public Task<Result<ErrorInfo, QueryResponse>> Query(GetAttractionData request, CancellationToken ct)
        => Task.FromResult(Result<ErrorInfo, QueryResponse>.FailureOf(
            new ErrorInfo("NOT_IMPLEMENTED", "Brama danych jeszcze niezaimplementowana")));
}
