# OpenAdoration

Open-source worship presentation software for churches. A free alternative to EasyWorship, built with .NET and WPF.

## What it does

OpenAdoration lets a church operator manage and project content during a service from a single desktop application:

- **Songs** — build and manage a song library with structured lyrics (verse, chorus, bridge)
- **Bible** — browse and search Bible translations, send verses to the projector
- **Service Schedule** — build a setlist before the service, navigate through it live
- **Media** — display images and videos on the projector
- **Themes** — control fonts, colors, and backgrounds for projected content
- **Projection** — full-screen display on a secondary monitor with instant slide switching

## Requirements

- Windows 10 or later
- .NET 10 Runtime
- A secondary monitor or projector (optional — app works on a single screen)

## Tech stack

| Layer | Technology |
|---|---|
| UI | WPF (.NET 10) |
| Architecture | Clean Architecture — Domain / Application / Infrastructure / WPF |
| Database | SQLite (via Entity Framework Core 9) |
| MVVM | CommunityToolkit.Mvvm |
| Logging | Serilog — rolling daily file logs |

## Project structure

```
OpenAdoration.Domain/           Core entities (Song, BibleVerse, Theme, ScheduleItem...)
OpenAdoration.Application/      Service interfaces, application services, Slide model
OpenAdoration.Infrastructure/   EF Core, SQLite, repositories, Serilog configuration
OpenAdoration.WPF/              WPF application — windows, views, view models
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 17.8+ or JetBrains Rider

### Run

```bash
# Clone the repo
git clone https://github.com/g0elles/OpenAdoration.git
cd OpenAdoration

# Create the initial database migration (first time only)
dotnet ef migrations add InitialCreate \
  --project OpenAdoration.Infrastructure \
  --startup-project OpenAdoration.WPF

# Run
dotnet run --project OpenAdoration.WPF
```

The database is created automatically on first launch at:
```
%LocalAppData%\OpenAdoration\openadoration.db
```

Logs are written daily to:
```
%LocalAppData%\OpenAdoration\logs\openadoration-YYYYMMDD.log
```

## Status

Active development — MVP in progress.

| Feature | Status |
|---|---|
| Domain layer | Done |
| Infrastructure / database | Done |
| Application services | Done |
| WPF shell + projection window | Done |
| Song management | Done |
| Bible browser | Done |
| Themes | Done |
| Service schedule | Planned |
| Media | Planned |

## Contributing

Contributions are welcome. Open an issue to discuss what you'd like to change before submitting a pull request.

## License

MIT
