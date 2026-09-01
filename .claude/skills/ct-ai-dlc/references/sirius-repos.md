# リポジトリ構成マップ

Phase 2 (Inception) で UoW の配置先を決める時、Phase 3 (Construction) で実装に入る時に参照する。

このファイルは **「どこに何を置き、どうコミットを分けるか」** に特化した参照資料。
規約事項（`.meta` の扱い、ブランチ運用など）は CLAUDE.md に一元化されているので、そちらを参照すること（末尾の「規約は別ファイル」セクション参照）。

## ディレクトリの役割

本リポジトリは単一リポジトリ構成で、次の3系統から成る。

| ディレクトリ | 役割 | 主な内容 |
|---|---|---|
| `SiriusPackages/` | UPM パッケージ実装本体 | `Sirius.Core` / `Sirius.PostProcessing` / `Sirius.DevSupport` |
| `SiriusAssets/` | 配布アセット | `Sirius.Core.Assets` |
| ルート直下 | デモアプリ + AI エージェント作業場 + テストハーネス + ドキュメント | `Assets/Demo/`, `Assets/Tests/`, `Assets/Settings/`, `.claude/`, `docs/`, `LocalPackages/`, `Packages/` |

`Packages/manifest.json` は `file:../SiriusPackages/...` / `file:../SiriusAssets/...` でこれらをローカル参照している。

## 編集対象と配置先のマトリクス

UoW ごとに「何を編集するか」が決まれば、配置先がこの表から決まる。

| 作業 | 編集対象 | 配置先 |
|---|---|---|
| Intent 起票 | intent.md | `docs/ai-dlc/<date>-<topic-slug>/` |
| Inception | plan.md | `docs/ai-dlc/<date>-<topic-slug>/` |
| パッケージ実装 | C# / Shader / hlsl | `SiriusPackages/Sirius.*/` |
| 配布アセット | mat / asset / prefab | `SiriusAssets/*` |
| デモシーン | Scene / Animation / Material / FBX | `Assets/Demo/<feature>/` |
| テスト期待画像 | png | `Assets/Tests/SuccessfulImages/` |
| 公開ドキュメント | README | `README.md` |
| Claude 資産 | SKILL.md / agent 定義 | `.claude/skills/`, `.claude/agents/` |
| 開発者向け文書 | README_DEVELOPERS | `README_DEVELOPERS.md` |
| tarball 検証 (一時) | .tgz | `LocalPackages/` → **コミットしない** |
| manifest 切替 (一時) | manifest.json | `Packages/manifest.json` → **コミットしない** |

## コミットの分け方

単一リポジトリなので PR は原則 1 本。ただし **コミットは性質ごとに分ける**と後から追いやすい。

| 機能の性質 | コミットの分け方 |
|---|---|
| パッケージ単独（既存 Pass 内部最適化など） | 実装 1 コミット |
| パッケージ + デモ | パッケージ実装 / デモシーン の 2 コミット |
| パッケージ + アセット + デモ | 3 コミット |
| スキル / エージェント追加・改善 | `.claude/` + `README_DEVELOPERS.md` で 1 コミット |
| AI-DLC フロー自体の改善 | 本スキルや reference の編集で 1 コミット |

`SiriusPackages/` のパッケージ実体を変えた場合は、該当パッケージの CLAUDE.md / SKILL.md 更新を **実装とは別コミット**にする。

## 規約は別ファイル

このファイルでは規約を再掲しない。実装着手前に以下を必ず確認すること（CLAUDE.md からも参照されている）:

| 規約 | 参照先 |
|---|---|
| `.meta` ファイルの AI 編集禁止 | [CLAUDE.md](../../../../CLAUDE.md) |
| PR 作成時は `origin/main` から新ブランチ | [CLAUDE.md](../../../../CLAUDE.md) |

迷ったら CLAUDE.md を読む。このファイルは「**どこに何を置き、どうコミットを分けるか**」だけを答える。
