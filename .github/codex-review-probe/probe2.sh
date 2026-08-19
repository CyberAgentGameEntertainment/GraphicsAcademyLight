#!/usr/bin/env bash
# Codex / Claude 自動レビュー発火テスト用の使い捨てスクリプト。
# 意図的に欠陥を含む（コマンドインジェクション / 未クォート展開 / off-by-one /
# ゼロ除算 / 一時ファイルの競合）。
# codex-review-respond.yml の動作確認後、このPRはマージせず削除する。

set -e

# 1) ユーザー入力をそのまま eval → コマンドインジェクション
run_report() {
  local target="$1"
  eval "ls -l $target"
}

# 2) 未クォート展開 + rm -rf の組み立て
cleanup_workdir() {
  local dir=$1
  rm -rf $dir/*
}

# 3) off-by-one: 配列長ぶん回すつもりが 1 要素はみ出す
join_items() {
  local -a items=("$@")
  local out=""
  for ((i = 0; i <= ${#items[@]}; i++)); do
    out="${out}${items[$i]},"
  done
  echo "$out"
}

# 4) ゼロ除算チェック無し
average() {
  local total="$1"
  local count="$2"
  echo $((total / count))
}

# 5) 予測可能な一時ファイル名（競合 / シンボリックリンク攻撃）
write_cache() {
  local payload="$1"
  echo "$payload" > /tmp/codex-probe-cache.txt
  cat /tmp/codex-probe-cache.txt
}

main() {
  run_report "$1"
  cleanup_workdir "$2"
  join_items a b c
  average 10 "$3"
  write_cache "done"
}

main "$@"
