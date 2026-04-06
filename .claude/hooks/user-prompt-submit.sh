#!/usr/bin/env bash
# .claude/hooks/user-prompt-submit.sh
# Runs BEFORE every user prompt — injects sprint context and enforces workflows
# Hook type: UserPromptSubmit (non-blocking)

set -euo pipefail

INPUT=$(cat)
PROMPT=$(echo "$INPUT" | python3 -c \
  "import sys,json; d=json.load(sys.stdin); print(d.get('prompt', ''))" \
  2>/dev/null || echo '')
PROMPT_LC=$(echo "$PROMPT" | tr '[:upper:]' '[:lower:]')
PROJECT_ROOT="/Users/pipat/Documents/MiniProject/Ryla"

# ── 1. Inject active sprint context ─────────────────────────────────────────
# ทุก agent ได้รับ current sprint state โดยอัตโนมัติ
CONTEXT_FILE="$PROJECT_ROOT/.claude/memory/active-context.md"
if [ -f "$CONTEXT_FILE" ] && [ -s "$CONTEXT_FILE" ]; then
  echo "=== Sprint Context (auto-injected from .claude/memory/active-context.md) ==="
  cat "$CONTEXT_FILE"
  echo "============================================================================"
  echo ""
fi

# ── 2. PR intent guard — enforce changelog fragment ──────────────────────────
# ตรวจว่า prompt มีเจตนาสร้าง PR แต่ไม่ได้พูดถึง changelog
if echo "$PROMPT_LC" | grep -qE \
  '(create.*pr|open.*pr|make.*pr|pull.?request|new.*pr|สร้าง.*pr|ทำ.*pr)'; then
  if ! echo "$PROMPT_LC" | grep -qE '(changelog|fragment|skip-changelog)'; then
    echo "⚠️  [Workflow Gate] ตรวจพบเจตนาสร้าง PR"
    echo "   ก่อน PR: สร้าง changelog fragment ก่อนด้วย"
    echo "   Command: make changelog-add"
    echo "   Skip:    ใส่ label 'skip-changelog' ถ้าไม่มี user-facing change"
    echo ""
  fi
fi

# ── 3. New feature — suggest RPI workflow ────────────────────────────────────
# ถ้าขอ implement feature ใหม่โดยไม่ผ่าน architect/RPI
if echo "$PROMPT_LC" | grep -qE \
  '(implement|build|create|add|สร้าง|เพิ่ม|ทำ).*(feature|endpoint|webhook|page|component|adapter|service|flow)'; then
  if ! echo "$PROMPT_LC" | grep -qE \
    '(rpi|research phase|plan phase|architect|orchestrat|design first|วาง.*แผน)'; then
    echo "💡 [Workflow Tip] Feature ใหม่ — แนะนำ RPI workflow:"
    echo "   Phase 1: Research — \"research feasibility of [feature]\""
    echo "   Phase 2: Plan    — \"create architecture plan for [feature]\""
    echo "   Phase 3: Impl    — \"implement [feature] following the plan\""
    echo "   หรือพูดว่า: 'follow RPI workflow for [feature]'"
    echo ""
  fi
fi

# ── 4. AOT reminder for C# changes ──────────────────────────────────────────
# ถ้ากำลังเพิ่ม NuGet package ใหม่
if echo "$PROMPT_LC" | grep -qE '(nuget|dotnet add package|install.*(package|library))'; then
  echo "🔒 [AOT Check] ก่อนเพิ่ม NuGet package:"
  echo "   ตรวจสอบว่ามี IsAotCompatible=true บน NuGet.org ก่อนเสมอ"
  echo "   รัน: make aot-check หลัง add"
  echo ""
fi

exit 0
