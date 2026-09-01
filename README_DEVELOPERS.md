# SIRIUS 開発者向けドキュメント

**目次**

- [Start up](#start-up)
- [リグレッションテストの環境の構築](#リグレッションテストの環境の構築)
- [Claude Code スキル](#claude-code-スキル)


## Start up
### SIRIUSデモのダウンロードとセットアップ

1. **リポジトリのクローン**
   ```bash
   git clone git@github.com:CyberAgentGameEntertainment/GraphicsAcademyLight.git
   cd GraphicsAcademyLight
   ```

> このリポジトリに100MBを超えるファイルは含まれていないため、Git LFSは使用していません（通常の `git clone` のみで全ファイルを取得できます）。

### SiriusPackages ディレクトリについて

このディレクトリには、SIRIUSで配布される以下の3つのパッケージが含まれています（ワークショップ用縮小版）。

- `Sirius.Core/`
- `Sirius.PostProcessing/`
- `Sirius.DevSupport/`

これらのパッケージは、SIRIUSの主要な機能ごとに分割されています。

#### Sirius Core への依存について

`Sirius.PostProcessing` の `package.json` には `jp.co.cyberagent.sirius.core` を依存として宣言していません。
UPM の仕様上、Git URL でインストールされたパッケージは semver 依存解決に参加しないため（[Unity Issue Tracker #1261468](https://issuetracker.unity3d.com/issues/package-manager-doesnt-resolve-the-dependencies-of-git-url-packages), "By Design"）、依存を宣言するとレジストリ側を参照しに行き、Git URL インストール時に競合が発生します。
そのため、README で Sirius Core を先にインストールするよう案内する方法で対応しています。

### サブモジュールについて

配布版では `SiriusPackages` / `SiriusAssets` は通常のフォルダとして同梱されています。
サブモジュールの初期化(`git submodule update`)は不要です。


## リグレッションテストの環境の構築
SIRIUSのビジュアルリグレッションテストではNVIDIAのFlipを利用しています。Macは権限周りのセットアップが不要なので下記のドキュメントに沿ってセットアップをお願いします。（Windowsは不要）

### セットアップ手順

1. ターミナルを開きます。

2. プロジェクトのルートディレクトリに移動します：
   ```bash
   cd /path/to/project
   ```

3. セットアップスクリプトに実行権限を付与します：
   ```bash
   chmod +x setup.sh
   ```

4. セットアップスクリプトを実行します：
   ```bash
   ./setup.sh
   ```

5. もし権限エラーが発生した場合は、以下のコマンドで管理者権限で実行してください：
   ```bash
   sudo ./setup.sh
   ```

### 動作確認

セットアップが正常に完了すると、以下のメッセージが表示されます：
```
quarantine 属性は正常に削除されました
```

### テストの実行
SIRIUSのデモを立ち上げてGeneral/Test Runnerを実行し、以下の点を確認してください：

1. ビルドターゲットを以下のように設定すること
   - Windows: **Android**
   - macOS: **iOS**
2. SIRIUSのデモアプリケーションが正常に起動すること
3. Test Runnerウィンドウが表示されること
4. Test RunnerのPlayモードでテストが正常に実行され、エラーが発生しないこと
   1. 一部のテストでエラーが発生する場合はテストが壊れている可能性があるので、担当者に確認してください。

<figure>
  <figcaption style="text-align: center;">図: Test Runner実行例</figcaption>
</figure>

### テスト結果の出力先

テスト実行時に生成される画像ファイルは以下のディレクトリに出力されます：

| 種類 | パス |
|------|------|
| 実行結果画像 | `Assets/ActualImages/Linear/{Platform}/None/` |
| 期待画像 | `Assets/Tests/SuccessfulImages/Linear/{Platform}/None/` |

`{Platform}` はプラットフォームに応じて以下のようになります：
- Windows (DirectX 12): `WindowsEditor/Direct3D12`
- macOS (Apple Silicon): `OSXEditor_AppleSilicon/Metal`

テスト失敗時には、以下の画像が出力されます：
- `{テスト名}.png` - 実際の描画結果
- `{テスト名}.diff.png` - 差分画像
- `{テスト名}.expected.png` - 期待画像

### デバッグ用ツール

テストのデバッグ用に以下のメニューが用意されています：

| メニュー | 説明                                                                   |
|----------|----------------------------------------------------------------------|
| `Tools/Sirius/Dev Support/Validate Mobile Target` | セーフティとしてモバイルターゲット（Windows: Android、macOS: iOS）の強制を有効/無効にします。デフォルトON。 |
| `Tools/Sirius/Dev Support/[Debug] Strict Threshold` | 画像比較の閾値を厳格にします。エディタ起動時は無効です。                                         |
| `Tools/Sirius/Dev Support/Copy AverageTest Result` | テスト結果画像を期待画像ディレクトリにコピーします。テストが成功した画像を新しい期待画像として登録する際に使用します。          |

## Claude Code スキル

Claude Code で利用できるスキル（スラッシュコマンド）について説明します。
プロジェクト固有のスキルと uloop スキルは `.claude/skills/` に格納されています。

### uloop CLI セットアップ（Unity Editor 連携）

SIRIUSプロジェクトでは [uloop CLI](https://github.com/hatayama/uloop) を使って Claude Code から Unity Editor と通信します。

#### 1. uloop CLI のグローバルインストール

```bash
npm install -g uloop-cli
```

uloop スキルはリポジトリの `.claude/skills/uloop-*/` にコミット済みのため、追加インストールは不要です。

> **Note:** CLI (`uloop-cli`) と Unity パッケージ (`io.github.hatayama.uloopmcp`) は**メジャーバージョンを揃える**必要があります。`Packages/manifest.json` の `uloopmcp` を更新した際は、各自 `uloop update` で CLI も合わせて更新してください。

#### 2. Claude Code のユーザーグローバル設定

`~/.claude/settings.json` の `permissions.allow` に以下を追加:

```json
{
  "permissions": {
    "allow": [
      "Bash(uloop *)"
    ]
  }
}
```

#### 3. Unity Editor の uLoopMCP セキュリティ設定（おすすめ）

Unity Editor のメニューから **Window > uLoopMCP** を開き、以下をすべて有効にしてください:

| 設定 | 推奨値 | 説明 |
|------|--------|------|
| Enable Tests Execution | ON | Claude Code からのテスト実行を許可 |
| Allow MenuItem Execution | ON | MenuItem の実行を許可 |
| Dynamic Code Security Level | FullAccess | 動的コード実行の許可 |

> **Note:** この設定は `UserSettings/` 配下（gitignore対象）のため、各自で設定が必要です。

#### 4. 動作確認

Unity Editor を起動した状態で以下を実行:

```bash
uloop compile
```

JSON レスポンスが返ればセットアップ完了です。

#### 旧 unity-natural-mcp からの移行者向け

`.claude/settings.local.json` に以下の設定が残っている場合は手動で削除してください:

- `enabledMcpjsonServers` 配列から `"unity-natural-mcp"` を削除
- `enableAllProjectMcpServers` が不要であれば削除

### AI 駆動開発フロー（/ct-ai-dlc）

SIRIUS で **AI 駆動開発 (AI-DLC)** を開始/継続するための起点スキルです。
ユーザーが `/ct-ai-dlc` と明示的に呼び出した時のみ起動し（`disable-model-invocation: true` 指定）、普段の開発スタイルには干渉しません。AI-DLC を選択したユーザー・タスクにだけ厳密なフローを適用します。

#### 思想

従来の「人間が書く → AI が補助」を反転させ、**AI が能動的に計画・提案 → 人間が承認 → AI が実装** という主従逆転を行う方法論です（AWS 提唱の AI-DLC ベース）。Intent (何を/なぜ) → Inception (どう作る) → Construction (実装) を Phase 単位で進め、各 Phase の成果物は Markdown ファイルとして `docs/ai-dlc/<date>-<topic>/` に蓄積されます。

#### 使い方

```
/ct-ai-dlc                              # 引数なし。進行中フォルダを列挙、または新規 Intent
/ct-ai-dlc <自由テキスト>                # 自然言語でトピック・開始フェーズを指定
```

引数は **自由テキスト** で受け取り、スキルが文脈から「トピック」と「開始フェーズ」を推測します。

例:

| 入力 | 動作 |
|---|---|
| `/ct-ai-dlc impactframe実装` | 新規 Intent を Phase 1 から起票 |
| `/ct-ai-dlc impactframe実装をinceptionから` | 既存フォルダで Phase 2 (Inception) を開始 |
| `/ct-ai-dlc ハイブリッドGIの計画立てる` | 既存フォルダで Phase 2 (Inception) を開始 |
| `/ct-ai-dlc 続きから` | 直近の進行中フォルダの次フェーズを開始 |

#### 5 フェーズの流れ

| Phase | 内容 | 使用スキル / エージェント | 成果物 |
|---|---|---|---|
| 1. Intent | 何を/なぜ作るか起票 | `/ct-ai-dlc` | `docs/ai-dlc/<date>-<topic>/intent.md` |
| 2. Inception | 設計案検討と UoW 分解 | `/ct-ai-dlc <topic>をinceptionから` | `docs/ai-dlc/<date>-<topic>/plan.md` |
| 3. Construction | 実装・コンパイル・テスト・PR 作成 | `/ct-ai-dlc <topic>を実装` | PR |
| 4. Review | レビュー依頼 | ―（チームの運用に合わせる） | レビュー依頼 + GitHub レビュアー設定 |
| 5. Operations | リリース検証（手動） | ― | 検証レポート |

> **品質ゲート（MUST）**: PR マージ前に **macOS（iOS）と Windows（Android）の両方で全 AverageTest（PlayMode ビジュアルリグレッション ＋ EditMode）の成功**を AI が手動 TestRunner 実行で担保します（`AverageTest` は実行 OS の期待ターゲット以外だとスキップされるため、両プラットフォームを別々に担保）。閾値近傍で初回失敗するテストは失敗テストのみを対象に**初回 + リトライ 2 回 = 最大 3 試行**まで実行し、**1 回でも成功すれば PASS**・**3 試行連続で失敗したものは「確定失敗」としてそれ以上リトライせず、人間の判断に引き渡し**ます（詳細: [Phase 3 Step 4](.claude/skills/ct-ai-dlc/references/phase-3-construction.md)）。

#### 推奨運用: Phase 間でコンテキストリセット

Phase 完了時に `/clear` を **軽く推奨** します（強制ではない）。中間 Markdown ファイルが自己完結している前提で、Phase ごとにコンテキストをリセットすると:

- 次フェーズが新鮮なコンテキストで動く（トークン消費を抑制）
- Markdown 引き継ぎの品質が自然に上がる（手抜きをすると次フェーズで困る）

各 Phase 完了時のメッセージで、次フェーズの起動コマンドが **コピペで動く形** で提示されます。

#### 関連ドキュメント

- スキル定義: [.claude/skills/ct-ai-dlc/SKILL.md](.claude/skills/ct-ai-dlc/SKILL.md)
- Phase 詳細: [.claude/skills/ct-ai-dlc/references/](.claude/skills/ct-ai-dlc/references/)
- 中間成果物: [docs/ai-dlc/README.md](docs/ai-dlc/README.md)

### Codexレビュー自動対応（GitHub Actions）

OpenAI Codex（ChatGPT の GitHub 連携 = `chatgpt-codex-connector[bot]`）の自動コードレビューが付いた PR に対し、Claude が各指摘を解析して**対応案をレビュースレッドへ返信**するワークフローです。返信には GitHub ネイティブの `suggestion` ブロックを使うため、開発者は内容を確認して「**Commit suggestion**」を押すだけで PR ブランチへ反映できます。

> SIRIUS本体の同名ワークフロー（`docs/ai-dlc/2026-07-15-codex-auto-review-migration/` で設計）を参考に移植したものです。

#### 前提: Codexレビューの有効化（コード外設定）

- ChatGPT の GitHub 連携（`chatgpt-codex-connector`）をこのリポジトリにインストールし、アクセスを許可する（GitHub org の管理者作業）
- ChatGPT の Codex settings でこのリポジトリの Code review を有効化し、**Automatic reviews を ON** にする（OFF の場合は PR コメントで `@codex review` とメンションした PR のみレビューされる）
- レビュー方針はリポジトリ直下の [AGENTS.md](AGENTS.md) の「Review guidelines」セクションでカスタマイズできる（`.meta` 指摘除外・日本語指摘などを指定済み）

#### 仕組み

- トリガー: `.github/workflows/codex-review-respond.yml`
  - `pull_request_review: [submitted]`（Codex bot がレビューを submit したとき）
  - `workflow_dispatch`（`pr_number` 入力でドライラン）
- 処理本体: `.github/scripts/codex-respond.mjs`（Node から Claude Code CLI を `-p` ヘッドレス実行）
- 認証: Claude Code CLI を **サブスク OAuth トークン**（`claude setup-token` で発行・`CLAUDE_CODE_OAUTH_TOKEN`）で認証。従量課金の API キーは使わない（後述）
- スクリプト取得: ワークフローは checkout を持たない（後述の承認ゲート）ため、`codex-respond.mjs` を workflow と同一 commit（`github.workflow_sha`）から `gh api` で取得し `/tmp` で実行する
- 起動条件: Codex bot（`user.type == "Bot"` かつ login に `codex-connector` を含む）由来のレビューのみ。人間レビュー・fork PR・close 済み PR は対象外
- 各指摘への対応:
  - **suggestion**: 単一ファイル・行アンカー範囲内で直せる → `suggestion` ブロック付きで返信（人間が「Commit suggestion」で適用）
  - **manual**: 複数ファイル/範囲外に及ぶ → 参考差分を提示し手動適用を促す
  - **defer**: 誤検知・対応不要 → 見送り理由を返信
  - **`.meta` 指摘**: 指摘が `.meta` ファイルに紐づく場合は CLI を呼ばず **自動で defer 固定**（`.meta` は手編集対象外のため）。「手編集対象外」である旨を返信に明示する
  - レビュー総評（行アンカー無し）→ PR 全体コメントとして対応方針を投稿
- 冪等性: 返信に `<!-- claude-codex-respond:{id} -->` マーカーを埋め込み、再実行時の二重返信を防止。Codex レビューが無い PR では CLI を呼ばず正常終了

#### 承認ゲート（重要）

このワークフローは **checkout も `git push` も一切持ちません**。修正の PR ブランチへの反映は、開発者が「Commit suggestion」を押すこと（= GitHub による人間操作のコミット）でのみ発生します。ワークフロー自身に push 経路が存在しないため、**承認前にコードが push される経路が構造的に存在しません**。

#### 必要な Secrets

| Secret | 用途 | 登録 |
|---|---|---|
| `CLAUDE_CODE_OAUTH_TOKEN` | Claude Code CLI のサブスク OAuth 認証 | **要登録**（下記手順で発行） |

コメント投稿には既定の `GITHUB_TOKEN`（`pull-requests: write` 権限）を使用します。SIRIUS本体では専用の GitHub App トークンを使っていますが、このリポジトリには同等の App 基盤が無いため簡略化しています（コメントの表示名が `sirius-github-app[bot]` ではなく `github-actions[bot]` になる点のみ差異）。

`CLAUDE_CODE_OAUTH_TOKEN` は、Claude サブスク（Pro / Max）でログイン済みのローカル環境で `claude setup-token` を実行して発行した長期トークンを登録します（CLI は同名の環境変数から自動認証します）。

```bash
claude setup-token                                  # ブラウザ認証 → 長期トークンが表示される
gh secret set CLAUDE_CODE_OAUTH_TOKEN -R CyberAgentGameEntertainment/GraphicsAcademyLight   # 表示されたトークンを貼り付け
```

モデルは CLI 既定（サブスクの既定モデル）を使い、`CLAUDE_MODEL` 環境変数で上書きできます。

### PR レビュー依頼について

Phase 4 (Review) では、GitHub PR にレビュアーを設定し、チームで使っている連絡手段でレビュー依頼を送ります。

依頼の送り先や書式はチームごとに異なるため、このリポジトリにはレビュー依頼スキルを同梱していません。
自分のチームで運用する場合は、「PR 情報の取得 → 変更概要の生成 → レビュアー選択 → プレビュー確認 → GitHub レビュアー設定＋依頼送信」という流れをスキル化すると、AI-DLC の Phase 4 をそのまま自動化できます。

### パッケージ設計情報スキル

各 SiriusPackages パッケージの設計詳細をコンテキストとして読み込むスキルです。
パッケージの修正・拡張時に Claude Code が設計を理解した上で作業するために使用します。

| スキル | 対象パッケージ | 主な用途 |
|--------|--------------|---------|
| `/ct-pkg-sirius-core` | Sirius.Core | 共通hlsl（ScreenSpaceUtil/CoreUtil等）、共通基盤の変更 |
| `/ct-pkg-sirius-postprocessing` | Sirius.PostProcessing | ポストエフェクト（DirectionalBlur/RadialBlur/RotationBlur/HeatDistortion）の変更 |
| `/ct-pkg-sirius-devsupport` | Sirius.DevSupport | リグレッションテスト、シェーダー性能計測、GPUプロファイラの変更 |

各パッケージには `CLAUDE.md`（静的なルール・制約）も配置されており、パッケージ内のファイルを編集する際に自動で読み込まれます。

### パッケージドキュメント更新（/ct-update-pkg-docs）

上記のパッケージ設計情報スキル（`CLAUDE.md` / `SKILL.md`）を最新のソースコードに基づいて更新するスキルです。

#### 使い方

```
/ct-update-pkg-docs
```

- 実行すると更新対象パッケージをチェックボックスで選択
- 各パッケージのソースコードを読み取り、既存ドキュメントとの差分を分析
- 更新案をパッケージごとに提示し、承認後に適用

### ワークショップ演習スキル（/workshop-ai-dlc）

学生向け AI 駆動グラフィックスプログラミングワークショップで使用するスキルです（詳細: [docs/workshop/](docs/workshop/)）。
演習ごとに引数を変えて呼び出すことで、AI-DLC フロー（AI が生成 → 人間がレビュー）を体験できます。

#### セットアップ

追加インストールは不要です。スキルは `.claude/skills/workshop-ai-dlc/` にコミット済みです。

#### 使い方

```
/workshop-ai-dlc rotation-blurの実装
/workshop-ai-dlc directional-blurの高品質化
```

**Part 2（RotationBlur 新規実装）:** AI が事前に用意した Intent / Plan に従って RotationBlur のシェーダー・スクリプトをリアルタイムで生成し、SiriusPackages に配置します。受講者は生成されたコードをレビューして問題を発見・修正します。コンパイル確認後、シーンへの配線（Volume Profile・Renderer Feature の設定）は AI が代行せず、Unity Editor 上で受講者が手動で行う手順が案内されます（専用スキル `/workshop-rotation-blur-setup`（後述）は自動連鎖せず、明示的に依頼された場合のみ使用）。

- 発見する問題: 横長画面で楕円形になる視覚的バグ（アスペクト比補正なし）、`float` 精度のパフォーマンスバグ
- 正解コード: `docs/workshop/answers/RotationBlur.shader`

**Part 3（DirectionalBlur 高品質化）:** AI が事前に用意した高品質化 Plan に従って DirectionalBlur を改修します。受講者はテストで問題を発見・修正します。

- 発見する問題: サンプリング増加による GPU 負荷（Mali Offline Compiler で検出）、合成式変更によるビジュアルデグレ（リグレッションテストで検出）
- 正解コード: `docs/workshop/answers/DirectionalBlur.shader`

#### 復元

スキル実行後、表示される rollback 手順にしたがって SiriusPackages を元の状態に戻してください。演習ファイルはコミットしないこと。

### RotationBlur シーン設定スキル（/workshop-rotation-blur-setup）

`/workshop-ai-dlc rotation-blurを実装`（Part 2）で生成した `RotationBlurVolume` / `RotationBlurRenderPass` は、シーンの Volume Profile と Renderer Feature に配線するまで Unity 上で確認できません。この配線は毎回同じ手順のため、探索を行わず固定レシピで実行する専用スキルです。

**注意:** `/workshop-ai-dlc` の通常フローでは、この配線は AI が代行せず Unity Editor 上での手動手順を受講者に案内する方針です。このスキルは Part 2 の Phase 4 から自動的には呼び出されません。ユーザーが自動配線を明示的に依頼した場合にのみ使用してください。

#### セットアップ

追加インストールは不要です。スキルは `.claude/skills/workshop-rotation-blur-setup/` にコミット済みです。

#### 使い方

```
/workshop-rotation-blur-setup
```

ユーザーから「自動で配線して」等の明示的な依頼があった場合にのみ実行してください。前提となる C# ファイル(`RotationBlurVolume.cs` など)が未生成の場合は、先に `/workshop-ai-dlc rotation-blurを実装` を実行してください。

行う操作:

- `Assets/Demo/Workshop_RotationBlur/Workshop_RotationBlur/Global Volume Profile.asset` に `RotationBlurVolume` コンポーネントを追加
- `Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset` の `SiriusPostProcessingFeature` で `allowRotationBlurPostProcess` を有効化
- 素の `uloop compile` で検証（`uloop execute-dynamic-code` は使わない。過去に権限クラシファイアに拒否されトークンが無駄になったため、YAML の直接編集で完結させる）

すでに配線済みの場合は各 Step をスキップするため、複数回実行しても安全です（冪等）。

#### 復元

`/workshop-ai-dlc` Part 2 の Phase 6 の rollback 手順に、このスキルが変更する 2 アセットの `git checkout` が含まれています。
