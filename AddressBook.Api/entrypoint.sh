#!/usr/bin/env sh
set -eu

SEED_DB="/seed/addressbook.db"
DATA_DIR="/app/data"
RUN_DB="$DATA_DIR/addressbook.db"

# Ensure data directory exists
mkdir -p "$DATA_DIR"

# Seed the DB only if it doesn't exist in the data directory
if [ ! -f "$RUN_DB" ] && [ -f "$SEED_DB" ]; then
  echo "Seeding database to $RUN_DB"
  cp "$SEED_DB" "$RUN_DB"
fi

# Display where the API will use the database from
if [ -f "$RUN_DB" ]; then
  echo "Using database at $RUN_DB"
else
  echo "No pre-seeded database found; a new DB will be created at $RUN_DB when the app starts."
fi

# Exec the .NET app
exec dotnet AddressBook.Api.dll
