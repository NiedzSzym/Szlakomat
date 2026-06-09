# Integracja data-providers z systemem Szlakomat

## Czym jest ten moduł i po co istnieje

Szlakomat to aplikacja .NET/C# modelująca atrakcje turystyczne, bilety i pakiety wycieczek. Jej model domeny jest kompletny i nie jest tutaj modyfikowany — ten moduł **dokłada się z boku** jako warstwa dostarczania danych z zewnątrz.

Cel: deweloper wysyła jedno zapytanie REST i dostaje JSON z ceną lub wydarzeniami dla konkretnej atrakcji. Nie musi wiedzieć, ile serwisów obsługuje żądanie ani jak są wdrożone.

---

## Miejsce w architekturze

```
┌─────────────────────────────────┐
│         Aplikacja Szlakomat     │
│  (Domain / Application / API)   │
│                                 │
│  POST /api/data/query           │
│         │                       │
│    [Data Gateway]               │  ← wpięty w warstwy Szlakomatu
│    waliduje envelope            │
│    buduje klucz routingu        │
└────────────┬────────────────────┘
             │ RabbitMQ RPC
             │ exchange: szlakomat.data (topic)
             │ klucz: "{type}.{city}"
    ┌────────┴────────────────────────────────┐
    │            data-providers               │
    │  (osobna solucja, osobny docker-compose) │
    │                                         │
    │  ┌──────────────┐  ┌─────────────────┐  │
    │  │ PricingProvider  │  EventsProvider  │  │
    │  │ routing: pricing │  routing: events  │  │
    │  └──────────────┘  └─────────────────┘  │
    │                                         │
    │  wspólny: MockDataStore (attractions.json) │
    └─────────────────────────────────────────┘
```

Moduł `data-providers` jest **odrębnym procesem** — ma własną solucję .NET, własny `docker-compose.yml` i własnego brokera RabbitMQ. Łączy się z bramą wyłącznie przez kolejkę, nie przez referencję projektu ani HTTP.

---

## Przepływ żądania krok po kroku

```
Developer
  │
  │  POST /api/data/query
  │  { "type": "pricing", "city": "krakow", "payload": { "attractionId": "wawel-castle" } }
  ▼
[Data Gateway — Szlakomat]
  1. Waliduje envelope: type i city muszą być niepuste i znane
  2. Buduje klucz routingu: "pricing.krakow"
  3. Publikuje na exchange "szlakomat.data" z reply_to (kolejka zwrotna) i correlation_id
  4. Czeka na odpowiedź (timeout → 504 PROVIDER_TIMEOUT)
  │
  │  RabbitMQ topic exchange
  │  klucz: "pricing.krakow"
  ▼
[PricingProvider — data-providers]
  5. Odbiera wiadomość z kolejki "pricing"
  6. Deserializuje DataQuery, odczytuje city z treści zapytania
  7. Waliduje payload (attractionId wymagane)
  8. Szuka atrakcji w MockDataStore (attractions.json[city][attractionId])
  9. Odsyła QueryResponse na reply_to z tym samym correlation_id
  │
  ▼
[Data Gateway — Szlakomat]
  10. Dopasowuje odpowiedź po correlation_id
  11. Mapuje QueryResponse → HTTP response i zwraca deweloperowi
```

---

## Kontrakt

Pełna specyfikacja: [`../docs/data-interface-contract.md`](../docs/data-interface-contract.md)

### Endpoint

```
POST /api/data/query
Content-Type: application/json
```

### Zapytanie (envelope)

```json
{
  "type": "pricing",
  "city": "krakow",
  "payload": { "attractionId": "wawel-castle" }
}
```

Pola `type` i `city` są normalizowane przez bramę (`trim()` + `toLowerCase()`). Pole `payload` brama przekazuje bez zmian — waliduje je provider.

### Odpowiedź sukces

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
  "meta": {
    "provider": "pricing.krakow",
    "source": "mock"
  }
}
```

Kwoty w `amount` są w **groszach** (3500 = 35,00 PLN).

### Odpowiedź błąd

```json
{
  "status": "error",
  "error": {
    "code": "ATTRACTION_NOT_FOUND",
    "message": "Attraction 'wawel-castle' not found in krakow"
  }
}
```

### Kody błędów i mapowanie HTTP

| Kod | HTTP | Zwraca | Kiedy |
|-----|------|--------|-------|
| `VALIDATION_ERROR` | 400 | Gateway | Brakujące lub puste `type`/`city` w envelope |
| `PROVIDER_NOT_FOUND` | 404 | Gateway | Nieznana para `type`+`city` (np. `pricing.gdansk`) |
| `ATTRACTION_NOT_FOUND` | 404 | Provider | `attractionId` nie istnieje w danych dla danego miasta |
| `PROVIDER_TIMEOUT` | 504 | Gateway | Provider nie odpowiedział w limicie czasu |
| `INTERNAL_ERROR` | 500 | Provider / Gateway | Nieoczekiwany wyjątek |

---

## Podział odpowiedzialności między bramą a providerami

| Warstwa | Co waliduje | Co zna |
|---------|-------------|--------|
| **Data Gateway** (Szlakomat) | `type` i `city` — czy niepuste i znane | listę zarejestrowanych kluczy routingu |
| **Provider** (data-providers) | `payload` — czy `attractionId` jest podane i istnieje w danych | dane atrakcji dla wszystkich miast |

Brama **nie zagląda** w `payload`. Provider **nie zna** listy miast z góry — obsługuje każde miasto obecne w pliku `attractions.json`.

---

## Routing przez RabbitMQ

| Element | Wartość |
|---------|---------|
| Exchange | `szlakomat.data` (topic) |
| Wzorzec klucza | `"{type}.{city}"`, np. `pricing.krakow` |
| Wzorzec komunikacji | RPC: `correlation_id` + `reply_to` (tymczasowa kolejka zwrotna) |
| Kolejka PricingProvider | `pricing` (binding: `pricing.#`) |
| Kolejka EventsProvider | `events` (binding: `events.#`) |

Provider czyta `city` z treści wiadomości (`DataQuery.City`), nie z klucza routingu. Dzięki temu jeden kontener obsługuje wiele miast.

---

## Struktura data-providers

```
data-providers/
├── docker-compose.yml          # RabbitMQ + 2 workery (pricing, events)
├── mock-data/
│   └── attractions.json        # dane mock: miasto → atrakcja → { pricing, events }
└── src/
    ├── DataProviders.Shared/   # RpcConsumerBase, MockDataStore, kontrakty
    ├── PricingProvider/        # worker: obsługuje klucz routing "pricing"
    ├── EventsProvider/         # worker: obsługuje klucz routing "events"
    └── DataProviders.Tests/    # testy jednostkowe
```

### DataProviders.Shared

Wspólna biblioteka dla obu workerów:

- **`RpcConsumerBase`** — `BackgroundService` łączący się z RabbitMQ, deserializujący `DataQuery`, wywołujący handler i odsyłający `QueryResponse`. Obsługuje błędy: zły JSON → `VALIDATION_ERROR`, wyjątek → `INTERNAL_ERROR`, brak `ReplyTo` → ack bez odpowiedzi. Zawsze ACK — wiadomość nie wraca do kolejki.
- **`MockDataStore`** — singleton wczytujący `attractions.json` przy starcie. Brak pliku → pusty słownik, każde zapytanie zwróci `ATTRACTION_NOT_FOUND`.
- **Kontrakty** — `DataQuery`, `QueryResponse` (serializacja/deserializacja JSON na granicy RabbitMQ).

### PricingProvider i EventsProvider

Każdy worker to:

1. `ConsumerService` dziedziczący z `RpcConsumerBase` — rejestruje queue i binding.
2. `RequestHandler` — logika domenowa: szuka atrakcji w `MockDataStore`, buduje odpowiedź.

---

## Format danych mock

Plik `mock-data/attractions.json` jest montowany do kontenerów w trybie read-only:

```json
{
  "krakow": {
    "wawel-castle": {
      "name": "Zamek Wawel",
      "pricing": {
        "currency": "PLN",
        "prices": [
          { "label": "Bilet normalny", "amount": 3500 },
          { "label": "Bilet ulgowy",   "amount": 2200 }
        ]
      },
      "events": {
        "upcoming": [
          { "title": "Wystawa korony",          "date": "2026-07-15" },
          { "title": "Dni Dziedzictwa Wawelu",  "from": "2026-09-01", "to": "2026-09-03" }
        ]
      }
    }
  }
}
```

Dwa formaty dat wydarzeń:
- **`date`** — wydarzenie jednorazowe
- **`from` / `to`** — wydarzenie wielodniowe; oba pola wymagane łącznie; format `YYYY-MM-DD`

Provider `events` obsługuje opcjonalne filtrowanie zakresu dat (`payload.from` / `payload.to`).

---

## Uruchomienie

```bash
cd data-providers
docker compose up -d
```

Uruchamia RabbitMQ oraz oba workery. Panel administracyjny: http://localhost:15672 (guest/guest).

Brama Szlakomatu musi wskazywać na ten sam broker (zmienna `RABBITMQ_HOST`).

---

## Rozszerzalność

Architektura pozwala dodawać nowe miasta i typy danych bez zmian w bramie:

| Co dodajesz | Co robisz |
|-------------|-----------|
| Nowe miasto | Dodajesz klucz do `attractions.json` + `docker compose restart` |
| Nowy typ danych (np. `reviews`) | Nowy worker dziedziczący z `RpcConsumerBase`, nowa kolejka z bindingiem `reviews.#`, wpis w `docker-compose.yml` |
| Prawdziwe API zamiast mocków | Podmieniasz `MockDataStore` na klienta HTTP w handlerze — kontrakt RabbitMQ i REST pozostają bez zmian |

---

## Testy

```bash
cd data-providers
dotnet test src/DataProviders.Tests/DataProviders.Tests.csproj
```

Pokrycie: `MockDataStore` (ładowanie, brakujący plik, lookups), `PricingRequestHandler` i `EventsRequestHandler` (sukces, kody błędów, filtr dat, kontrakt no-throw).
