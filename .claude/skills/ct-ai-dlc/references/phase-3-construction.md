# Phase 3: Construction（実装）

このフェーズでは plan.md に従って実装を進め、コンパイル・テストまでを直列で進める。コミット以降（コミット / push / PR 作成）は git 操作にあたるため、**ユーザーが明示的に許可した場合にのみ実行する**（[SKILL.md](../SKILL.md) 共通ルール「git 操作はユーザー許可制」を参照）。許可がなければ Step 4 完了時点（実装＋テスト通過）でいったん報告して止まる。

## 入力

- `docs/ai-dlc/<date>-<topic-slug>/plan.md` (Phase 2 の成果物)
- [./sirius-repos.md](./sirius-repos.md) (リポジトリ役割マップ)
- 対象パッケージの `CLAUDE.md`, `SKILL.md`

## 成果物

- 各リポジトリ (SIRIUS / SiriusPackages / SiriusAssets) への PR
- (将来) `docs/ai-dlc/<date>-<topic-slug>/review-notes.md`, `release-report.md` などを同フォルダに追加可能

## 並列発散と直列収束のモデル

```
領域独立な実装は並列 OK:
  ├ 異なるパッケージのファイル編集
  ├ 別 CLAUDE.md / SKILL.md 更新
  └ デモシーン用スクリプトとパッケージ実装の同時進行

直列必須（干渉する）:
  ├ uloop-compile / uloop-run-tests (Unity Editor は単一)
  ├ git add / commit / push (index は単一)
  ├ Packages/manifest.json 編集 (共有ファイル)
  └ 同一ファイルへの書き込み
```

**本 PR (AI-DLC 導入の最小構成) では並列実装は使わない**。
すべての UoW を順次実行する。並列実行は将来サブエージェント導入時に追加する。

## 手順

### Step 1: plan.md を読み込み、UoW 順序を確定

plan.md の依存関係に従って UoW の実行順を確定する。
人間作業推奨の UoW は AI が実装をスキップし、後で人間が完了させる前提で進める。

### Step 2: UoW を 1 件ずつ実装

各 UoW について:

1. plan.md の該当 UoW セクションを読む
2. 対象パッケージの `CLAUDE.md`, `SKILL.md` を読む（まだ読んでなければ）
3. 編集対象ファイルを Read してから Edit / Write
4. **判断点があればメッセージで報告**（AskUserQuestion ではなく地の文で）

### Step 3: コンパイルチェック

UoW 完了ごと、または複数 UoW がひと段落したら:

```
/uloop-compile
```

エラーが出たら直して再実行。エラーが消えるまで次に進まない。

### Step 4: テスト実行（PR マージ前の品質ゲート — MUST）

**品質ゲート（MUST）**: PR をマージする前に、**macOS（iOS）と Windows（Android）の両方で全 AverageTest（`Assets/Tests/Runtime/AverageTest.cs` の PlayMode ビジュアルリグレッション全ケース ＋ EditMode 全テスト）が成功していること**を必須とする（`AverageTest` は実行 OS の期待ターゲット以外だとスキップされる。ターゲット切替の詳細は Step 4.5 参照）。SIRIUS / SiriusPackages のどちらの CI にも Unity テストの自動ゲートは存在しないため、**AI が手動で TestRunner を実行して担保する**。シェーダ・描画の変更（アウトライン等）は変更対象シーン以外（Shading / PBR / 反射・ポストエフェクト等、同じ描画経路を通る全シーン）にも波及し得るので、**変更箇所に関係なく必ず全件を回す**。一部だけ回して成功しても品質ゲートを満たしたとは見なさない。

```
/uloop-run-tests --test-mode EditMode
/uloop-run-tests --test-mode PlayMode
```

**flaky リトライ規則（MUST）**: 一部のテストは何らかの理由（FLIP 閾値近傍のプラットフォーム差等。特に反射・スクリーンスペース系 SSR / SSPR / PlanarReflection / LightShaft で起きやすい）で初回に失敗することがある。試行回数は**初回 + リトライ 2 回 = 最大 3 試行**とし、リトライ対象は**直前の試行で失敗したテストのみ**（成功テストは再実行しない）。**3 試行のうち 1 回でも成功すれば PASS**（flaky 扱い）。**3 試行連続で失敗したテストは「確定失敗」とし、それ以上リトライせず、FLIP Mean / 閾値 / 差分画像パスを添えて AskUserQuestion で人間の判断に引き渡す**。flaky か実装起因かの最終判定は人間が行い、実装起因と判断された場合のみ修正して再度全件を成功させる。再実行は失敗ケースだけに絞れる:

```
# 単一: exact フィルタ
/uloop-run-tests --test-mode PlayMode --filter-type exact --filter-value 'Tests.Runtime.AverageTest.Test("PostProcess_SSR/SSR",0)'
# 複数: regex フィルタ
/uloop-run-tests --test-mode PlayMode --filter-type regex --filter-value '.*(SSR|SSPR|PlanarReflection|LightShaft).*'
```

**実行上の注意（CLI タイムアウト ≠ テスト失敗）**: PlayMode 全件はシーンロード込みで **uloop CLI のリクエストタイムアウト（180 秒）を超える**ことがある。CLI が `Request timed out` を返しても **Unity 側ではテストが完走している**ことが多い。全件は `run_in_background` で実行し、完了後に **`.uloop/outputs/TestResults/<timestamp>.xml`（NUnit 形式）を読んで合否を判定**する。XML は encoding 宣言が不正なことがあるので、先頭の `<?xml ...?>` を除去してからパースする。失敗があればログも確認: `/uloop-get-logs`。

新規シーン追加に伴う期待画像未登録のような「想定内の失敗」は plan.md に追記して許容、それ以外は上記リトライ規則で判定する。

### Step 4.5: ビジュアルリグレッション / ビジュアル検証の勘所

シェーダ・描画を変更する Phase 3 では、ビジュアルリグレッションテスト (`Assets/Tests/Runtime/AverageTest.cs`、NVIDIA FLIP 画像比較) と手動 screenshot 検証で繰り返しハマる罠がある。グラフィックス変更時は必ず参照すること（2026-05 アウトライン内側食い込み抑制で実際に踏んだ罠を反映）。

**ビルドターゲット（最重要）**:
- `AverageTest` は `OneTimeSetUp` の Validate Mobile Target（デフォルト有効）により、**実行 OS の期待ターゲット以外（例: StandaloneOSX）だと `Assert.Ignore` でスキップ**される。期待ターゲットは OS で異なる（`Application.platform` で分岐）: **macOS → iOS / WebGL**、**Windows → Android / WebGL**。GraphicsAPI も D3D12/Metal 必須。
- 正しい pass/fail 判定には実行 OS の期待ターゲット（**macOS なら iOS、Windows なら Android**。WebGL は共通）に切り替えてから実行する。**AI が `uloop execute-dynamic-code` で切替できる**:
  ```csharp
  using UnityEditor; using UnityEditor.Build;
  // macOS の例。Windows では NamedBuildTarget.Android, BuildTarget.Android を指定する
  EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.iOS, BuildTarget.iOS); // 戻り値 true で成功
  ```
  - 対象の Build Support モジュール（iOS / Android / WebGL）がインストールされている必要がある。未導入だと切替が false になる — その場合のみ人間に依頼。
  - **別ターゲットへの初回切替はアセット再インポート（テクスチャ圧縮形式の変換等）が走り時間がかかる**。切替後は `uloop compile --wait-for-domain-reload true` 等で落ち着くのを待ってからテストする。
  - 現在のターゲットは `EditorUserBuildSettings.activeBuildTarget`、実行 OS は `Application.platform` で確認できる。

**OFF 検証（既存挙動が変わらないことの証明）**:
- 「デフォルト OFF で既存挙動と同一」を最も確実に証明する方法は **git stash でベースライン比較**:
  1. 変更を `git stash`
  2. `uloop execute-menu-item --menu-item-path "Assets/Refresh"` + `uloop compile --wait-for-domain-reload true` でベースラインをビルド
  3. 同じテストを実行し FLIP Mean を記録
  4. `git stash pop` で変更を戻し、再 Refresh + compile + 同テスト
  5. **FLIP Mean が完全一致すれば変更前後でピクセル単位同一**と実証できる。プラットフォーム差を相殺できるので StandaloneOSX でも判定可能。

**ON 検証（新機能の見た目を screenshot で確認）**:
- `uloop control-play-mode --action Play` → `uloop screenshot --window-name Game --capture-mode rendering` で PlayMode のゲーム画面をキャプチャ。
- **アニメーションを必ず止める**: `uloop execute-dynamic-code` で `Time.timeScale = 0f` を設定してから OFF/ON を撮る。止めないと撮影間の数秒でポーズが変わり、**機能の差分がアニメ差に埋もれて判別不能**になる（本セッションで実際に発生）。
- OFF/ON は `md5` で同一でないこと（＝設定が反映されたこと）を先に確認し、PIL (`ImageChops.difference`) で差分領域・変化ピクセル数・RGB の色傾向を定量化すると確実。1体を crop して 2 倍拡大した OFF/ON 並置画像が人間にも分かりやすい。

**uloop execute-dynamic-code の落とし穴**:
- `UnityEngine.Object` は `System.Object` と曖昧になるため **完全修飾**する（`UnityEngine.Object.FindObjectsByType<T>(...)`）。
- `VolumeProfile` に `GetComponent` は **存在しない**。`profile.TryGet<T>(out var c)` を使う。誤ると無言のコンパイルエラーで設定が一切適用されず、原因（同一画像になる等）に気付きにくい。
- Volume の値を動的に変えるときは **`sharedProfile` を変更**する。`volume.profile`（ランタイム instance）経由の変更が VolumeManager のスタックに反映されないことがある。アセットを変えるので **検証後に必ず元に戻す**。
- 戻り値の `Result` が空のときはたいてい無言のコンパイルエラー。`grep` で絞らず出力全体（`CompilationErrors` / `ErrorCode`）を確認すること。

### Step 5: コミット

> **このステップ以降（コミット / push / PR）は git 操作。実行前にユーザーの明示的な許可を得ること**（[SKILL.md](../SKILL.md) 共通ルール「git 操作はユーザー許可制」）。許可がなければ Step 4 完了時点で報告して止まり、以降の手順は許可が出てから進める。以下は **許可された後に従う手順**。

変更の性質ごとにコミットを分ける。コミット先は plan.md に書いた通り。

**重要なルール:**
- コミットメッセージは **日本語**（CLAUDE.md 規約）
- `git add` は **ファイル名を明示**（`-A` / `.` は機密ファイル混入のリスク）
- `.meta` ファイルは Unity 生成のものをそのままコミット（AI が編集したものはコミット前に検出）

```bash
# パッケージ実体
git add SiriusPackages/Sirius.PostProcessing/...
git commit -m "Impact Frames Pass を追加"

# デモシーン
git add Assets/Demo/ImpactFrames/...
git commit -m "Impact Frames デモシーンを追加"
```

### Step 6: ブランチ作成と push

CLAUDE.md 規約: **PR を作成するときは必ず `origin/main` から新しいブランチを作成**。

```bash
git checkout -b feat/impact-frames origin/main
# (コミットを cherry-pick または再作業)
git push -u origin feat/impact-frames
```

### Step 7: PR 作成

本文は [assets/pr-template.md](../assets/pr-template.md) を雛形にする（lean / 署名なし）。

**7-1: 変更の有無を確認**

feature ブランチが `origin/main` より先行していることを確認する:

```bash
git rev-list --count origin/main..HEAD
```

**7-2: パッケージドキュメント更新**

`SiriusPackages/` のパッケージ実体が変わっている場合、[/ct-update-pkg-docs](../../ct-update-pkg-docs/SKILL.md) の手順で該当パッケージの CLAUDE.md / SKILL.md を更新し、**実装とは別コミット**にする。

**7-3: ブランチを push**

PR 作成にはリモートブランチが必要。未 push のブランチは push する（push は外部反映なので、未確認なら一言ことわってから実行）。

**7-4: PR を作成**

`assets/pr-template.md` の `<...>` を埋め、ガイドコメントを除去した本文を一時ファイルに書き出して作成する。タイトルは変更の本質を簡潔に表す（70 文字以内目安）。

```bash
gh pr create --base main \
  --title "<変更の要点を簡潔に>" --body-file /tmp/pr-body.md
```

- 概要に既定値変更・追加アセット・lockfile 差分の意図を明記する。最低 Unity 版を上げたら `version` メジャー bump する。
- **テスト欄は品質ゲート（macOS→iOS / Windows→Android の両方で全 AverageTest 成功、リトライ規則）を実際に確認した上で `[x]`** にする。テスト欄はプラットフォーム別に 2 チェック項目へ分かれている。
- 作成前に PR タイトルと本文を提示し、**AskUserQuestion で確認してから作成**する。

**7-5: 関連 PR のクロスリンク（複数 PR に分けた場合のみ）**

関連する PR が複数あるときは、URL が出揃ってから各 PR 本文の「関連 PR」に相互リンクを追記する（本文を再生成して `gh pr edit --body-file` で差し替え）:

```bash
gh pr edit <PR_A番号> --body-file /tmp/pr-body-A-linked.md  # 関連に PR_B を追記
gh pr edit <PR_B番号> --body-file /tmp/pr-body-B-linked.md  # 関連に PR_A を追記
```

→ どの PR からも双方向にたどれる状態にする。完成した PR URL は Step 8 の完了メッセージに含める。

### Step 8: 完了メッセージ

```
✅ Phase 3 (Construction) 完了
   作成 PR:
     - PR #XXX
   関連 plan: docs/ai-dlc/<date>-<topic-slug>/plan.md

次は Phase 4 (Review) です。PR にレビュアーを割り当て、
チームで使っている連絡手段でレビュー依頼を送ってください。
```

## このフェーズで AskUserQuestion を使う場面

- ✅ テスト失敗の対応方針（無視 / 修正）
- ✅ PR 作成時のコミット粒度・タイトル
- ❌ 「次の UoW に進んでいいですか？」のような形式的確認

## 注意事項

- **plan.md と異なる実装をした場合は plan.md を更新する** — 「採用設計」と「実装」が食い違う状態を残さない
- **判断点をメッセージで報告する** — ユーザーが気付ける形にする（AskUserQuestion で止めないが、報告は重要）
- **`.meta` ファイルを AI が編集していないか確認** — Unity Editor が生成したものに限定（CLAUDE.md 規約、PR #553 教訓）
- **PR を分けるべき変更は分ける** — 1 PR にしすぎると review コストが増える。plan.md の「PR 本数」見積もりを尊重する
