#!/usr/bin/env bash
# .claude/hooks/create-review-stamp.sh
# เรียกโดย quality-auditor-agent เมื่อ review ผ่านทุก dimension
# Usage: .claude/hooks/create-review-stamp.sh

set -euo pipefail

PROJECT_ROOT="/Users/pipat/Documents/MiniProject/Ryla"
REVIEW_FILE="$PROJECT_ROOT/.claude/review-passed.json"
CURRENT_SHA=$(cd "$PROJECT_ROOT" && git rev-parse HEAD)
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

cat > "$REVIEW_FILE" <<EOF
{
  "passed_at": "$TIMESTAMP",
  "last_reviewed_sha": "$CURRENT_SHA",
  "dimensions": {
    "security": "PASS",
    "performance": "PASS",
    "architecture": "PASS",
    "test_coverage": "PASS"
  }
}
EOF

echo "Review stamp created: $REVIEW_FILE"
echo "  SHA: $CURRENT_SHA"
echo "  Time: $TIMESTAMP"
