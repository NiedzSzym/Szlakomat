using MediatR;
using Szlakomat.Products.Application.DataGateway.Common;
using Szlakomat.Products.Application.DataGateway;
using Szlakomat.Products.Domain.Common;

namespace Szlakomat.Products.Application.DataGateway.GetAttractionData;

internal sealed class GetAttractionDataHandler : IRequestHandler<GetAttractionData, Result<ErrorInfo, QueryResponse>>
{
    private readonly IDataGateway _gateway;
    private readonly DataGatewayOptions _options;

    public GetAttractionDataHandler(IDataGateway gateway, DataGatewayOptions options)
    {
        _gateway = gateway;
        _options = options;
    }

    public Task<Result<ErrorInfo, QueryResponse>> Handle(GetAttractionData request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.City))
            return Task.FromResult(Result<ErrorInfo, QueryResponse>.FailureOf(
                new ErrorInfo("VALIDATION_ERROR", "Pole 'type' i 'city' są wymagane")));

        var routingKey = RoutingKey.From(request.Type, request.City);

        if (!_options.KnownProviders.Contains(routingKey))
            return Task.FromResult(Result<ErrorInfo, QueryResponse>.FailureOf(
                new ErrorInfo("PROVIDER_NOT_FOUND", $"Brak providera dla {routingKey}")));

        return _gateway.Query(request, routingKey, cancellationToken);
    }
}
