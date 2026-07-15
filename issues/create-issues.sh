#!/usr/bin/env bash
# Creates the Cache Cow v1 epic and all sub-issues on GitHub via `gh issue create`.
#
# Usage:
#   ./issues/create-issues.sh            # dry run: prints what would be created
#   ./issues/create-issues.sh --create   # actually creates the issues
#
# Behavior:
#   1. Ensures labels exist (epic, blocked, P1, P2).
#   2. Creates every NNN-*.md sub-issue (001..100) in order; title = first line's "# " heading,
#      body = rest of file. Labels derived from the file's Priority metadata and BLOCKED marker.
#   3. Builds the epic task list from the created issue numbers, substitutes it into
#      000-epic-cache-cow-v1.md at the <!-- SUBISSUE-TASKLIST --> marker, and creates the epic.
#
# Resume-safe: issues whose exact title already exists on the repo are skipped (their existing
# number is reused for the epic task list), so the script can be re-run after a rate-limit or
# network failure without creating duplicates. If the epic already exists, its body is updated.
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CREATE=false
[[ "${1:-}" == "--create" ]] && CREATE=true

EXISTING="$(mktemp)"   # lines of "<number>\t<title>"
TASKLIST_FILE="$(mktemp)"
trap 'rm -f "$EXISTING" "$TASKLIST_FILE"' EXIT

if $CREATE; then
  gh label create epic    --color 6f42c1 --description "Parent tracking issue" --force
  gh label create blocked --color d73a4a --description "Blocked on an open decision (ARCHITECTURE.md Known unknowns)" --force
  gh label create P1      --color b60205 --description "Priority 1 requirement" --force
  gh label create P2      --color fbca04 --description "Priority 2 requirement" --force
  gh issue list --state all --limit 500 --json number,title \
    --jq '.[] | "\(.number)\t\(.title)"' > "$EXISTING"
fi

existing_number() { # $1 = title -> prints number or nothing
  awk -F'\t' -v t="$1" '$2 == t { print $1; exit }' "$EXISTING"
}

for f in "$DIR"/[0-9][0-9][0-9]-*.md; do
  base="$(basename "$f")"
  [[ "$base" == 000-* ]] && continue

  title="$(head -1 "$f" | sed 's/^# //')"
  labels=""
  prio="$(grep -m1 -E '^\- \*\*Priority\*\*' "$f" || true)"
  case "$prio" in
    *Critical*|*High*) labels="P1" ;;
    *Medium*|*Low*)    labels="P2" ;;
  esac
  if grep -q '^> \*\*BLOCKED' "$f"; then
    labels="${labels:+$labels,}blocked"
  fi

  if $CREATE; then
    num="$(existing_number "$title" || true)"
    if [[ -n "$num" ]]; then
      echo "exists  #${num}: ${title} (skipped)"
    else
      body="$(mktemp)"
      tail -n +2 "$f" > "$body"
      url="$(gh issue create --title "$title" --body-file "$body" ${labels:+--label "$labels"})"
      rm -f "$body"
      num="${url##*/}"
      echo "created #${num}: ${title} [${labels:-none}]"
      sleep 1   # stay clear of GitHub secondary rate limits on content creation
    fi
    echo "- [ ] #${num} — ${title}" >> "$TASKLIST_FILE"
  else
    echo "- [ ] ${title}" >> "$TASKLIST_FILE"
    echo "would create: ${title} [${labels:-none}]"
  fi
done

EPIC_SRC="$DIR/000-epic-cache-cow-v1.md"
epic_title="$(head -1 "$EPIC_SRC" | sed 's/^# //')"
epic_body="$(mktemp)"
awk -v tasklist="$TASKLIST_FILE" '
  /<!-- SUBISSUE-TASKLIST -->/ { while ((getline line < tasklist) > 0) print line; next }
  NR > 1 { print }
' "$EPIC_SRC" > "$epic_body"

if $CREATE; then
  epic_num="$(existing_number "$epic_title" || true)"
  if [[ -n "$epic_num" ]]; then
    gh issue edit "$epic_num" --body-file "$epic_body"
    echo "updated epic #${epic_num}: ${epic_title}"
  else
    epic_url="$(gh issue create --title "$epic_title" --body-file "$epic_body" --label epic,P1)"
    echo "created epic: $epic_url"
  fi
else
  echo
  echo "would create epic: ${epic_title} with task list:"
  cat "$TASKLIST_FILE"
fi
rm -f "$epic_body"
