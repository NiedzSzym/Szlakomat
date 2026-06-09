# Data Interface — przewodnik dewelopera

Jeden endpoint JSON-in/JSON-out, który trasuje zapytania do zewnętrznych providerów danych (RabbitMQ RPC).
Szczegółowy kontrakt wiadomości: [data-interface-contract.md](data-interface-contract.md).

---

## Jak uruchomić

### 1. Uruchom brokerów i providerów

```bash
cd data-providers
docker compose up -d        # RabbitMQ + 4 kontenery providerów
```

Czekaj aż `docker compose ps` pokaże wszystkie kontenery jako `Up (healthy)`.

### 2. Uruchom API Szlakomatu

```bash
cd source
dotnet run --project Szlakomat.Products.Api
# Nasłuchuje na http://localhost:5000
```

---

## Endpoint

```
POST /api/data/query
Content-Type: application/json
```

### Ciało żądania

```jsonc
{
  "type": "<typ danych>",          // wymagane: "pricing" | "events"
  "city": "<miasto>",              // wymagane: "krakow" | "warszawa"
  "payload": { ... }              // wymagane: obiekt specyficzny dla providera
}
```

### Odpowiedź sukcesu — HTTP 200

```json
{
  "status": "ok",
  "type": "pricing",
  "city": "krakow",
  "data": { ... },
  "meta": {
    "provider": "pricing.krakow",
    "source": "mock"
  }
}
```

### Odpowiedź błędu

```json
{
  "status": "error",
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Pole 'type' i 'city' są wymagane"
  }
}
```

---

## Kody błędów → statusy HTTP

| Kod                  | HTTP | Opis                                                                  |
|----------------------|------|-----------------------------------------------------------------------|
| `VALIDATION_ERROR`   | 400  | Brak/błąd pola `type`/`city`, niepoprawny JSON, zły Content-Type     |
| `PROVIDER_NOT_FOUND` | 404  | Brak providera dla kombinacji `type.city`                             |
| `ATTRACTION_NOT_FOUND` | 404 | Provider nie znalazł atrakcji o podanym `attractionId`               |
| `PROVIDER_TIMEOUT`   | 504  | Provider nie odpowiedział w czasie `DataGateway:TimeoutSeconds`       |
| `INTERNAL_ERROR`     | 500  | Broker niedostępny, błąd infrastruktury, niepoprawna odpowiedź       |

---

## Dostępni providerzy

| Routing key        | Opis                                  |
|--------------------|---------------------------------------|
| `pricing.krakow`   | Cennik atrakcji — Kraków              |
| `pricing.warszawa` | Cennik atrakcji — Warszawa            |
| `events.krakow`    | Nadchodzące wydarzenia — Kraków       |
| `events.warszawa`  | Nadchodzące wydarzenia — Warszawa     |

Konfiguracja w `appsettings.json`:

```json
"DataGateway": {
  "RabbitMqHost": "localhost",
  "Exchange": "szlakomat.data",
  "TimeoutSeconds": 10,
  "KnownProviders": ["pricing.krakow", "events.krakow", "pricing.warszawa", "events.warszawa"]
}
```

### Jak dodać kolejnego providera

1. Dodaj routing key do `KnownProviders` w `appsettings.json` (np. `"pricing.gdansk"`)
2. Utwórz nowy Worker Service w repo `data-providers` dziedzicząc po `RpcConsumerBase`
3. Dodaj serwis do `docker-compose.yml` z odpowiednimi env (`PROVIDER_CITY`, `RABBITMQ_HOST`, `MOCK_DATA_PATH`)

---

## Przykłady curl

### pricing/krakow

```bash
curl -s -X POST http://localhost:5000/api/data/query \
  -H "Content-Type: application/json" \
  -d '{"type":"pricing","city":"krakow","payload":{"attractionId":"wawel-castle"}}' | jq .
```

```json
{
  "status": "ok",
  "type": "pricing",
  "city": "krakow",
  "data": {
    "attractionId": "wawel-castle",
    "currency": "PLN",
    "prices": [
      { "label": "Bilet normalny", "amount": 3500 },
      { "label": "Bilet ulgowy",   "amount": 2200 }
    ]
  },
  "meta": { "provider": "pricing.krakow", "source": "mock" }
}
```

### events/krakow (z filtrem dat)

```bash
curl -s -X POST http://localhost:5000/api/data/query \
  -H "Content-Type: application/json" \
  -d '{"type":"events","city":"krakow","payload":{"attractionId":"wawel-castle","from":"2026-07-15","to":"2026-07-31"}}' | jq .
```

### pricing/warszawa

```bash
curl -s -X POST http://localhost:5000/api/data/query \
  -H "Content-Type: application/json" \
  -d '{"type":"pricing","city":"warszawa","payload":{"attractionId":"lazienki-park"}}' | jq .
```

### events/warszawa

```bash
curl -s -X POST http://localhost:5000/api/data/query \
  -H "Content-Type: application/json" \
  -d '{"type":"events","city":"warszawa","payload":{"attractionId":"lazienki-park"}}' | jq .
```

---

## Błędy — przykłady curl

### 400 — niepoprawny JSON

```bash
curl -s -X POST http://localhost:5000/api/data/query \
  -H "Content-Type: application/json" \
  -d '{ zly json }' | jq .
# {"status":"error","error":{"code":"VALIDATION_ERROR","message":"..."}}
```

### 400 — zły Content-Type

```bash
curl -s -X POST http://localhost:5000/api/data/query \
  -H "Content-Type: text/plain" \
  -d '{"type":"pricing","city":"krakow","payload":{}}' | jq .
# {"status":"error","error":{"code":"VALIDATION_ERROR","message":"Wymagany Content-Type: application/json"}}
```

### 404 — nieznany provider

```bash
curl -s -X POST http://localhost:5000/api/data/query \
  -H "Content-Type: application/json" \
  -d '{"type":"pricing","city":"gdansk","payload":{}}' | jq .
# {"status":"error","error":{"code":"PROVIDER_NOT_FOUND","message":"..."}}
```

### 504 — timeout providera

```bash
# (gdy provider nie odpowiada w ciągu DataGateway:TimeoutSeconds sekund)
# {"status":"error","error":{"code":"PROVIDER_TIMEOUT","message":"..."}}
```
