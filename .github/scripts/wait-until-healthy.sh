#!/usr/bin/env bash
# Waits until a health endpoint answers "Healthy", for up to 5 minutes: a new deployment can take that long to start.
#   bash .github/scripts/wait-until-healthy.sh https://<host>/health
set -euo pipefail

url=$1
attempts=30

for attempt in $(seq "$attempts"); do
  if [ "$(curl --silent --max-time 10 "$url")" = "Healthy" ]; then
    echo "$url is healthy."
    exit 0
  fi
  echo "Not healthy yet ($attempt of $attempts)."
  sleep 10
done

echo "::error::$url wasn't healthy after 5 minutes."
exit 1
