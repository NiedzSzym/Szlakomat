using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Szlakomat.Products.Application.DataGateway;
using Szlakomat.Products.Application.DataGateway.Common;
using Szlakomat.Products.Application.DataGateway.GetAttractionData;
using Szlakomat.Products.Domain.Common;

namespace Szlakomat.Products.Infrastructure.DataGateway;

internal sealed class RabbitMqDataGateway : IDataGateway, IAsyncDisposable
{
    private readonly DataGatewayOptions _opts;
    private IConnection? _connection;
    private IChannel?    _channel;
    private string       _replyQueue = string.Empty;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;

    private static readonly JsonSerializerOptions CaseInsensitive = new()
        { PropertyNameCaseInsensitive = true };

    public RabbitMqDataGateway(DataGatewayOptions opts) => _opts = opts;

    // ── inicjalizacja ────────────────────────────────────────────────────────

    private async Task EnsureInitAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await InitCoreAsync(ct);
            _initialized = true;
        }
        catch
        {
            // Nie ustawiamy _initialized — kolejne wywołanie spróbuje ponownie
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task InitCoreAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = _opts.RabbitMqHost };
        _connection = await factory.CreateConnectionAsync(ct);
        _channel    = await _connection.CreateChannelAsync(cancellationToken: ct);

        // Idempotentna deklaracja exchange — te same parametry co provider
        await _channel.ExchangeDeclareAsync(
            exchange:    _opts.Exchange,
            type:        "topic",
            durable:     true,
            autoDelete:  false,
            cancellationToken: ct);

        // Ekskluzywna kolejka zwrotna (serwer nadaje nazwę)
        var qr = await _channel.QueueDeclareAsync(
            queue:       string.Empty,
            durable:     false,
            exclusive:   true,
            autoDelete:  true,
            cancellationToken: ct);
        _replyQueue = qr.QueueName;

        // Jeden konsument odpowiedzi
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, ea) =>
        {
            var corrId = ea.BasicProperties.CorrelationId;
            if (corrId is not null && _pending.TryRemove(corrId, out var tcs))
                tcs.TrySetResult(Encoding.UTF8.GetString(ea.Body.Span));
            return Task.CompletedTask;
        };
        await _channel.BasicConsumeAsync(
            queue:    _replyQueue,
            autoAck:  true,
            consumer: consumer,
            cancellationToken: ct);
    }

    // ── RPC ──────────────────────────────────────────────────────────────────

    public async Task<Result<ErrorInfo, QueryResponse>> Query(
        GetAttractionData request, string routingKey, CancellationToken ct)
    {
        await EnsureInitAsync(ct);

        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;

        try
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                type    = request.Type,
                city    = request.City,
                payload = request.Payload
            });

            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo       = _replyQueue,
                ContentType   = "application/json"
            };

            await _channel!.BasicPublishAsync(
                exchange:        _opts.Exchange,
                routingKey:      routingKey,
                mandatory:       false,
                basicProperties: props,
                body:            body,
                cancellationToken: ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_opts.TimeoutSeconds));

            var responseJson = await tcs.Task.WaitAsync(timeoutCts.Token);
            return ParseResponse(responseJson);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Upłynął timeout — ct nie zostało anulowane przez wywołującego
            return Result<ErrorInfo, QueryResponse>.FailureOf(
                new ErrorInfo("PROVIDER_TIMEOUT",
                    $"Brak odpowiedzi od {routingKey} w {_opts.TimeoutSeconds}s"));
        }
        finally
        {
            _pending.TryRemove(correlationId, out _);
        }
    }

    // ── deserializacja odpowiedzi ────────────────────────────────────────────

    private static Result<ErrorInfo, QueryResponse> ParseResponse(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;
            var status     = root.TryGetProperty("status", out var sp) ? sp.GetString() : null;

            if (status == "ok")
            {
                var response = JsonSerializer.Deserialize<QueryResponse>(json, CaseInsensitive);
                return response is not null
                    ? Result<ErrorInfo, QueryResponse>.SuccessOf(response)
                    : Result<ErrorInfo, QueryResponse>.FailureOf(
                        new ErrorInfo("INTERNAL_ERROR", "Nieprawidłowa odpowiedź od providera"));
            }

            var code    = "INTERNAL_ERROR";
            var message = "Błąd od providera";
            if (root.TryGetProperty("error", out var errEl))
            {
                if (errEl.TryGetProperty("code",    out var cEl)) code    = cEl.GetString() ?? code;
                if (errEl.TryGetProperty("message", out var mEl)) message = mEl.GetString() ?? message;
            }
            return Result<ErrorInfo, QueryResponse>.FailureOf(new ErrorInfo(code, message));
        }
        catch (JsonException)
        {
            return Result<ErrorInfo, QueryResponse>.FailureOf(
                new ErrorInfo("INTERNAL_ERROR", "Nie można przetworzyć odpowiedzi od providera"));
        }
    }

    // ── cleanup ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_channel    is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
    }
}
