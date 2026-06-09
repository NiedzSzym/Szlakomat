# data-providers

Zestaw niezależnych Worker Service'ów odpowiadających na zapytania RPC przez RabbitMQ. Każdy serwis obsługuje jeden typ danych (`pricing` albo `events`) dla wszystkich miast. Miasto pochodzi z treści zapytania, nie z konfiguracji kontenera.

---

## Wymagania

- Docker + Docker Compose
- (opcjonalnie, do uruchomienia testów) .NET SDK 10

---

## Uruchomienie

```bash
cd data-providers
docker compose up -d
```

Polecenie uruchamia:

| Kontener              | Routing key | Opis                                    |
|-----------------------|-------------|-----------------------------------------|
| `rabbitmq`            | —           | Broker (port 5672, panel 15672)         |
| `szlakomat-pricing`   | `pricing`   | Cennik atrakcji — wszystkie miasta      |
| `szlakomat-events`    | `events`    | Nadchodzące wydarzenia — wszystkie miasta |

Sprawdź stan:

```bash
docker compose ps
```

Panel administracyjny RabbitMQ: http://localhost:15672 (login: `guest` / `guest`)

### Zatrzymanie

```bash
docker compose down
```

---

## Dane mock (mock-data/)

Plik `mock-data/attractions.json` jest montowany do każdego kontenera w trybie read-only.

### Format

```json
{
  "<miasto>": {
    "<attraction-id>": {
      "name": "Nazwa atrakcji",
      "pricing": {
        "currency": "PLN",
        "prices": [
          { "label": "Bilet normalny", "amount": 3500 }
        ]
      },
      "events": {
        "upcoming": [
          { "title": "Wydarzenie jednorazowe", "date": "2026-07-15" },
          { "title": "Wydarzenie wielodniowe", "from": "2026-07-20", "to": "2026-07-22" }
        ]
      }
    }
  }
}
```

Kwoty w `pricing.prices[].amount` są w **groszach** (3500 = 35,00 PLN).

Daty wydarzeń:
- **`date`** — wydarzenie jednorazowe (traktowane jak `from` = `to` = podana data)
- **`from` / `to`** — wydarzenie wielodniowe; oba pola wymagane łącznie; format `YYYY-MM-DD`

### Jak dodać atrakcję

Dodaj klucz do odpowiedniego miasta w `mock-data/attractions.json`:

```json
{
  "krakow": {
    "nowa-atrakcja": {
      "name": "Nowa Atrakcja",
      "pricing": { ... },
      "events":  { "upcoming": [] }
    }
  }
}
```

Nie ma potrzeby restartu kontenerów — plik jest montowany; wartość jest cachowana przy starcie kontenera, więc przy dodaniu nowych danych uruchom:

```bash
docker compose restart pricing events
```

### Jak dodać miasto

1. Dodaj klucz miasta do `mock-data/attractions.json` (np. `"gdansk": { ... }`)
2. Zrestartuj kontenery (`docker compose restart pricing events`)

Nie trzeba zmieniać `docker-compose.yml` ani `appsettings.json` — miasto pochodzi z treści zapytania.

---

## Architektura

```
┌──────────────────────────────────────────────────────────┐
│  DataProviders.Shared                                    │
│  ├── RpcConsumerBase   (BackgroundService + RabbitMQ)    │
│  ├── MockDataStore     (singleton JSON cache)            │
│  └── Contracts         (DataQuery, QueryResponse, …)     │
└──────────┬───────────────────────────────────────────────┘
           │ dziedziczą
┌──────────┴───────┐        ┌───────────────────────┐
│  PricingProvider │        │   EventsProvider       │
│  routing: pricing│        │   routing: events      │
│  ├─ ConsumerSvc  │        │   ├─ ConsumerSvc        │
│  └─ RequestHndlr │        │   └─ RequestHndlr       │
└──────────────────┘        └───────────────────────┘
```

Routing key zawiera wyłącznie typ (`pricing` / `events`). Miasto (`city`) jest częścią treści zapytania (`DataQuery.City`) i jest obsługiwane przez każdy provider dla dowolnego miasta obecnego w mock-data.

### Odporność konsumentów (RpcConsumerBase)

- **Niepoprawny JSON** w ciele wiadomości → loguje ostrzeżenie, odsyła `VALIDATION_ERROR`, ackuje
- **Wyjątek w logice biznesowej** → loguje błąd, odsyła `INTERNAL_ERROR`, ackuje; konsument działa dalej
- **Brak ReplyTo** → loguje ostrzeżenie, ackuje bez odpowiedzi
- **Zawsze ACK** — wiadomość nie jest wpychana z powrotem do kolejki w pętli

### Brak pliku mock-data

Jeśli plik `MOCK_DATA_PATH` nie istnieje, `MockDataStore` loguje błąd przy starcie i zwraca pusty słownik. Każde zapytanie otrzyma odpowiedź `ATTRACTION_NOT_FOUND`.

---

## Testy

```bash
cd data-providers
dotnet test src/DataProviders.Tests/DataProviders.Tests.csproj
```

Pokrycie:
- `MockDataStore` — ładowanie, brakujący plik, lookups
- `PricingRequestHandler` — sukces, VALIDATION_ERROR, ATTRACTION_NOT_FOUND, kontrakt no-throw
- `EventsRequestHandler` — sukces, filtr dat (from/to), VALIDATION_ERROR, ATTRACTION_NOT_FOUND, kontrakt no-throw
