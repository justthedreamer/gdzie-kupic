# Architecture Overview

## Description

This is a microservices-based MVP application for a location-aware marketplace platform built with .NET Core and Vue.js (Quasar). The system enables merchants to register their locations and buyers to search for products within a specified radius.

### Core Features

- **Location Service**: Handles Polish postal code lookups, city searches, and radius-based geospatial queries using PostGIS
- **Business Logic Service**: Manages merchants, buyers, products, and order matching logic
- **API Gateway**: Single entry point for the frontend, routes requests to appropriate microservices

### Communication Flow

1. Frontend communicates exclusively with the API Gateway
2. Gateway routes requests to appropriate microservices based on URL path
3. Each microservice has its own isolated database
4. Location Service uses Redis for caching postal code and city data (TTL: 7 days)

---

## Tech Stack

| Layer                      | Technology                                           |
| -------------------------- | ---------------------------------------------------- |
| **Frontend**               | Vue.js 3, Quasar Framework, TypeScript, PWA          |
| **API Gateway**            | .NET 8 Minimal API, YARP (Yet Another Reverse Proxy) |
| **Business Logic Service** | .NET 8 Minimal API, Entity Framework Core            |
| **Location Service**       | .NET 8 Minimal API, EF Core + NetTopologySuite       |
| **Business Database**      | PostgreSQL 16                                        |
| **Location Database**      | PostgreSQL 16 + PostGIS 3.4                          |
| **Cache**                  | Redis 7                                              |
| **Containerization**       | Docker, Docker Compose                               |
| **Location Data Source**   | TERYT (Polish government registry)                   |

---

## Architecture Diagram

```mermaid
flowchart TB
    subgraph Frontend
        FE[Vue.js + Quasar PWA<br/>localhost:9000]
    end

    subgraph Gateway["API Gateway (gateway-network)"]
        GW[".NET 8 + YARP<br/>localhost:5000"]
    end

    subgraph BusinessService["Business Logic Service"]
        BL_API[".NET 8 Minimal API<br/><br/>• Merchants management<br/>• Buyer requests<br/>• Order matching<br/>• Notifications"]
    end

    subgraph LocationService["Location Service"]
        LOC_API[".NET 8 Minimal API<br/><br/>• Postal code lookup<br/>• City search<br/>• Radius-based queries<br/>• Geospatial calculations"]
    end

    subgraph BusinessDB["Business Database"]
        BL_DB[(PostgreSQL 16<br/><br/>• merchants<br/>• buyers<br/>• products<br/>• orders)]
    end

    subgraph LocationDB["Location Database"]
        LOC_DB[(PostgreSQL 16<br/>+ PostGIS 3.4<br/><br/>• polish_cities<br/>• postal_codes<br/>• voivodeships)]
        REDIS[(Redis 7<br/><br/>• postal:code TTL:7d<br/>• city:id TTL:7d)]
    end

    FE -->|HTTP| GW
    GW -->|"/api/business/*"| BL_API
    GW -->|"/api/locations/*"| LOC_API
    BL_API --> BL_DB
    LOC_API --> LOC_DB
    LOC_API --> REDIS
```

## Docker Networks Diagram

```mermaid
flowchart TB
    subgraph gateway-network
        GW[api-gateway]
        BL_API[business-logic-api]
        LOC_API[location-api]
    end

    subgraph business-network
        BL_API2[business-logic-api]
        BL_DB[(business-db<br/>PostgreSQL)]
    end

    subgraph location-network
        LOC_API2[location-api]
        LOC_DB[(location-db<br/>PostGIS)]
        REDIS[(redis)]
    end

    GW --> BL_API
    GW --> LOC_API
    BL_API2 --> BL_DB
    LOC_API2 --> LOC_DB
    LOC_API2 --> REDIS

    BL_API -.->|same container| BL_API2
    LOC_API -.->|same container| LOC_API2
```

## Sequence Diagram - Location Search Flow

```mermaid
sequenceDiagram
    participant FE as Frontend
    participant GW as API Gateway
    participant LOC as Location Service
    participant REDIS as Redis Cache
    participant DB as PostGIS

    FE->>GW: GET /api/locations/postal-codes/32-400/cities
    GW->>LOC: GET /postal-codes/32-400/cities
    LOC->>REDIS: GET postal:32-400

    alt Cache Hit
        REDIS-->>LOC: [cities data]
    else Cache Miss
        REDIS-->>LOC: null
        LOC->>DB: SELECT * FROM cities WHERE postal_code = '32-400'
        DB-->>LOC: [cities data]
        LOC->>REDIS: SET postal:32-400 (TTL: 7d)
    end

    LOC-->>GW: [cities response]
    GW-->>FE: [cities response]
```

## Sequence Diagram - Nearby Merchants Search

```mermaid
sequenceDiagram
    participant FE as Frontend
    participant GW as API Gateway
    participant LOC as Location Service
    participant DB as PostGIS

    FE->>GW: POST /api/locations/nearby
    Note right of FE: { lat: 49.83, lon: 19.94, radiusKm: 50 }

    GW->>LOC: POST /nearby
    LOC->>DB: SELECT * FROM cities<br/>WHERE ST_DWithin(location, point, 50km)
    DB-->>LOC: [cities within radius + distances]
    LOC-->>GW: [nearby cities response]
    GW-->>FE: [nearby cities response]
```

---
