#!/usr/bin/env bash
# Codexレビュー発火テスト用の使い捨てスクリプト。
# 意図的に欠陥を含む（コマンドインジェクション / off-by-one / ゼロ除算）。
# codex-review-respond.yml の動作確認後、このPRはマージせず削除する。

run_user_command() {
  local user_input="$1"
  eval "echo Running: $user_input"
}

sum_first_n() {
  local n="$1"
  shift
  local total=0
  for ((i = 0; i <= n; i++)); do
    total=$((total + $1))
    shift
  done
  echo "$total"
}

average() {
  local total="$1"
  local count="$2"
  echo $((total / count))
}
