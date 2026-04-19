#!/usr/bin/env bash
# .claude/hooks/pre-pr-gate.sh
# PreToolUse hook — block git push / gh pr create ถ้ายังไม่ผ่าน quality-auditor
# Exit 2 = BLOCK, Exit 0 = ALLOW

set -euo pipefail

INPUT=$(cat)
CMD=$(echo "$INPUT" | python3 -c \
  "import sys,json; d=json.load(sys.stdin); print(d.get('tool_input', d).get('command', ''))" \
  2>/dev/null || echo '')

# Only gate git push and gh pr create
if ! echo "$CMD" | grep -qE '^(git push|gh pr create)'; then
  exit 0
fi

PROJECT_ROOT="/Users/pipat/Documents/MiniProject/Ryla"
REVIEW_FILE="$PROJECT_ROOT/.claude/review-passed.json"
BRANCH=$(cd "$PROJECT_ROOT" && git branch --show-current 2>/dev/null || echo '')

# Skip gate for develop and main (only gate feature/fix/chore branches)
if echo "$BRANCH" | grep -qE '^(main|develop|release/)'; then
  exit 0
fi

# Check 1: review-passed.json exists
if [ ! -f "$REVIEW_FILE" ]; then
  echo "BLOCKED: quality-auditor review ยังไม่ผ่าน" >&2
  echo "  ไม่พบ .claude/review-passed.json" >&2
  echo "  ต้องรัน quality-auditor-agent ก่อน push/PR" >&2
  exit 2
fi

# Check 2: reviewed SHA matches current HEAD
REVIEWED_SHA=$(python3 -c \
  "import json; d=json.load(open('$REVIEW_FILE')); print(d.get('last_reviewed_sha', ''))" \
  2>/dev/null || echo '')
CURRENT_SHA=$(cd "$PROJECT_ROOT" && git rev-parse HEAD 2>/dev/null || echo '')

if [ "$REVIEWED_SHA" != "$CURRENT_SHA" ]; then
  echo "BLOCKED: มี commit ใหม่หลัง quality-auditor review" >&2
  echo "  Reviewed SHA:  $REVIEWED_SHA" >&2
  echo "  Current HEAD:  $CURRENT_SHA" >&2
  echo "  ต้อง re-run quality-auditor-agent ก่อน push/PR" >&2
  exit 2
fi

# Check 3: all 4 dimensions passed
FAILED=$(python3 -c "
import json
d = json.load(open('$REVIEW_FILE'))
dims = d.get('dimensions', {})
required = ['security', 'performance', 'architecture', 'test_coverage']
failed = [r for r in required if dims.get(r) != 'PASS']
print(','.join(failed))
" 2>/dev/null || echo 'unknown')

if [ -n "$FAILED" ] && [ "$FAILED" != "" ]; then
  echo "BLOCKED: quality-auditor บาง dimension ยังไม่ผ่าน: $FAILED" >&2
  echo "  ต้องแก้ไขแล้ว re-run quality-auditor-agent" >&2
  exit 2
fi

# All checks pass
exit 0
