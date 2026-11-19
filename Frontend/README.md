# Orbit Frontend – Angular 22.2.7

The frontend has been upgraded from Angular 12 to Angular 22.2.7 while preserving the existing screens, NgModule organization, API contracts, and visual design.

## Requirements

Angular 22.2.x compatibility:

- Node.js `^22.22.3 || ^24.15.0 || ^26.0.0`
- TypeScript `>=6.0.0 <6.1.0`
- RxJS `^6.5.3 || ^7.4.0`

This project pins Angular framework packages, Angular CLI, Angular Material, CDK, and `@angular/build` to `22.2.7`, TypeScript to `~6.0.0`, and RxJS to `^7.8.2`.

Node.js `24.13.1` is not new enough. Install Node.js `24.15.0` or later in the 24.x line, or Node.js `22.22.3` or later in the 22.x line.

Official compatibility table: https://angular.dev/reference/versions

## Install and run

From this directory:

```powershell
node --version
npm --version
npm install
npm start
```

The app starts at `http://localhost:4200` and calls the backend at the URL configured in `src/environments/environment.ts`.

Production build:

```powershell
npm run build
```

## Configuration

- `src/environments/environment.ts`: development API URL.
- `src/environments/environment.prod.ts`: production API URL.
- `angular.json`: Angular CLI build and serve configuration.
- `package.json`: pinned framework and build-tool versions.
- `.nvmrc` / `.node-version`: recommended Node.js version.

## Upgrade notes

- The application deliberately keeps its existing NgModule-based structure to minimize unrelated changes.
- The old Webpack browser builder was replaced with Angular's current `@angular/build:application` builder.
- The obsolete `relativeLinkResolution` router option was removed.
- Angular Material theming was migrated to the Material 3 Sass API. The application retains its custom dark/light theme switch.
- `zone.js` is loaded through the Angular CLI `polyfills` option.
