#!/bin/sh
set -eu

# The provider supplies PORT. AccountHosting validates the listener and public identity independently.
cloud_port="${PORT:-10000}"
case "$cloud_port" in ''|*[!0-9]*) echo 'Invalid HTTP listener port.' >&2; exit 1 ;; esac
if [ "$cloud_port" -lt 1024 ] || [ "$cloud_port" -gt 65535 ]; then echo 'Invalid HTTP listener port.' >&2; exit 1; fi
export QuickPrompt__Hosting__ListenUrl="${QuickPrompt__Hosting__ListenUrl:-http://0.0.0.0:$cloud_port}"

# Mounted delegation files and encoded delegation inputs are mutually exclusive.
# Never generate credentials on startup: replacement would invalidate existing grants and sessions.
cloud_encoded_delegation=false
if [ "${QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64+x}" = x ] || [ "${QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64+x}" = x ]; then
  if [ -z "${QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64:-}" ] || [ -z "${QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64:-}" ] \
      || [ -n "${QuickPrompt__Delegation__KeyDirectory:-}" ]; then
    echo 'Invalid private delegation key configuration: select mounted files or one complete encoded pair.' >&2
    exit 1
  fi
  cloud_encoded_delegation=true
fi

# Materialize only operator-supplied bytes; .NET remains responsible for cryptographic validation.
cloud_secret_dir=''
load_secret_file() {
  cloud_encoded="$(printenv "$1" || true)"
  if [ -z "$cloud_encoded" ]; then return; fi
  umask 077
  if [ -z "$cloud_secret_dir" ]; then cloud_secret_dir="$(mktemp -d /tmp/quickprompt-host-keys.XXXXXX)"; fi
  if ! printf '%s' "$cloud_encoded" | base64 --decode > "$cloud_secret_dir/$2" 2>/dev/null; then
    echo 'A private host key file could not be loaded.' >&2
    exit 1
  fi
  if [ -n "$3" ]; then export "$3=$cloud_secret_dir/$2"; fi
  unset "$1"
  unset cloud_encoded
}
load_secret_file QUICKPROMPT_KEY_RING_CERTIFICATE_BASE64 key-ring.pfx QuickPrompt__Hosting__KeyRingCertificatePath
for cloud_previous_index in 0 1 2 3; do
  load_secret_file "QUICKPROMPT_KEY_RING_PREVIOUS_CERTIFICATE_${cloud_previous_index}_BASE64" "previous-$cloud_previous_index.pfx" "QuickPrompt__Hosting__PreviousKeyRingCertificates__${cloud_previous_index}__Path"
done
if [ "$cloud_encoded_delegation" = true ]; then
  if [ -z "$cloud_secret_dir" ]; then umask 077; cloud_secret_dir="$(mktemp -d /tmp/quickprompt-host-keys.XXXXXX)"; fi
  mkdir -m 700 "$cloud_secret_dir/delegation"
  load_secret_file QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64 delegation/signing.pem ''
  load_secret_file QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64 delegation/encryption.pem ''
  export QuickPrompt__Delegation__KeyDirectory="$cloud_secret_dir/delegation"
fi

exec dotnet /app/QuickPrompt.Cloud.dll
