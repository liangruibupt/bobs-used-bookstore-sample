#!/usr/bin/env bash
#
# Self-contained verification of the SQL Server -> PostgreSQL code transformation.
# Requires only Docker. Spins up PostgreSQL, builds the converted Bookstore.Web
# and this harness with the .NET 8 SDK, runs the harness against PostgreSQL, then
# tears everything down.
#
# Usage:  ./run-verify.sh          (set KEEP=1 to leave the postgres container running)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

NET=bobsverify
PG=bobs-pg
SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:8.0
PG_IMAGE=postgres:16-alpine

cleanup() {
  if [ "${KEEP:-0}" != "1" ]; then
    docker rm -f "$PG" >/dev/null 2>&1 || true
    docker network rm "$NET" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "=== setup: network + postgres ==="
docker network create "$NET" >/dev/null 2>&1 || true
docker rm -f "$PG" >/dev/null 2>&1 || true
docker run -d --name "$PG" --network "$NET" \
  -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=BobsUsedBookStore \
  -p 5433:5432 "$PG_IMAGE" >/dev/null

echo "=== waiting for postgres ==="
for i in $(seq 1 60); do
  docker exec "$PG" pg_isready -U postgres >/dev/null 2>&1 && { echo "ready after ${i}s"; break; }
  sleep 1
done

echo "=== build converted app + run verification harness ==="
docker run --rm --network "$NET" \
  -v "$REPO_ROOT":/repo \
  -e PGCONN="Host=${PG};Port=5432;Database=BobsUsedBookStore;Username=postgres;Password=postgres" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
  -w /repo "$SDK_IMAGE" \
  bash -c '
    set -e
    echo "--- build Bookstore.Web (Release) ---"
    dotnet build /repo/app/Bookstore.Web/Bookstore.Web.csproj -c Release --nologo -v minimal
    echo "--- run db-verification harness ---"
    dotnet run --project /repo/db-verification/bobs-verify.csproj -c Release --nologo -v minimal
  '
