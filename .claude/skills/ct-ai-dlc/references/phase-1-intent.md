# Phase 1: Intent 起票

このフェーズでは、「何を・なぜ作るか」を Intent として言語化する。
Intent は AI-DLC のすべての起点で、後続フェーズの判断基準になる。

## 成果物

`docs/ai-dlc/<date>-<topic-slug>/intent.md` (テンプレ: [../assets/intent-template.md](../assets/intent-template.md))

## Intent の 3 要素

| 要素 | 内容 | 例 |
|---|---|---|
| **Description** | 何を作るか | 「攻撃ヒット時に画面を強調する Impact Frames 機能を追加」 |
| **Context** | なぜ・どんな制約か | 「Cross Punch アニメと同期する演出。モバイル GPU 予算 1ms 以内」 |
| **Completion Criteria** | 完了条件 | 「Timeline Clip で配置可、複数 Clip 重疊で破綻しない、`uloop-run-tests` 通過」 |

## Completion Criteria は SMAV を満たすほど AI 自律性が上がる

| 属性 | 意味 | 不十分な例 | SMAV な例 |
|---|---|---|---|
| **S**pecific | 具体的 | 「速くする」 | 「p95 で 200ms 以内」 |
| **M**easurable | 測定可能 | 「使いやすく」 | 「DL 開始まで 3 秒」 |
| **A**tomic | 原子的 | 「全エッジケース」 | 「id 不在で 404」 |
| **V**erifiable | 検証可能 | 「コードがきれい」 | 「ESLint エラー 0」 |

理由: 完了条件が SMAV であるほど、AI は後段で「迷わずに」実装判断できる。曖昧な基準は Phase 2/3 で何度も人間に確認することになり、フローが停滞する。

## Completion Criteria は 3 要素で書く

- ✅ **成功ケース** — 達成すべき動作
- ❌ **失敗ケース** — 起きてはいけないこと（エラーハンドリング含む）
- 🔒 **品質ゲート** — 客観的な合格基準（コンパイル、テスト、レビュー）

この 3 つを書くと、AI はエラーハンドリングまで自律的に実装する。

## Intent に書かないもの（4 種）

Intent は「何を / なぜ」の安定した核を書くドキュメント（揺れやすい運用情報は持たせない）。次の 4 種は混入しやすいが、いずれも入れない:

| カテゴリ | 例 | 代わりにどうするか |
|---|---|---|
| **スケジュール** | リリース日 / マイルストーン / 期限 | GitHub Issue / PR description / カンバンで管理 |
| **後段で決まる数値プレースホルダ** | 「GPU 予算 X ms は Phase 2 で plan.md に転記」 | 相対・定性的な基準（「OFF 時 0」「対象オブジェクト数のオーダー以内」等）に留める |
| **CLAUDE.md 既出ルール** | `.meta` AI 編集禁止、サブモジュールポインタ非コミット、`Packages/manifest.json` swap 状態非コミット、領域別ガイドライン参照、コミットメッセージは日本語、など | CLAUDE.md が常時ロードされるため、Intent への重複記載は DRY 違反。Intent に書くのは **そのトピック固有で CLAUDE.md にない教訓** だけ |
| **具体の検証手段の固有名** | `uloop-compile`、`uloop-run-tests`、Unity Frame Debugger、Profiler など | Intent の品質ゲートは「検証カテゴリ + 客観メトリクス」で書き、「具体の検証手段は Phase 2 で plan.md に記述」と末尾に明記する |

**判定基準**: Intent に残すのは次の 4 条件をすべて満たすもののみ:

1. 後段 AI（Phase 2/3）の判断材料になる
2. 変動しない（少なくとも本トピック完了まで動かない）
3. 他のドキュメント／システムで管理されていない
4. 手段非依存（特定ツール／スキル／コマンド名に依存しない）

## Description / Completion Criteria は汎用的に書く

SIRIUS は複数プロダクト共通基盤なので、**Description と Completion Criteria では特定プロダクト固有の用語を使わない**。プロダクト固有の文脈（依頼元名、ゲーム内固有名詞、特定シーン名など）は **Context** の「背景となる依頼」「利用想定」に閉じる。

| セクション | プロダクト固有用語 | 例 |
|---|---|---|
| Description | ❌ 使わない | 「Gift で配置される Collectible のアウトライン...」 → 「SIRIUS のアウトライン機構に対し...」 |
| Completion Criteria | ❌ 使わない | 「Gift マッチフィクトリーと同等の見た目」 → 「対象モデル外側のみアウトライン描画」 |
| Context | ✅ OK | 「背景となる依頼: 一部プロダクト演出で...の要望が起点」 |

**理由**: Description / Completion Criteria が特定プロダクトに紐づくと、機能の汎用性（他プロダクトでの再利用可能性）が見えづらくなり、Phase 2 で「他プロダクトにも汎用化するか」の判断が偏る。

## 品質ゲートの書き方

検証カテゴリ + 客観メトリクスで書き、固有の手段名は plan.md に回す。

| レベル | 例 | Intent に書く？ |
|---|---|---|
| **検証カテゴリ**（何を担保するか） | コンパイルエラー 0、自動テスト pass、VRT 差分なし | ✅ 書く |
| **客観メトリクス**（業界標準語彙） | 警告 0、追加ドローコール 0、ステンシルバッファ汚染なし | ✅ 書く |
| **検証手段の固有名**（具体ツール / スキル / コマンド） | `uloop-compile`、`uloop-run-tests`、Unity Frame Debugger | ❌ Phase 2 plan.md で書く |

末尾に **「具体の検証手段（コマンド / スキル / 計測ツール）は Phase 2 (Inception) で plan.md に記述する。」** と明示すること。

## 動的軸を必ず確認する

Context に書く項目のうち、**動的軸** は Phase 2 でインタフェース層を決める入力になるため、Phase 1 で確定させる必要がある。intent.md に書かれていないと、Phase 2 で AI が「これは静的に設定する想定ですか？それともランタイムで切り替わりますか？」と確認しに戻り、フローが停滞する。

| 動的軸 | 定義 | Phase 2 で影響する論点 |
|---|---|---|
| **静的** | Inspector / Volume Profile で事前設定、ランタイム変更なし | OFF 時 cost 0 担保が緩い基準でよい |
| **半動的** | シーン入退出やステート遷移で C# が切替（数秒〜数分単位） | 切替時のちらつき・リーク・整合性要件が完了条件に入る |
| **完全動的** | フレーム単位 / 入力単位で頻繁に切替 | per-frame の追加コストを厳密に評価する必要 |

ユーザーに尋ねる時の例:
> この機能は次のどれで利用されますか？
> - 静的（Inspector / Volume Profile で事前設定）
> - 半動的（シーン入退出やステート遷移で C# が切替）
> - 完全動的（フレーム単位で頻繁に切替）
>
> 半動的・完全動的の場合、どのスクリプト/イベントが切り替えを行う想定か教えてください。

intent.md の Context 「動的軸」項目にこの答えを反映する。Phase 2 の `phase-2-inception.md` Step 1 (深掘り調査) と Step 3 (公開階層選定) でこの情報が参照される。

## 手順

### Step 1: 対話で 3 要素を引き出す

ユーザーとの対話で **Description / Context / Completion Criteria** を埋める。
質問は **不足している要素に絞る**。すでに引数や会話から推測できることは再確認しない。

ただし **動的軸** は intent.md に必須項目なので、明示的に確認すること（推測で埋めないこと。動的軸の前提ズレは Phase 2 全体のやり直しに繋がりやすい）。

例（不足要素のみ尋ねる）:
> 引数から「攻撃ヒット時のインパクトフレーム機能」と理解しました。
> 以下が不明なので教えてください:
> - 対象パッケージ: Sirius.PostProcessing で合っていますか？
> - GPU 予算の目安はありますか？
> - 動的軸: 静的 / 半動的 / 完全動的のどれですか？切替経路は？
> - 完了条件として「これだけは満たしたい」というテスト/動作はありますか？

### Step 2: テンプレートをコピーして埋める

```bash
cp .claude/skills/ct-ai-dlc/assets/intent-template.md \
   docs/ai-dlc/<date>-<topic-slug>/intent.md
```

`<date>-<topic-slug>` はスキル起動時に決定済みのフォルダ名を使う。

### Step 3: SMAV チェック

書き終えた intent.md を読み返し、Completion Criteria の各項目が SMAV を満たすか自己点検する。
満たさないものは具体化する。曖昧な基準を残したまま Phase 2 に進むと後で必ず詰まる。

### Step 4: 内容の最終確認

ユーザーに intent.md の中身を提示して合意を取る。
**全文を貼り付ける必要はなく**、要約 + 「全文は `docs/ai-dlc/.../intent.md` を参照」で十分。

ユーザーが修正を入れたければ会話で指示するか、ファイルを直接編集する。

### Step 5: 完了メッセージ

```
✅ Phase 1 (Intent) 完了
   出力: docs/ai-dlc/<date>-<topic-slug>/intent.md

次は Phase 2 (Inception) です。
コンテキストをリセットしてから実行することを推奨します（任意）:

   /clear
   /ct-ai-dlc <トピック>をinceptionから
```

`<トピック>` は元の引数のトピック部分を使う。コピペで動く形にする。

## このフェーズで AskUserQuestion を使う場面

- ✅ Description / Context / Completion Criteria の不足要素を質問する時
- ❌ 「Phase 2 に進んでいいですか？」のような形式的確認（メッセージで誘導するだけで十分）

## 注意事項

- intent.md には実装方針や UoW 分解を **書かない**。それは Phase 2 (Inception) の仕事
- intent.md は「**何を / なぜ**」の安定した核を書く。Phase 2/3 で要件が大きく変わった場合は intent.md を更新し、その変更点からフローを再進行する（別フォルダは作らない）
- 過去 PR の参照リンクを Context に書くのは「**そのトピック固有で CLAUDE.md にない教訓**」に限定する。CLAUDE.md 既出ルール（`.meta` 編集禁止 / サブモジュール / Packages/manifest.json 等）を Intent に再記載しない
