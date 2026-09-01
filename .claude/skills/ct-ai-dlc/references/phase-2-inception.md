# Phase 2: Inception（設計と UoW 分解）

このフェーズでは、Intent を「どう作るか」に落とす。
複数の設計案を発散させて1つを選び、実装単位 (UoW: Unit of Work) に分解する。

## 成果物

`docs/ai-dlc/<date>-<topic-slug>/plan.md` (テンプレ: [../assets/plan-template.md](../assets/plan-template.md))

## 入力

- `docs/ai-dlc/<date>-<topic-slug>/intent.md` (Phase 1 の成果物)
- `SiriusPackages/<pkg>/CLAUDE.md`, `SKILL.md` (対象パッケージがある場合)
- [./sirius-repos.md](./sirius-repos.md) (リポジトリ役割マップ)

## 手順

### Step 1: 既存実装の深掘り調査（チェックリスト）

設計案を考える前に必ず以下を完了する。**1 つでも飛ばすと Phase 2 の途中で「実装の制約条件を見落として案を出し直し」になる確率が高い**。本セッションでこの遅延が多発した実例があり、AI には「関連ファイル名を列挙して終わり」になりがちなので、チェックリストとして明示する。

```
□ 直接変更する関数 / クラスの【本体ロジック】を Read（シグネチャだけでなく中身まで）
□ その関数を呼ぶ側 (1-hop caller) を Read
□ その関数が依存する側 (1-hop callee) を Read
□ 関連するデータの【書き込み元】を特定し Read
    例: シェーダで読む uniform があれば、その値を書く C# / 上流 Pass / 別シェーダ
    例: GBuffer チャネルを読むなら、そのチャネルを書く側のシェーダ
□ データのエンコード / 命名 / 単位の規則を 1 文で言語化
    例:「ObjectID は `(_ObjectID + 1.1) / 64.0` で UNorm エンコード、最大 64 体」
□ 設定値の【入力経路】を特定（MonoBehaviour SerializeField / VolumeComponent / Material Property / ProjectSettings のどれか）
□ 同種のユーザー設定値が既存にあれば、その【公開階層】を確認（VolumeComponent か MonoBehaviour か等）
□ シェーダ変更を伴う場合: 該当シェーダの `#pragma multi_compile*` の使い分け頻度を grep でカウント
□ 既存パターンを 1 段落で言語化する（次項参照）
```

#### 既存パターンを 1 段落で言語化する

チェックリスト最後の項目が特に重要。「この機能領域の既存コードは X の原則で動いている」を 1〜2 文で書き出す。

例:
- 「RenderPass は **Volume 駆動の自己完結原則**。パスは Volume の値だけを見て動き、シーン側の状態を参照しない」
- 「Sirius.PostProcessing の Volume 系は **Inspector + script 両用パターン**。`Use*` 系プロパティに getter/setter ペアを持つ」
- 「RotationBlur.shader は **頂点処理が単純で fragment 中心**。Blit 前提で vertex は固定、負荷は fragment のサンプル数で決まる」

この言語化があれば、後続の設計案で「既存原則に沿うか / 逸脱するか」が即座に判断できる。

調査範囲が広い場合は Explore エージェントを並列起動して効率化できる（読み取りのみなので安全）。ただし **本 PR ではサブエージェントは使わない方針** のため、メインでの調査で十分なケースが大半。

### Step 2: 設計案を 2〜3 案発散（既存パターンとの整合性を必ず評価）

すぐに 1 案に絞らず、**複数案を発散** させてから比較する。
案ごとに「採用したらどう実装するか」「どこで詰まるか」を簡潔に書く。

例:
- **Plan A**: Volume 直接駆動（Timeline なし） — シンプルだが Timeline 同期の要求と整合しない
- **Plan B**: Timeline Clip + Mixer + Volume — Mixer の blend が複雑だが要求に最も適合
- **Plan C**: ScriptableObject エフェクトリスト — Mixer 構造を再現する Editor が複雑化

#### 各案を 2 軸で評価する

1. **既存原則との整合**: Step 1 で言語化した「この領域の既存原則」と整合するか / 逸脱するか
2. **新規導入インフラ**: 既存にない仕組み（新規 keyword / 新規 varying / per-instance データ経路 / 新規 RT / 新規 attachment 構成 / 新規ドローコール経路）をどれだけ追加するか

#### 採用判断の優先順

1. **既存原則に整合する案を最優先する**。既存と同じ仕組みで実現できるなら、それを選ぶ
2. 既存原則に逸脱する案を採用する場合、**明示的な理由** が必要
   例: 性能要件で既存パターンが破綻する、新規ユースケースで既存パターンが構造的に対応できない、等
3. 「新しい仕組みを足したほうがエレガント / 拡張性がある」は採用理由として **弱い**。既存原則を尊重したほうが、レビュー時間 / 学習コスト / 将来の改修コストが小さい

「既存原則と異質である」ことは **客観的な棄却理由** として書いてよい（既存設計と異質な案はそれ自体がメンテナンスコスト要因）。

### Step 3: 採用案を選び、棄却案と理由を記録、インタフェース層を明示

採用は 1 案だが、**棄却案も plan.md に残す**。後から「なぜこの設計にしたのか」が辿れる。

棄却理由は具体的に書く:
- ❌ 「複雑だから」← 主観的
- ✅ 「Mixer の blend 構造をカスタム Editor で再現する必要があり、UoW 数が 2 倍になる」← 客観的
- ✅ 「既存の ScreenSpaceOutline は per-instance 情報を一切使わない GBuffer 自己完結設計であり、per-instance 配線を新規導入する本案はその原則と異質」← 既存原則整合を理由にしてよい

#### インタフェース層の選定を明示する

採用設計セクションに以下を明記する（intent.md の動的軸要件 + Step 1 で確認した既存の公開階層と整合させる）:

- **公開階層**: VolumeComponent / MonoBehaviour SerializeField / Material Property / ProjectSettings のどれに公開するか
- **その階層を選んだ理由**:
  例:「intent で『シーン入りで 1 回 ON にする半動的運用』と明記されており、これは既存の `useDistanceFade` と同じ階層 (`HeatDistortionVolume`) で十分。per-object 階層に分散させる理由がない」
  例:「intent で『完全動的（フレーム単位の切替）』と指定されており、`MaterialPropertyBlock` 経由で per-draw に値を流す経路が必要」
- **API パターン**: getter / setter の有無、命名、デフォルト値などを既存パターン（例: `Use*` 系の bool 公開）と揃える
- **配置場所**: 既存セクションのどこに挿入するか（既存ヘッダの順序を尊重）

### Step 4: UoW に分解

採用設計を実装単位 (UoW) に分解する。各 UoW は:

- **対象**: 編集ファイル / ディレクトリ
- **依存**: 他のどの UoW が完了している必要があるか
- **並列可能か**: 領域独立な UoW は同時実装できる（本 PR では並列実行はしないが、依存関係を明示しておくと将来役立つ）
- **担当**: AI 実装 / 人間作業 / 両方
  - **人間作業推奨**: ShaderGraph 編集、Timeline アセット配置、FBX 配置などは Unity Editor 上で対話的にやる方が早い

UoW 粒度の目安: **1 UoW = コミット 1〜2 個分**。これより大きいと並列化や Phase 3 での進捗管理が難しくなる。

### Step 5: コミット先の判断

各 UoW がどのリポジトリにコミットされるかを [./sirius-repos.md](./sirius-repos.md) で確認し、plan.md に明記する。
SIRIUS / SiriusPackages / SiriusAssets のいずれに着地するかで PR の本数が決まる。

### Step 6: テンプレートに沿って plan.md を書く

```bash
cp .claude/skills/ct-ai-dlc/assets/plan-template.md \
   docs/ai-dlc/<date>-<topic-slug>/plan.md
```

埋める項目:
- 採用設計（**公開階層 / インタフェース設計** を含む）
- 棄却した代替案と理由（既存原則との整合性を観点に含めてよい）
- UoW 一覧（対象 / 依存 / 担当 / コミット先）
- 並列可能ペア
- 触ってはいけないファイル

### Step 7: 内容の最終確認

ユーザーに plan.md の概要を提示する。
**全文の貼り付けは不要**。要約 + 「全文は `docs/ai-dlc/.../plan.md` 参照」で十分。

ユーザーが UoW 分解・採用案・棄却理由について意見を持つ可能性が高いので、
**「ExitPlanMode で plan.md を直接編集してもらう」運用が望ましい**。

### Step 8: 完了メッセージ

```
✅ Phase 2 (Inception) 完了
   出力: docs/ai-dlc/<date>-<topic-slug>/plan.md
   採用設計: <Plan B の名前>
   UoW: <N> 件

次は Phase 3 (Construction) です。
コンテキストをリセットしてから実行することを推奨します（任意）:

   /clear
   /ct-ai-dlc <トピック>を実装
```

## このフェーズで AskUserQuestion を使う場面

- ✅ 採用案が複数の妥当な選択肢に分かれる時
- ✅ ユーザーが特定の UoW を「自分でやる / AI に任せる」を選ぶ時
- ❌ 「この UoW 分解で良いですか？」のような全体確認（plan.md を見せて誘導すれば十分）

## 注意事項

- **既存実装を読まずに設計提案しない** — Step 1 のチェックリストをすべて埋めるまで Step 2 に進まない。これを破ると、Phase 2 内で何度も「既存パターンと異質だった」「エンコード式を見落としていた」で案を出し直すことになる
- **既存原則に逸脱する案を採用する時は理由を明示する** — 「新しい仕組みを足したほうが拡張性がある」だけでは不十分。intent の要件 / 性能制約 / 構造的限界など客観的な裏付けを書く
- **公開階層の選定は intent の動的軸を入力にする** — intent.md に「静的 / 半動的 / 完全動的」が書かれていなければ Phase 1 に戻って確認する。動的軸の前提ズレは plan 全体のやり直しに繋がりやすい
- **棄却案を消さない** — 「なぜこの設計か」の歴史的記録。後で「別案で行けば良かった」と気付いた時に参照する
- **人間作業 UoW を明示する** — ShaderGraph や FBX 配置を AI に任せようとすると Phase 3 で破綻する
- **コミット先を明示する** — Phase 3 で「これどこにコミットするんだっけ」となるのを防ぐ
- **Intent の SMAV 不足を後から修正しない** — もし intent.md の Completion Criteria が曖昧で Phase 2 が進まない場合は、Phase 1 に戻る判断をする
