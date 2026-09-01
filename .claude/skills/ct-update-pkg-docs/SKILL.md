---
name: ct-update-pkg-docs
description: "SiriusPackagesのCLAUDE.mdとSKILL.mdを最新のソースコードに基づいて更新する"
disable-model-invocation: true
---

# SiriusPackages ドキュメント更新スキル

SiriusPackages の各パッケージに配置された CLAUDE.md（ルール・制約）と SKILL.md（設計詳細）を、最新のソースコードに基づいて更新する。

## 対象パッケージとファイルパス

| パッケージ | CLAUDE.md | SKILL.md |
|-----------|-----------|----------|
| Sirius.Core | `SiriusPackages/Sirius.Core/CLAUDE.md` | `SiriusPackages/Sirius.Core/.claude/skills/ct-pkg-sirius-core/SKILL.md` |
| Sirius.PostProcessing | `SiriusPackages/Sirius.PostProcessing/CLAUDE.md` | `SiriusPackages/Sirius.PostProcessing/.claude/skills/ct-pkg-sirius-postprocessing/SKILL.md` |
| Sirius.DevSupport | `SiriusPackages/Sirius.DevSupport/CLAUDE.md` | `SiriusPackages/Sirius.DevSupport/.claude/skills/ct-pkg-sirius-devsupport/SKILL.md` |

---

## 実行フロー

### Step 1: 対象パッケージの選択

AskUserQuestion（**multiSelect: true**）で更新対象を選択する:
- Sirius.Core
- Sirius.PostProcessing
- Sirius.DevSupport

### Step 2: 各パッケージについて以下を実行

#### 2-1: 現状把握

1. 既存の CLAUDE.md と SKILL.md を Read ツールで読む
2. パッケージの主要ソースファイルを読む:
   - `package.json` — 依存関係の変化
   - `Runtime/**/*.asmdef` — アセンブリ参照の変化
   - Feature クラス (`*Feature.cs`) — パス登録の変化
   - Volume クラス (`*Volume.cs`) — 新しいVolume追加
   - 追加・変更されたファイル（git diff で検出）

#### 2-2: 差分分析

以下を確認し、更新が必要な箇所を特定する:
- 新しいクラス/パスの追加・削除
- 既存パスの登録条件変更
- パッケージ間の依存関係変更
- 新しいゴッチャ・制約の発見

変更が見つからない場合は「変更なし」と報告してそのパッケージをスキップする。

#### 2-3: CLAUDE.md 更新案の作成

**後述の CLAUDE.md ポリシーに厳密に従って**更新案を作成する。
変更箇所のみを差分で表示する（全文書き直しではなく、追加・変更・削除を明示）。

#### 2-4: SKILL.md 更新案の作成

**後述の SKILL.md ポリシーに厳密に従って**更新案を作成する。
変更箇所のみを差分で表示する。

SKILL.md の frontmatter（name, description）は変更しない。

#### 2-5: ユーザー確認

各パッケージごとに更新内容のサマリを表示し、AskUserQuestion で確認する:
- 適用する
- 修正して適用する（フィードバックを受けて再作成）
- スキップする

#### 2-6: ファイル書き込み

承認されたら Edit ツールで更新を適用する。

### Step 3: 完了報告

更新したファイル一覧と主な変更点を表示する。

---

## CLAUDE.md ポリシー（厳守）

CLAUDE.md はパッケージ固有の **ルール・制約・ゴッチャ** のみを記載するファイル。

### 含めるべきもの

- **MUST**: 違反するとバグ・ビルドエラーになるルール
- **IMPORTANT**: 知らないと間違いやすいゴッチャ・落とし穴
- 非自明な制約（コードを読んだだけでは気づきにくいもの）

### 含めてはならないもの

以下は **絶対に含めない**:
- パッケージ情報（名前、バージョン、依存関係）— `package.json` を読めばわかる
- アセンブリ構成 — `.asmdef` を読めばわかる
- クラスやインターフェースの説明 — ソースを読めばわかる
- 設計パターンの説明 — SKILL.md の役割
- 「〜を担当する」「〜を管理する」のような機能説明

### フォーマット

- **MUST** / **IMPORTANT** で重要度を明示する
- 見出し（`##`）でカテゴリ分け
- 箇条書きで簡潔に記述
- **50行以内**を目標とする

### 検証テスト

各行に対して「この行を削除したら、Claude がこのパッケージで作業する際に間違いを犯すか？」を自問する。答えが No なら削除する。

---

## SKILL.md ポリシー（厳守）

SKILL.md はパッケージの **設計詳細** を記載するファイル。コードの1ファイルからは読み取れない、アーキテクチャレベルの知識を提供する。

### 含めるべきもの

- **コンポーネント間の関係性**: パッケージ横断の依存、暗黙的な連携（例: Bloom→GBuffer）
- **パス登録順序と条件のサマリテーブル**: 複数パスの全体像
- **設計パターンの「なぜ」**: AllowFlagパターン等の規約の理由
- **非自明な設計判断の理由**: なぜこの実装になっているかの背景
- **ワークフロー/パイプライン**: 複数Stepにまたがる処理の流れ

### 含めてはならないもの

以下は **絶対に含めない**:
- ASCIIディレクトリツリー — `Glob` ツールで取得可能。最も陳腐化しやすい
- ソースコードのコピペ（インターフェース定義、クラス定義等）— ソースを読めばわかる
- Enum一覧テーブル — ソースを読めばわかる
- フィールド/プロパティの列挙 — ソースを読めばわかる
- CLAUDE.md と重複する内容 — ルール・制約は CLAUDE.md に一本化

### フォーマット

- frontmatter の name / description は既存のものを維持する
- テーブル形式はパスサマリ等の全体像把握に有効。積極的に使う
- コードブロックは設計パターンの例示（数行以内）にのみ使用
