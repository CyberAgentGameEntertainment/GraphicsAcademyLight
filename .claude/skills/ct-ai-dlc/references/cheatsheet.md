# AI-DLC チートシート（Phase 1〜4）

各フェーズで **決めるもの / 作るもの（成果物）/ やらない・注意 / 起動コマンド** を1枚に凝縮したもの。
詳細は各フェーズの reference を参照。Phase 5 (Operations) は未整備のため本チートには含めない。

```
Phase 1          Phase 2           Phase 3            Phase 4
Intent      →    Inception    →    Construction  →    Review
（何を/なぜ）      （どう作る）        （実装）            （レビュー）
   ↓                ↓                 ↓                  ↓
intent.md        plan.md           コード + PR         GitHub
   └────────────── docs/ai-dlc/<date>-<topic-slug>/ ──────────┘
```

Phase 間の引き継ぎは **すべてファイル経由**（自己完結）。フェーズ完了ごとに `/clear` してよい。

---

## 早見表

| Phase | 決めるもの（核） | 作るもの | 起動コマンド |
|---|---|---|---|
| **1 Intent** | Description / Context（★動的軸）/ Completion Criteria（SMAV） | `intent.md` | `/ct-ai-dlc <トピック>` |
| **2 Inception** | 既存制約調査 → 採用設計（公開階層）→ UoW 分解 → コミット先 | `plan.md` | `/ct-ai-dlc <トピック>をinceptionから` |
| **3 Construction** | UoW 順次実装 → コンパイル0 → 全 AverageTest 成功（品質ゲート） | **PR** | `/ct-ai-dlc <トピック>を実装` |
| **4 Review** | 対象 PR / レビュアー / 変更概要 | GitHub レビュアー割当 + レビュー依頼 | ―（チームの運用に合わせる） |

---

## Phase 1: Intent（何を / なぜ）
参照: [phase-1-intent.md](phase-1-intent.md) ／ 雛形: [../assets/intent-template.md](../assets/intent-template.md)

**決めるもの**
- **Description** — 何を作るか。汎用機能として記述（プロダクト固有用語なし）。実装方針は書かない
- **Context** — なぜ・制約
  - ★**動的軸（静的 / 半動的 / 完全動的）＝必須**（Phase 2 のインタフェース層決定の入力。半動的・完全動的なら切替経路も）
  - 配置先パッケージ候補 / 既存類似機構との関係 / パフォーマンス制約（相対・定性）/ 利用想定 / 背景依頼 / トピック固有で CLAUDE.md にない教訓
- **Completion Criteria** — SMAV を満たす完了条件を3要素で：✅成功ケース／❌失敗ケース／🔒品質ゲート（検証カテゴリ＋客観メトリクス）

**書かない4種**
スケジュール ／ 後段で決まる数値プレースホルダ ／ CLAUDE.md 既出ルール ／ 具体の検証手段の固有名（→ 品質ゲートは「カテゴリ＋メトリクス」で書き、手段は Phase 2 plan.md へ）

**作るもの**: `docs/ai-dlc/<date>-<topic-slug>/intent.md`（1枚）

---

## Phase 2: Inception（どう作る）
参照: [phase-2-inception.md](phase-2-inception.md) ／ 雛形: [../assets/plan-template.md](../assets/plan-template.md)

**決めるもの**
- **既存実装の制約（Step 1・設計前に必須）** — 変更関数の本体ロジック／1-hop caller・callee／データ書き込み元／エンコード・命名・単位の規則／入力経路・既存公開階層。**既存パターンを1段落で言語化**（以降の採否判断の土台）
- **採用設計（Step 2–3）** — 2〜3案を発散 → 1案採用
  - 評価2軸: ①既存原則との整合 ②新規導入インフラの量
  - 優先順: 既存原則に整合する案を最優先。逸脱案は**客観的理由**が必須
- **インタフェース層（Step 3）** — intent の動的軸 + 既存公開階層と整合
  - 公開階層（VolumeComponent / MonoBehaviour SerializeField / Material Property / ProjectSettings）＋理由 / API パターン（getter・setter・命名・既定値を既存と揃える）/ 配置場所
- **UoW 分解（Step 4）** — 対象ファイル・依存・並列可否・担当（AI／人間／両方）。粒度 ≒ コミット1〜2個。ShaderGraph・Timeline・FBX 配置は**人間作業として明示**
- **コミット先（Step 5）** — 変更がどのディレクトリに着地するか（`SiriusPackages/` のパッケージ実体／`Assets/` のデモ・テスト／`docs/` など）

**残す・守る**: 棄却案と**客観的な棄却理由**を消さない（「なぜこの設計か」の記録）

**作るもの**: `docs/ai-dlc/<date>-<topic-slug>/plan.md`（採用設計＋公開階層／棄却案と理由／UoW一覧〈対象・依存・担当・コミット先〉／並列可能ペア／触ってはいけないファイル）

---

## Phase 3: Construction（実装）
参照: [phase-3-construction.md](phase-3-construction.md) ／ PR 雛形: [../assets/pr-template.md](../assets/pr-template.md)

**やること / 決めるもの**
- **実装（Step 2）** — UoW を1件ずつ順次（本構成では並列実装しない）。対象パッケージの CLAUDE.md / SKILL.md を読み、Read → Edit/Write。判断点は AskUserQuestion で止めず地の文で報告
- **コンパイル（Step 3）** — エラーが消えるまで次に進まない
- **テスト＝品質ゲート（MUST・Step 4）**
  - **macOS（iOS）と Windows（Android）両方**で全 AverageTest（PlayMode ビジュアルリグレッション全ケース＋EditMode 全件）成功
  - シェーダ・描画変更は他シーンに波及 → **変更箇所に関係なく必ず全件**
  - **flaky 規則**: 初回＋リトライ2回＝最大3試行、リトライは失敗テストのみ、1回でも成功で PASS。**3連続失敗＝確定失敗 → FLIP Mean/閾値/差分画像を添えて AskUserQuestion で人間判断**
  - CLI タイムアウト（180秒）≠ テスト失敗 → `run_in_background` ＋ `TestResults/<ts>.xml` で判定
- **ビジュアル検証（Step 4.5）** — ビルドターゲット切替（macOS→iOS / Windows→Android）／git stash で OFF ベースライン比較／ON は `Time.timeScale=0` で screenshot／execute-dynamic-code の落とし穴（`sharedProfile` 変更・`TryGet`・完全修飾）

**守るルール（git 操作以降）**
- **commit / push / PR はユーザー明示許可制**。許可がなければ Step 4 完了（実装＋テスト通過）で報告して停止
- コミットメッセージは日本語 / `git add` はファイル名明示 / `.meta` は AI 編集分を混入させない
- **plan.md と実装が食い違ったら plan.md を更新**

**作るもの**: **PR**（pr-template、lean・署名なし）
- `SiriusPackages/` のパッケージ実体が変わったら、該当パッケージの CLAUDE.md / SKILL.md を**別コミット**で更新

---

## Phase 4: Review（レビュー）
専用 reference なし。レビュー依頼の送り方はチームの運用に合わせる。

**決めるもの**
- 対象 PR（引数 or 現在ブランチから自動検出、関連 PR を束ねる）
- レビュアー選択（PR author を除くメンバーから複数選択・表示名で提示）
- 変更概要（**1〜2個の箇条書き**、詳細は PR 本文に委ねる）
- 送信可否（プレビュー → 送信／編集／キャンセル）

**やること**
- GitHub レビュアー設定（`gh pr edit <PR> --add-reviewer ...`、関連 PR にも同じレビュアー）
- チームで使っている連絡手段でレビュー依頼を送る

**注意**
- **送信前に必ずプレビュー確認**（外部発信）
- 依頼文はプレーンテキスト中心にする（チャットツールによってはマークダウン記法がリンク解析と干渉する）

**作るもの**: レビュー依頼 ＋ GitHub 側レビュアー割当

---

## 全フェーズ共通ルール
- **git 操作はユーザー許可制**（add / commit / push / ブランチ作成 / PR 作成）。自走判断より優先
- **触ってはいけないファイル**: `.meta`（Unity 生成）／`Packages/manifest.json` の swap 状態／`LocalPackages/*.tgz`
- **品質ゲート（PR マージ前）**: macOS→iOS と Windows→Android の両方で全 AverageTest 成功
- **Markdown 引き継ぎ**: 中間成果物は `docs/ai-dlc/<date>-<topic-slug>/` に固定ファイル名（`intent.md` / `plan.md`）で置く
- **Human in the Loop**: 迷ったら進む。AskUserQuestion は「人間にしか判断できない場面／不可逆操作の前」に限定
