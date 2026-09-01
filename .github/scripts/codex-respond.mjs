#!/usr/bin/env node
// Codex（chatgpt-codex-connector[bot]）のコードレビュー指摘ごとに、Claude Code CLI
// （OAuth サブスク認証）へ問い合わせて「どう直すか（suggestion / 手動差分）」または
// 「対応見送り理由」を生成する純粋ロジック。
// (SIRIUS本体の同名スクリプトを参考に移植。ロジックは変えていない)
//
// 認証方式:
//   workflow 側で env CLAUDE_CODE_OAUTH_TOKEN（`claude setup-token` で発行した長期トークンを
//   渡しておけば、CLI がそこから自動認証する。本スクリプトは `claude -p ... --output-format json`
//   を spawn するだけ。従量課金の ANTHROPIC_API_KEY は使わない（サブスク OAuth トークンで動かす）。
//
// 責務分割:
//   - 本スクリプト: 入力 JSON を受け取り CLI で各指摘を分類し、返信 markdown を組み立てて
//     result JSON を stdout に出すだけ。GitHub 操作（コメント取得 / 返信 post）はしない。
//   - 外部プロセスは `claude` のみ。spawnSync を argv 配列 + shell:false で呼ぶ（コマンドインジェクション防止）。
//   - `.meta` 紐づけの指摘は LLM を介さず defer 固定（Unity 生成メタデータは手編集対象外。決定論・コスト0）。
//
// 入力 JSON (--input-file <path> または stdin):
//   {
//     "comments": [
//       { "comment_id": 123, "path": "Foo.cs", "line": 42, "start_line": null,
//         "diff_hunk": "@@ ... @@\n ...", "body": "Codex の指摘本文" }
//     ],
//     "review_summary": "Codex レビュー総評（行アンカー無し）。空文字なら overview は生成しない"
//   }
//
// 出力 JSON (stdout):
//   {
//     "replies": [
//       { "comment_id": 123, "action": "suggestion"|"manual"|"defer", "body": "返信 markdown（冪等マーカー無し）" }
//     ],
//     "overview": "PR 全体コメント用 markdown または null",
//     "errors": [ { "comment_id": 123, "message": "..." } ]
//   }
//
// 終了コード:
//   0: 正常終了（replies / overview の有無は問わない）
//   1: 致命的エラー（入力 JSON 不正）

import fs from 'node:fs';
import { spawnSync } from 'node:child_process';

// CLI 既定モデル（サブスクの既定 = Sonnet）を使う。必要なら CLAUDE_MODEL で上書き。
const MODEL = process.env.CLAUDE_MODEL || '';

function readInput() {
  const args = process.argv.slice(2);
  const i = args.indexOf('--input-file');
  const raw = i >= 0 && args[i + 1] ? fs.readFileSync(args[i + 1], 'utf8') : fs.readFileSync(0, 'utf8');
  return JSON.parse(raw);
}

const SYSTEM_INSTRUCTIONS =
  'あなたは Unity (URP / C# / HLSL) プロジェクトのコードレビュー対応エージェントです。' +
  'OpenAI Codex が付けたレビュー指摘を読み、PR 作成者に代わって対応方針を決めます。' +
  '指摘が妥当なら修正を、行アンカー範囲内かつ単一ファイルで直せるなら suggestion 用の置換コードを提示します。' +
  '複数ファイルや範囲外に及ぶ修正は manual、誤検知や対応不要と判断したら defer を選びます。' +
  '推測でコードを壊さないこと。確信が持てない・文脈不足なら defer にして理由を述べること。' +
  // 【重要】レビュー指摘を盲目的に受け入れない（ローカル CLAUDE.md と同方針）。
  '指摘を鵜呑みにせず、diff と指摘内容から「本当に妥当か（実バグ/明確な改善か、それとも誤検知・不要な複雑化・好みの問題か）」を' +
  '批判的に検証してください。妥当でないと判断した指摘は修正せず defer を選び、' +
  '「なぜ対応不要と判断したか」を簡潔に述べてください。実バグや正当な可読性/安全性/正確性の向上に限って suggestion / manual を選びます。' +
  'YAGNI・KISS を尊重し、シンプルで十分なコードをいたずらに複雑化させないこと。';

// 1 指摘ぶんのプロンプト。CLI には system/tool が無いため、指示・スキーマ・データを 1 本にまとめ、
// 「JSON オブジェクトのみを返す」ことを強く指示する。
function buildPrompt(c) {
  const isMultiline = c.start_line && c.start_line !== c.line;
  const range = isMultiline
    ? `${c.start_line} 行目〜${c.line} 行目（複数行）`
    : `${c.line ?? '不明'} 行目（単一行）`;
  return [
    SYSTEM_INSTRUCTIONS,
    '',
    'ツールは使わず、以下のデータだけで判断してください。',
    '出力は **JSON オブジェクト 1 個のみ**。前後に説明文やコードフェンスを付けないこと。',
    'スキーマ: {"action":"suggestion"|"manual"|"defer","explanation":"日本語の簡潔な説明",' +
      '"suggestion_code":"action=suggestion のときの置換後コード（説明やフェンスを含めない）。manual は参考差分（任意）。defer は空文字"}',
    '',
    `ファイル: ${c.path}`,
    `指摘がアンカーされている行範囲: ${range}`,
    '',
    'Codex の指摘:',
    c.body || '(本文なし)',
    '',
    '該当箇所の diff hunk（行頭が ` `/`+` の行が現在のファイル内容）:',
    '```diff',
    c.diff_hunk || '(diff_hunk なし)',
    '```',
    '',
    '【重要】GitHub の suggestion は「アンカー行範囲の全行」を suggestion_code で丸ごと置き換えます。',
    'そのため suggestion を選ぶ場合、suggestion_code は **アンカー行範囲（上記）に含まれる全行を過不足なく** 出力してください。',
    'その範囲内にある「変更しない行」（関数の宣言行・閉じ括弧 `}`・前後の行など）も **省略せずそのまま含める** こと。',
    '変更行だけを返すと、適用時に範囲内の他の行が消えてコードが壊れます。逆に範囲外の行は含めないでください。',
    'diff hunk から現在のアンカー行範囲の内容を再構成し、その範囲ぶんを置換後の形で返してください。',
  ].join('\n');
}

function buildSummaryPrompt(summary) {
  return [
    SYSTEM_INSTRUCTIONS,
    '',
    'ツールは使わず、以下の Codex レビュー総評（行アンカー無し）への対応方針を決めてください。',
    'コード行に紐づかないため suggestion は使えません。action は manual か defer を選んでください。',
    '出力は **JSON オブジェクト 1 個のみ**。スキーマ: {"action":"manual"|"defer","explanation":"日本語の簡潔な説明","suggestion_code":""}',
    '',
    '総評:',
    summary,
  ].join('\n');
}

// 先頭 `{` から対応する `}` までの「最初の完全な JSON オブジェクト」を返す。
// 文字列リテラル内の波括弧・エスケープを正しく無視する。見つからなければ null。
function firstBalancedObject(s) {
  const start = s.indexOf('{');
  if (start < 0) return null;
  let depth = 0;
  let inStr = false;
  let esc = false;
  for (let i = start; i < s.length; i++) {
    const ch = s[i];
    if (inStr) {
      if (esc) esc = false;
      else if (ch === '\\') esc = true;
      else if (ch === '"') inStr = false;
    } else if (ch === '"') {
      inStr = true;
    } else if (ch === '{') {
      depth++;
    } else if (ch === '}') {
      depth--;
      if (depth === 0) return s.slice(start, i + 1);
    }
  }
  return null;
}

// モデルがたまに混入させる末尾の不正トークンを除去する（例: `...","" }` / 末尾カンマ）。
function sanitizeJsonish(s) {
  return s
    .replace(/,\s*""\s*}/g, '}') // 末尾の空文字要素 `,"" }`
    .replace(/,\s*}/g, '}') // 末尾カンマ `, }`
    .replace(/,\s*]/g, ']');
}

// CLI 応答テキストから JSON オブジェクトを取り出す。フェンス・前後の文・末尾の不正トークンに耐える。
function extractJson(text) {
  const t = String(text).trim();
  const fence = t.match(/```(?:json)?\s*([\s\S]*?)```/i);
  // 候補をフォールバック順に試す: フェンス内 → 全文 → それぞれの最初の完全オブジェクト。
  const raw = [];
  if (fence) raw.push(fence[1].trim());
  raw.push(t);
  const candidates = [];
  for (const c of raw) {
    candidates.push(c);
    const obj = firstBalancedObject(c);
    if (obj) candidates.push(obj);
  }
  for (const c of candidates) {
    for (const variant of [c, sanitizeJsonish(c)]) {
      try {
        return JSON.parse(variant);
      } catch {
        /* 次の候補へ */
      }
    }
  }
  throw new Error(`JSON 抽出失敗: ${t.slice(0, 200)}`);
}

// claude CLI を 1 回呼び、モデルの応答テキスト（envelope.result）を返す。
function runClaudeOnce(prompt) {
  const argv = ['-p', prompt, '--output-format', 'json', '--allowedTools', ''];
  if (MODEL) argv.push('--model', MODEL);

  const r = spawnSync('claude', argv, {
    encoding: 'utf8',
    shell: false,
    maxBuffer: 16 * 1024 * 1024,
    env: process.env,
  });

  // 起動失敗（ENOENT 等）はリトライしても直らないので致命エラーとして区別する。
  if (r.error) {
    const err = new Error(`claude CLI を起動できません: ${r.error.message}`);
    err.fatal = true;
    throw err;
  }
  if (r.status !== 0) {
    throw new Error(`claude CLI が異常終了 (status=${r.status}): ${String(r.stderr || '').slice(0, 300)}`);
  }
  // --output-format json は {type:"result", result:"<text>", is_error:bool, ...} を返す
  let envelope;
  try {
    envelope = JSON.parse(r.stdout);
  } catch {
    throw new Error(`CLI 出力の JSON パース失敗: ${String(r.stdout).slice(0, 200)}`);
  }
  if (envelope.is_error) {
    throw new Error(`claude がエラーを返しました: ${String(envelope.result).slice(0, 300)}`);
  }
  return envelope.result;
}

// claude CLI を呼び、構造化された判定オブジェクトを返す。
// モデル出力の JSON が壊れることがあるため、パース失敗時は最大 3 回までリトライ（再生成）する。
function callClaude(prompt) {
  const MAX_ATTEMPTS = 3;
  let lastErr;
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    try {
      return extractJson(runClaudeOnce(prompt));
    } catch (e) {
      if (e.fatal) throw e; // 起動失敗はリトライしない
      lastErr = e;
    }
  }
  throw new Error(`${MAX_ATTEMPTS} 回試行しても有効な JSON を得られませんでした: ${lastErr && lastErr.message}`);
}

// モデルの構造化出力から、スレッド返信用の markdown を決定論的に組み立てる。
// ```suggestion フェンスは Node 側で固定生成し、モデルが本文内でフェンスを壊すのを防ぐ。
function assembleBody(decision) {
  const header = '🤖 **Claude による Codex 指摘への対応案**';
  const explanation = (decision.explanation || '').trim();

  if (decision.action === 'suggestion') {
    const code = (decision.suggestion_code || '').replace(/\n+$/, '');
    // 置換コードが空の suggestion を出すと、適用時に「アンカー行の削除」になってしまう。
    // モデルが action=suggestion を返しつつ suggestion_code を空にした場合は、
    // 空フェンスを post せず手動対応を促す本文にフォールバックする。
    if (!code.trim()) {
      return (
        `${header}\n\n${explanation}\n\n` +
        '> ⚠️ 置換コードを生成できなかったため自動 suggestion は提示できません。手動での対応をご検討ください。'
      );
    }
    return (
      `${header}\n\n${explanation}\n\n` +
      '下の「Commit suggestion」で PR ブランチへ適用できます。\n\n' +
      '```suggestion\n' +
      code +
      '\n```'
    );
  }

  if (decision.action === 'manual') {
    const code = (decision.suggestion_code || '').trim();
    const diffBlock = code ? `\n\n参考差分（手動適用が必要）:\n\n\`\`\`\n${code}\n\`\`\`` : '';
    return (
      `${header}\n\n${explanation}\n\n` +
      '> ⚠️ この修正は複数ファイル/範囲外に及ぶため自動 suggestion 化できません。手動で適用してください。' +
      diffBlock
    );
  }

  // defer
  return `${header}\n\n対応を見送ります（理由）: ${explanation}`;
}

// 指摘が紐づくファイルが Unity の `.meta` か判定する。`.meta` は常に `<asset>.meta` 形式。
function isMetaPath(path) {
  return typeof path === 'string' && path.endsWith('.meta');
}

// `.meta` 指摘に対する固定の見送り理由（手編集対象外であることを明示する）。
const META_DEFER_REASON =
  'このファイルは Unity の `.meta` です。`.meta` はプロジェクト規約により手編集の対象外で、' +
  '生成・更新は Unity Editor に委ねています（AI/手動編集は GUID 衝突などの破壊的リスクを伴うため）。' +
  'したがって本指摘へのコード修正は提案せず、対応を見送ります。';

function main() {
  let input;
  try {
    input = readInput();
  } catch (e) {
    process.stdout.write(JSON.stringify({ error: `入力 JSON のパースに失敗: ${e.message}` }) + '\n');
    process.exit(1);
  }

  const comments = Array.isArray(input.comments) ? input.comments : [];
  const result = { replies: [], overview: null, errors: [] };

  for (const c of comments) {
    // `.meta` 紐づけの指摘は LLM を呼ばず、固定理由で defer 返信を生成する（決定論・コスト0）。
    // suggestion/manual が生成される経路に構造的に到達しないため、誤って編集差分を出さない。
    if (isMetaPath(c.path)) {
      result.replies.push({
        comment_id: c.comment_id,
        action: 'defer',
        body: assembleBody({ action: 'defer', explanation: META_DEFER_REASON }),
      });
      continue;
    }
    try {
      const decision = callClaude(buildPrompt(c));
      result.replies.push({
        comment_id: c.comment_id,
        action: decision.action,
        body: assembleBody(decision),
      });
    } catch (e) {
      // 1 件の失敗で全体を落とさない。未対応として errors に記録し、
      // 返信は post しない（再実行時にマーカーが無いので再試行される）。
      result.errors.push({ comment_id: c.comment_id, message: e.message });
    }
  }

  const summary = (input.review_summary || '').trim();
  if (summary) {
    try {
      const decision = callClaude(buildSummaryPrompt(summary));
      result.overview = `🤖 **Claude による Codex レビュー総評への対応方針**\n\n${(decision.explanation || '').trim()}`;
    } catch (e) {
      result.errors.push({ comment_id: 'review_summary', message: e.message });
    }
  }

  process.stdout.write(JSON.stringify(result, null, 2) + '\n');
  process.exit(0);
}

main();
