# AI 駆動開発 (AI-DLC) 中間成果物

このディレクトリは [/ct-ai-dlc](../../.claude/skills/ct-ai-dlc/SKILL.md) スキルが生成する **AI 駆動開発フローの中間成果物** を蓄積する領域です。
Phase 1 (Intent) と Phase 2 (Inception) の出力がここに溜まり、Phase 3 (Construction) 以降で参照されます。

## ディレクトリ構成

```
docs/ai-dlc/
├── README.md                          (このファイル)
└── <date>-<topic-slug>/               機能単位フォルダ
    ├── intent.md                      Phase 1 成果物
    └── plan.md                        Phase 2 成果物
```

機能単位フォルダ名は `<YYYY-MM-DD>-<topic-slug>` 形式:

- `YYYY-MM-DD` — Intent 起票日（UTC）
- `topic-slug` — 機能を表す kebab-case 英数字（例: `impactframe-impl`, `hybrid-gi`, `smear-pass-improvement`）

日本語入力 (`impactframe実装`, `ハイブリッドGI`) は `/ct-ai-dlc` スキルが kebab-case に正規化します。

## ファイル命名規則

各機能フォルダ内のファイル名は **artifact 名で固定**:

| ファイル | 意味 | 生成元 |
|---|---|---|
| `intent.md` | 何を・なぜ作るか（Description / Context / Completion Criteria） | Phase 1 (Intent) |
| `plan.md` | どう作るか（採用設計 / 棄却案 / UoW 一覧） | Phase 2 (Inception) |

将来的に Phase 4/5 の成果物を追加する余地があります（`review-notes.md`, `release-report.md` 等）。

## 運用ルール

- **Intent は後続フェーズで変更可** — Inception / Construction のやりとりで要件が変わったら intent.md を更新し、その変更点からフローを再進行する（別フォルダは作らず、plan.md など後続成果物も追従して更新）
- **Plan は実装中に更新可** — 採用設計と実装が食い違ったら plan.md を更新
- **完了したフォルダも残す** — マージ済みの機能でも履歴として保持

## 関連スキル

- [/ct-ai-dlc](../../.claude/skills/ct-ai-dlc/SKILL.md) — AI 駆動開発フロー起点スキル

Phase 4 (Review) のレビュー依頼はチームごとに運用が異なるため、専用スキルは同梱していない。

## 参考

- AWS AI-DLC: AI-Driven Development Life Cycle
