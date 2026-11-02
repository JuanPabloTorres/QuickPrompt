#!/bin/sh
# Synthetic transport checks only. Run in a disposable container without network or host secrets.
set -eu
test "$(id -u)" -ne 0
test -s /app/QuickPrompt.Cloud.dll
test -s /app/wwwroot/index.html
test -z "$(find /app -type f \( -name '*.pem' -o -name '*.pfx' \) -print)"
printf 'PASS runtime-nonroot-web-backend-no-key-files\n'
fixture="$(mktemp -d /tmp/quickprompt-entrypoint-check.XXXXXX)"
trap 'rm -rf "$fixture"' EXIT
cp "${ENTRYPOINT_SOURCE:-/input/entrypoint.sh}" "$fixture/entrypoint.sh"
mkdir "$fixture/bin" "$fixture/mounted"
printf 'synthetic-signing-input' > "$fixture/mounted/signing.pem"
printf 'synthetic-encryption-input' > "$fixture/mounted/encryption.pem"
printf 'synthetic-certificate-input' > "$fixture/certificate"
printf 'synthetic-previous-input' > "$fixture/previous"
signing="$(base64 < "$fixture/mounted/signing.pem" | tr -d '\n')"
encryption="$(base64 < "$fixture/mounted/encryption.pem" | tr -d '\n')"
certificate="$(base64 < "$fixture/certificate" | tr -d '\n')"
previous="$(base64 < "$fixture/previous" | tr -d '\n')"
cat > "$fixture/bin/dotnet" <<'STUB'
#!/bin/sh
set -eu
# Record entry before assertions: invalid configuration must not even start the host.
printf x >> "$TEST_MARKER"
test "$1" = /app/QuickPrompt.Cloud.dll
test "$QuickPrompt__Hosting__ListenUrl" = "$TEST_LISTENER"
for name in QUICKPROMPT_KEY_RING_CERTIFICATE_BASE64 QUICKPROMPT_KEY_RING_PREVIOUS_CERTIFICATE_0_BASE64 QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64 QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64; do
  if printenv "$name" >/dev/null; then exit 31; fi
done
if [ "${TEST_KEYS:-none}" != none ]; then
  cmp "$TEST_FIXTURE/mounted/signing.pem" "$QuickPrompt__Delegation__KeyDirectory/signing.pem"
  cmp "$TEST_FIXTURE/mounted/encryption.pem" "$QuickPrompt__Delegation__KeyDirectory/encryption.pem"
  if [ "$TEST_KEYS" = materialized ]; then
    test "$(stat -c %a "$QuickPrompt__Delegation__KeyDirectory")" = 700
    test "$(stat -c %a "$QuickPrompt__Delegation__KeyDirectory/signing.pem")" = 600
    test "$(stat -c %a "$QuickPrompt__Delegation__KeyDirectory/encryption.pem")" = 600
  fi
fi
if [ "${TEST_CERT:-none}" = materialized ]; then
  cmp "$TEST_FIXTURE/certificate" "$QuickPrompt__Hosting__KeyRingCertificatePath"
  test "$(stat -c %a "$QuickPrompt__Hosting__KeyRingCertificatePath")" = 600
fi
if [ "${TEST_PREVIOUS:-none}" = materialized ]; then
  cmp "$TEST_FIXTURE/previous" "$QuickPrompt__Hosting__PreviousKeyRingCertificates__0__Path"
fi
STUB
chmod 700 "$fixture/bin/dotnet"
passed=0
failed=0
check() {
  expected="$1"; name="$2"; shift 2
  marker="$fixture/$name.marker"; output="$fixture/$name.output"
  code=0
  env -i PATH="$fixture/bin:/usr/bin:/bin" TEST_FIXTURE="$fixture" TEST_MARKER="$marker" \
    TEST_LISTENER=http://0.0.0.0:10000 "$@" /bin/sh "$fixture/entrypoint.sh" > "$output" 2>&1 || code=$?
  actual=FAIL
  if [ "$expected" = pass ] && [ "$code" -eq 0 ] && [ -f "$marker" ] && [ "$(wc -c < "$marker")" -eq 1 ]; then actual=PASS; fi
  if [ "$expected" = refuse ] && [ "$code" -ne 0 ] && [ ! -f "$marker" ] \
      && grep -Eq 'Invalid HTTP listener port|private key-ring certificate could not be loaded|private host key file could not be loaded|delegation key configuration' "$output"; then actual=PASS; fi
  # Diagnostics must never echo the input material, even on decoder/configuration failures.
  for secret in "$signing" "$encryption" "$certificate" synthetic-signing-input synthetic-encryption-input; do
    if grep -Fq -- "$secret" "$output"; then actual=FAIL; fi
  done
  printf '%s %s exit=%s\n' "$actual" "$name" "$code"
  if [ "$actual" = PASS ]; then passed=$((passed + 1)); else failed=$((failed + 1)); fi
}

check pass default-listener
check pass provider-port PORT=12000 TEST_LISTENER=http://0.0.0.0:12000
check pass explicit-listener PORT=12000 QuickPrompt__Hosting__ListenUrl=http://127.0.0.1:12001 TEST_LISTENER=http://127.0.0.1:12001
check pass mounted-keys QuickPrompt__Delegation__KeyDirectory="$fixture/mounted" TEST_KEYS=mounted
check pass materialized-certificate QUICKPROMPT_KEY_RING_CERTIFICATE_BASE64="$certificate" TEST_CERT=materialized
check pass materialized-previous QUICKPROMPT_KEY_RING_CERTIFICATE_BASE64="$certificate" QUICKPROMPT_KEY_RING_PREVIOUS_CERTIFICATE_0_BASE64="$previous" TEST_CERT=materialized TEST_PREVIOUS=materialized
check pass materialized-keys QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64="$signing" QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64="$encryption" TEST_KEYS=materialized
check pass keys-and-certificate QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64="$signing" QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64="$encryption" QUICKPROMPT_KEY_RING_CERTIFICATE_BASE64="$certificate" TEST_KEYS=materialized TEST_CERT=materialized
check refuse missing-encryption QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64="$signing"
check refuse missing-signing QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64="$encryption"
check refuse empty-signing QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64= QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64="$encryption"
check refuse empty-encryption QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64="$signing" QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64=
check refuse mixed-key-sources QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64="$signing" QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64="$encryption" QuickPrompt__Delegation__KeyDirectory="$fixture/mounted"
check refuse invalid-signing QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64='!!invalid!!' QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64="$encryption"
check refuse invalid-encryption QUICKPROMPT_DELEGATION_SIGNING_KEY_BASE64="$signing" QUICKPROMPT_DELEGATION_ENCRYPTION_KEY_BASE64='!!invalid!!'
check refuse invalid-certificate QUICKPROMPT_KEY_RING_CERTIFICATE_BASE64='!!invalid!!'
check refuse invalid-port PORT=not-a-number
check refuse privileged-port PORT=1023
check refuse out-of-range-port PORT=65536
printf 'TOTAL passed=%s failed=%s\n' "$passed" "$failed"
test "$failed" -eq 0
