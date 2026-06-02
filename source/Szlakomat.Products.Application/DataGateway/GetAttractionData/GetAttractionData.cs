using System.Text.Json;
using MediatR;
using Szlakomat.Products.Application.DataGateway.Common;
using Szlakomat.Products.Domain.Common;

namespace Szlakomat.Products.Application.DataGateway.GetAttractionData;

public record GetAttractionData(string Type, string City, JsonElement Payload)
    : IRequest<Result<ErrorInfo, QueryResponse>>;
