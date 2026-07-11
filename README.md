# 🏭 ManufacturingERP Desktop Solution

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%20WPF-lightgrey.svg)]()
[![Database](https://img.shields.io/badge/Database-SQLite%20%2F%20EF%20Core-green.svg)]()

A comprehensive, production-ready ERP desktop application template designed for small-to-medium manufacturing operations. It features key modules spanning warehouse management, production control, route distribution, van sales, collections, and dual-layer accounting.

---

## 📸 Screenshots

![UI](docs/screenshots/erp1.png)
![UI](docs/screenshots/erp2.png)
![UI](docs/screenshots/erp3.png)
![UI](docs/screenshots/erp4.png)
![UI](docs/screenshots/erp5.png)
![UI](docs/screenshots/erp6.png)
![UI](docs/screenshots/erp7.png)
![UI](docs/screenshots/erp8.png)


---

## 🏗️ Solution Architecture

The solution is built using **Clean Architecture** principles, enforcing separation of concerns, testability, and a clear flow of dependencies.

```mermaid
graph TD
    %% Define Presentation Layer
    subgraph Presentation ["💻 Presentation Layer"]
        Desktop[ManufacturingERP.Desktop]
        WPF[WPF Views & Styles]
        VM[MVVM ViewModels]
    end

    %% Define Application Layer
    subgraph Application ["⚙️ Application Layer"]
        App[ManufacturingERP.Application]
        Services[Analytics, Inventory, Sales, Accounting Services]
        DTOs[Data Transfer Objects]
        Interfaces[Repository & DB Interfaces]
    end

    %% Define Infrastructure Layer
    subgraph Infrastructure ["🗄️ Infrastructure Layer"]
        Infra[ManufacturingERP.Infrastructure]
        DbContext[AppDbContext EF Core]
        Seeder[DbSeeder Seed Data]
        Repos[Repositories]
    end

    %% Define Domain Layer
    subgraph Domain ["📦 Domain Layer"]
        Dom[ManufacturingERP.Domain]
        Entities[Core Entities: Product, Order, Account...]
        Enums[Enums & Common Types]
    end

    %% Dependencies flow
    Desktop --> App
    Desktop --> Infra
    App --> Dom
    Infra --> App
    Infra --> Dom
```

### 🗂️ Layer Breakdown

1. **`ManufacturingERP.Desktop` (WPF UI)**
   * Built on the MVVM (Model-View-ViewModel) pattern using the `CommunityToolkit.Mvvm` package.
   * Utilizes `MaterialDesignThemes` for a modern, sleek dark/light themed interface.
   * Standardizes visual elements with customized styles (e.g. `Styles.xaml`).

2. **`ManufacturingERP.Application` (Services & Logic)**
   * Contains core application logic, services (like [AnalyticsService](file:///c:/Projects/ManufacturingERP/src/ManufacturingERP.Application/Services/AnalyticsService.cs)), and DTOs.
   * Defines interfaces and abstractions to decouple the domain from the underlying persistence layer.

3. **`ManufacturingERP.Domain` (Entities & Abstractions)**
   * Contains core domain models (e.g., `Product`, `Customer`, `ProductionOrder`, `Account`).
   * Expresses business rules, enums, and domain behavior without any external infrastructure dependencies.

4. **`ManufacturingERP.Infrastructure` (Data Persistence)**
   * Implements EF Core with SQLite backend for light-footprint local databases.
   * Configures tables, relations, and indexes in `AppDbContext`.
   * Integrates a seeder (`DbSeeder`) containing mock data for quick developer bootstrapping.

5. **`ManufacturingERP.Shared` (Utilities & Helpers)**
   * Houses project-wide cross-cutting helpers, constants, and utilities.

---
