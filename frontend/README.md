# BoricuaBite Frontend

React + TypeScript + Tailwind frontend for BoricuaBite.

## Development

Start the ASP.NET API first from the repository root:

```sh
dotnet run --project backend/BoricuaBite.Api --launch-profile https
```

The API should listen on `https://localhost:7144`.

In a second terminal:

```sh
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`.

Vite proxies `/api` requests to the local ASP.NET HTTPS server, so frontend development does not require a separate CORS policy.

## Current screens

- Public restaurant catalog
- Restaurant search and city/open filters
- Public menu viewer
- Account registration and login
- Owner restaurant list
- Restaurant availability toggle
- Menu item creation
- Menu item availability toggle
- Menu item deletion
- Restaurant creation

The original static test UI remains available from the ASP.NET app while this React frontend is developed.
