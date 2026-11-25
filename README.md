# Roam

Roam is an open-source, non-profit, remote human-guided exploration platform. It allows anyone (requesters) to explore legally accessible places in real time through the perspective of a volunteer physically present at the location.

## Core Principles
- **Open and Free:** Not a paid tour marketplace. Built for exploration, accessibility, and human connection.
- **Volunteer-Led:** Volunteers decide where they go and accept requests based on their availability. Requesters never control a volunteer's physical movement.
- **Privacy-First:** Requester camera is off by default. Sessions are live and never recorded on Roam servers. Precise GPS locations of volunteers are never exposed to requesters.
- **Safe and Secure:** Robust moderation, age restrictions, and reporting tools protect all participants.

## Technology Stack
- **Backend:** C# / .NET 10 / ASP.NET Core (Modular Monolith architecture)
- **Database:** PostgreSQL + PostGIS (Geospatial queries)
- **Real-Time:** WebRTC (Live Video/Audio) + SignalR (Signaling & Text Chat)
- **Clients:** 
  - Cross-platform Native: .NET MAUI (iOS, Android, Windows, macOS)
  - Web: React + TypeScript + Vite + MapLibre

## Getting Started
Detailed setup instructions for the backend services (PostgreSQL, Redis, coturn) and clients can be found in the documentation.

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose (for PostgreSQL/PostGIS, Redis, and coturn)
- Node.js & npm (for the React Web Client)

## Publishing
To put Roam online for free on your own domain (Oracle Cloud free server or your own PC, plus a
Cloudflare Tunnel), follow the step-by-step guide in [docs/PUBLISHING.md](docs/PUBLISHING.md).
The production stack lives in [`deploy/`](deploy): `setup.sh` generates the settings and secrets,
`docker compose up -d --build` starts it, and `backup.sh` / `restore.sh` / `update.sh` keep it running.

## License
This project is licensed under the MIT License - see the LICENSE file for details.