#!/bin/sh
set -eu

# Render supplies PORT. This listener is not the public identity; AccountHosting validates both independently.
cloud_port="${PORT:-10000}"
case "$cloud_port" in ''|*[!0-9]*) echo 'Invalid HTTP listener port.' >&2; exit 1 ;; esac
if [ "$cloud_port" -lt 1024 ] || [ "$cloud_port" -gt 65535 ]; then echo 'Invalid HTTP listener port.' >&2; exit 1; fi
export QuickPrompt__Hosting__ListenUrl="${QuickPrompt__Hosting__ListenUrl:-http://0.0.0.0:$cloud_port}"

# Secret-file systems can carry PEM directly. Binary PFX may instead be supplied as private base64 environment values.
cloud_secret_dir=''
load_certificate() {
  cloud_encoded="$(printenv "$1" || true)"
  if [ -z "$cloud_encoded" ]; then return; fi
  umask 077
  if [ -z "$cloud_secret_dir" ]; then cloud_secret_dir="$(mktemp -d /tmp/quickprompt-host-keys.XXXXXX)"; fi
  if ! printf '%s' "$cloud_encoded" | base64 --decode > "$cloud_secret_dir/$2.pfx"; then
    echo 'The private key-ring certificate could not be loaded.' >&2
    exit 1
  fi
  export "$3=$cloud_secret_dir/$2.pfx"
  unset "$1"
  unset cloud_encoded
}
load_certificate QUICKPROMPT_KEY_RING_CERTIFICATE_BASE64 key-ring QuickPrompt__Hosting__KeyRingCertificatePath
for cloud_previous_index in 0 1 2 3; do
  load_certificate "QUICKPROMPT_KEY_RING_PREVIOUS_CERTIFICATE_${cloud_previous_index}_BASE64" "previous-$cloud_previous_index" "QuickPrompt__Hosting__PreviousKeyRingCertificates__${cloud_previous_index}__Path"
done

exec dotnet /app/QuickPrompt.Cloud.dll
