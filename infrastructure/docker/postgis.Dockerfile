# PostgreSQL 15 + PostGIS for both x86-64 and ARM64 servers (e.g. Oracle Cloud's free Ampere VMs).
# The official postgis/postgis images are x86-64 only; the official postgres image is multi-arch and
# already has the PostgreSQL apt repository, which ships PostGIS for both architectures.
FROM postgres:15-bookworm

RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-15-postgis-3 postgresql-15-postgis-3-scripts \
    && rm -rf /var/lib/apt/lists/*

# The API's EF Core migrations run CREATE EXTENSION postgis; nothing else is needed here.
