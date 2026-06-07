#!/usr/bin/env bash
# ============================================================
# JwtLens SampleJwtLensApi — Endpoint Test Script (curl)
# ============================================================
# Usage:
#   chmod +x test-endpoints.sh
#   ./test-endpoints.sh [BASE_URL]
#
# Defaults to http://localhost:5000 if no BASE_URL is provided.
# Requires: curl, bash
# ============================================================

set -euo pipefail

BASE_URL="${1:-http://localhost:5000}"
PASS=0
FAIL=0

# Generate a minimal unsigned JWT for testing (alg:none, sub:testuser)
# Header: {"alg":"none","typ":"JWT"}
# Payload: {"sub":"testuser","iss":"test-script","iat":1700000000}
HEADER=$(echo -n '{"alg":"none","typ":"JWT"}' | base64 -w0 2>/dev/null || echo -n '{"alg":"none","typ":"JWT"}' | base64)
PAYLOAD=$(echo -n '{"sub":"testuser","iss":"test-script","iat":1700000000}' | base64 -w0 2>/dev/null || echo -n '{"sub":"testuser","iss":"test-script","iat":1700000000}' | base64)
# Convert to base64url
HEADER=$(echo -n "$HEADER" | tr '+/' '-_' | tr -d '=')
PAYLOAD=$(echo -n "$PAYLOAD" | tr '+/' '-_' | tr -d '=')
TEST_JWT="${HEADER}.${PAYLOAD}."

echo ""
echo "🔍 JwtLens SampleJwtLensApi — Endpoint Tests"
echo "   Target: $BASE_URL"
echo "   Test JWT: ${TEST_JWT:0:40}..."
echo ""

# Helper: run a test case
test_case() {
    local name="$1"
    local method="$2"
    local endpoint="$3"
    local extra_args="${4:-}"

    echo -n "  Testing: $name ... "
    local url="${BASE_URL}${endpoint}"

    if [ -n "$extra_args" ]; then
        HTTP_CODE=$(curl -s -o /tmp/jwtlens_response.json -w "%{http_code}" -X "$method" $extra_args "$url")
    else
        HTTP_CODE=$(curl -s -o /tmp/jwtlens_response.json -w "%{http_code}" -X "$method" "$url")
    fi

    if [[ "$HTTP_CODE" -ge 200 && "$HTTP_CODE" -lt 300 ]]; then
        echo "✅ PASS (HTTP $HTTP_CODE)"
        PASS=$((PASS + 1))
    else
        echo "❌ FAIL (HTTP $HTTP_CODE)"
        echo "    Response: $(cat /tmp/jwtlens_response.json 2>/dev/null | head -c 200)"
        FAIL=$((FAIL + 1))
    fi
}

# ════════════════════════════════════════════════════════════
# 1. GET /api/test — Simple endpoint (no auth)
# ════════════════════════════════════════════════════════════
echo "━━━ Basic Connectivity ━━━"
test_case "GET /api/test (no auth)" "GET" "/api/test"

# ════════════════════════════════════════════════════════════
# 2. GET /api/test — With ****** (inbound capture)
# ════════════════════════════════════════════════════════════
echo ""
echo "━━━ Inbound JWT Capture ━━━"
test_case "GET /api/test (with ******" "GET" "/api/test" "-H 'Authorization: ******'"

# ════════════════════════════════════════════════════════════
# 3. GET /api/jwt/events — Returns all stored events
# ════════════════════════════════════════════════════════════
echo ""
echo "━━━ Event Store Endpoints ━━━"
test_case "GET /api/jwt/events" "GET" "/api/jwt/events"

# ════════════════════════════════════════════════════════════
# 4. GET /api/jwt/events/count — Returns count info
# ════════════════════════════════════════════════════════════
test_case "GET /api/jwt/events/count" "GET" "/api/jwt/events/count"

# ════════════════════════════════════════════════════════════
# 5. DELETE /api/jwt/events — Clear event store
# ════════════════════════════════════════════════════════════
test_case "DELETE /api/jwt/events" "DELETE" "/api/jwt/events"

# ════════════════════════════════════════════════════════════
# 6. GET /api/jwt/diagnostics — Diagnostics metadata
# ════════════════════════════════════════════════════════════
echo ""
echo "━━━ Diagnostics & Options ━━━"
test_case "GET /api/jwt/diagnostics" "GET" "/api/jwt/diagnostics"

# ════════════════════════════════════════════════════════════
# 7. GET /api/jwt/options — Current JwtLensOptions
# ════════════════════════════════════════════════════════════
test_case "GET /api/jwt/options" "GET" "/api/jwt/options"

# ════════════════════════════════════════════════════════════
# 8. GET /api/outbound-test?token={jwt} — Outbound call
# ════════════════════════════════════════════════════════════
echo ""
echo "━━━ Outbound JWT Capture ━━━"
test_case "GET /api/outbound-test?token=JWT" "GET" "/api/outbound-test?token=${TEST_JWT}"

# ════════════════════════════════════════════════════════════
# Verify events were captured after the above calls
# ════════════════════════════════════════════════════════════
echo ""
echo "━━━ Verification ━━━"
echo -n "  Checking event count after tests ... "
EVENTS_RESPONSE=$(curl -s "${BASE_URL}/api/jwt/events/count")
echo "Response: $EVENTS_RESPONSE"

# ════════════════════════════════════════════════════════════
# Results
# ════════════════════════════════════════════════════════════
echo ""
echo "════════════════════════════════════════"
echo "Results: $PASS passed, $FAIL failed"
echo "════════════════════════════════════════"

if [ "$FAIL" -gt 0 ]; then
    exit 1
fi
