using MediatR;
using Szlakomat.Products.Application.DataGateway.Common;
using Szlakomat.Products.Domain.Common;

namespace Szlakomat.Products.Application.DataGateway.GetAttractionData;

internal sealed class GetAttractionDataHandler : IRequestHandler<GetAttractionData, Result<ErrorInfo, QueryResponse>>
{
    private readonly IDataGateway _gateway;

    public GetAttractionDataHandler(IDataGateway gateway)
    {
        _gateway = gateway;
    }

    public Task<Result<ErrorInfo, QueryResponse>> Handle(GetAttractionData request, CancellationToken cancellationToken)
        => _gateway.Query(request, cancellationToken);
}
