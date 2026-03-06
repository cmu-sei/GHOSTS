# GHOSTS Frontend (Ghosts.Frontend)

A modern Angular 20 application for managing the GHOSTS framework.

## Project Overview

This project consolidates the UI from both `ghosts.api` (MVC views) and `ghosts.ui` (Next.js) into a single, modern Angular application. The existing `ghosts.api` project will be eventually refactored to be a pure REST API and `ghosts.ui` is expected to be retired.

## Technology Stack

- **Angular**: 20.3.7
- **Angular Material**: 20.2.10 (Material Design components)
- **TypeScript**: 5.x with strict type checking
- **RxJS**: For reactive programming
- **Signals**: For state management (Angular's new reactive primitive)

## Architecture

### Folder Structure

```
src/app/
├── core/                      # Core application services and models
│   ├── models/               # TypeScript interfaces and types
│   │   ├── machine.model.ts
│   │   ├── machine-group.model.ts
│   │   ├── timeline.model.ts
│   │   ├── npc.model.ts
│   │   └── activity.model.ts
│   ├── services/             # API services
│   │   ├── machine.service.ts
│   │   ├── machine-group.service.ts
│   │   ├── timeline.service.ts
│   │   ├── npc.service.ts
│   │   └── activity.service.ts
│   └── interceptors/         # HTTP interceptors (future)
│
├── shared/                    # Shared components, directives, pipes
│   ├── components/
│   │   └── navigation/       # Main navigation component
│   ├── directives/
│   └── pipes/
│
├── features/                  # Feature modules (lazy-loaded)
│   ├── machines/             # Machine management
│   │   ├── machines.routes.ts
│   │   ├── machines-list/
│   │   ├── machine-detail/   # TODO
│   │   └── machine-form/     # TODO
│   ├── machine-groups/       # Machine group management
│   ├── timelines/            # Timeline management
│   ├── npcs/                 # NPC management
│   ├── animations/           # From ghosts.api Views/Animations
│   ├── activities/           # From ghosts.api Views/ViewActivities
│   ├── relationships/        # From ghosts.api Views/ViewRelationships
│   └── social/               # From ghosts.api Views/ViewSocial
│
├── app.ts                    # Root component
├── app.html                  # Root template
├── app.routes.ts             # Application routing
└── app.config.ts             # Application configuration

environments/
├── environment.ts            # Development environment
└── environment.production.ts # Production environment
```

## Contributing

Follow the patterns established in the existing code:
- Use signals for state management
- Keep components small and focused
- Use Material Design components
- Follow TypeScript strict mode
- Avoid `any` types
- Document public APIs

## License

[Same as GHOSTS project]
