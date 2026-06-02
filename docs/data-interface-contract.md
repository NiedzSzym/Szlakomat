# Kontrakt interfejsu danych (Data Gateway)

Wspólne źródło prawdy dla bramy danych i dostawców (providerów).

---

## Endpoint

```
POST /api/data/query
Content-Type: application/json
```

---

## Wejście — envelope

```json
{
  "type": "string",
  "city": "string",
  "payload": { ... }
}
```

| Pole | Typ | Opis |
|------|-----|------|
| `type` | `string` | Rodzaj danych. Normalizowany: `trim()` + `toLowerCase()`. |
| `city` | `string` | Miasto. Normalizowane jak wyżej. |
| `payload` | `object` | Parametry zależne od typu. **Gateway go nie waliduje** — walidacja leży po stronie providera. |

### Dopuszczalne wartości (po normalizacji)

- `type`: `"pricing"`, `"events"`
- `city`: `"krakow"`, `"warszawa"`

---

## Przykłady

### Zapytanie o cennik atrakcji

**Request:**
```json
{
  "type": "Pricing",
  "city": "Krakow",
  "payload": { "attractionId": "wawel-castle" }
}
```

**Odpowiedź (sukces):**
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
    "source":   "mock"
  }
}
```

### Zapytanie o wydarzenia przy atrakcji

**Request:**
```json
{
  "type": "events",
  "city": "warszawa",
  "payload": { "attractionId": "lazienki-park" }
}
```

**Odpowiedź (sukces):**
```json
{
  "status": "ok",
  "type": "events",
  "city": "warszawa",
  "data": {
    "attractionId": "lazienki-park",
    "events": [
      { "title": "Koncert chopinowski", "date": "2025-07-20", "free": true },
      { "title": "Noc Muzeów",          "date": "2025-05-17", "free": true }
    ]
  },
  "meta": {
    "provider": "events.warszawa",
    "source":   "mock"
  }
}
```

> **Uwaga:** `events` zawsze identyfikuje konkretną atrakcję przez `payload.attractionId`.

---

## Format odpowiedzi sukces

```json
{
  "status": "ok",
  "type":   "<znormalizowany type>",
  "city":   "<znormalizowane city>",
  "data":   { ... },
  "meta": {
    "provider": "<klucz routingu, np. pricing.krakow>",
    "source":   "<mock|live>"
  }
}
```

---

## Format odpowiedzi błąd

```json
{
  "status": "error",
  "error": {
    "code":    "KOD_BLEDU",
    "message": "Czytelny opis błędu"
  }
}
```

---

## Kody błędów i mapowanie HTTP

| Kod błędu | HTTP | Kto zwraca | Opis |
|-----------|------|------------|------|
| `VALIDATION_ERROR` | 400 | Gateway | Brakujące lub puste `type` / `city` w envelope |
| `PROVIDER_NOT_FOUND` | 404 | Gateway | Brak zarejestrowanego providera dla klucza `{type}.{city}` |
| `ATTRACTION_NOT_FOUND` | 404 | Provider | Provider nie zna podanego `attractionId` |
| `PROVIDER_TIMEOUT` | 504 | Gateway | Provider nie odpowiedział w limicie czasu |
| `INTERNAL_ERROR` | 500 | Provider / Gateway | Nieoczekiwany błąd wewnętrzny |
| `NOT_IMPLEMENTED` | 501 | Gateway (stub) | Brama istnieje, ale transport RabbitMQ jeszcze nie zaimplementowany |

---

## Podział odpowiedzialności

| Warstwa | Waliduje | Zna |
|---------|----------|-----|
| **Gateway** | envelope (`type`, `city` — wymagane, znany klucz routingu) | listę providerów |
| **Provider** | payload (np. `attractionId` — czy istnieje w bazie mocków) | dane atrakcji dla danego miasta |

---

## Routing (transport RabbitMQ — kolejny etap)

- **Klucz routingu:** `"{type}.{city}"` (po normalizacji), np. `"pricing.krakow"`
- **Exchange:** `szlakomat.data` (topic exchange)
- **Wzorzec komunikacji:** RPC — gateway wysyła wiadomość z `correlation_id` i `reply_to` (tymczasowa kolejka zwrotna) i czeka na odpowiedź
- Szczegóły transportu są niewidoczne dla dewelopera korzystającego z REST API

---

## Znani providerzy (na bieżący etap)

| Klucz routingu | Status |
|----------------|--------|
| `pricing.krakow` | stub (NOT_IMPLEMENTED) |
| `events.krakow` | stub (NOT_IMPLEMENTED) |
| `pricing.warszawa` | stub (NOT_IMPLEMENTED) |
| `events.warszawa` | stub (NOT_IMPLEMENTED) |

---

## Identyfikatory atrakcji (`attractionId`)

Na obecnym etapie `attractionId` to zwykły klucz tekstowy dopasowywany do bazy mocków po stronie providera (np. `"wawel-castle"`, `"lazienki-park"`). Format zostanie ustandaryzowany w kolejnym etapie.
