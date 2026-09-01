# <機能名> 実装計画

<!-- Phase 2 (Inception) で生成するファイル。
     intent.md を入力に、UoW 分解と採用設計を記述する。
     Phase 3 (Construction) で UoW ごとに該当セクションを読む。
-->

## 採用設計

<!-- 採用した設計案の名称と概要。例:
     **Plan B: Timeline Clip + Mixer + Volume**
     - Timeline で Clip を配置、Mixer で blend 計算
     - Volume で Project Settings 切替
     - Frame Buffer Fetch は Sirius.Core 共通 hlsl で吸収
-->

## 棄却した代替案

<!-- 検討したが採用しなかった案と理由。後から「なぜこの設計か」を辿るために残す。
     棄却理由は客観的に書く（「複雑だから」ではなく「UoW 数が 2 倍になる」など）。

     ### Plan A: <名前>
     棄却理由: ...

     ### Plan C: <名前>
     棄却理由: ...
-->

## Units of Work

<!-- 実装単位の一覧。各 UoW に以下を書く:
     - 対象: 編集ファイル / ディレクトリ
     - 依存: 他のどの UoW が完了している必要があるか
     - 並列可能か
     - 担当: AI / 人間 / 両方
     - コミット先: SIRIUS / SiriusPackages / SiriusAssets

     UoW 粒度の目安: 1 UoW = コミット 1〜2 個分
-->

### UoW#1 [<役割>] <依存・並列の注記>

- 対象: <パス>
- 追加 / 変更ファイル: <一覧>
- 依存: <他 UoW or なし>
- 担当: AI / 人間 / 両方
- コミット先: SIRIUS / SiriusPackages / SiriusAssets

### UoW#2 [...]

...

## 並列可能ペア

<!-- 領域独立で同時実装できる UoW の組。本 PR (AI-DLC 導入最小構成) では並列実行はしないが、
     依存関係の理解のため明示しておく。

     例:
     - UoW#2 ‖ UoW#5
     - UoW#8 内部の Clip スクリプト 3 種は並列可
-->

## 触ってはいけないファイル

<!-- 個別 UoW のスコープと無関係に、本機能の実装中に触ってはいけないもの。例:
     - *.meta (既存 GUID 維持、新規は Unity Editor 任せ)
     - Packages/manifest.json のコミット
     - サブモジュールポインタ
-->

## PR 構成

<!-- どのリポジトリに何本の PR が生まれるか。
     references/sirius-repos.md の「1 機能追加で発生する PR の組」を参照。

     例:
     - SiriusPackages PR × 1（パッケージ実装）
     - SIRIUS PR × 1（デモシーン + ドキュメント）
-->
