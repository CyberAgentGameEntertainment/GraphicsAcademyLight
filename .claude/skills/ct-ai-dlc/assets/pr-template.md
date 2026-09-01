<!-- ct-ai-dlc PR 本文テンプレート（lean）。Phase 3 Step 7 で各リポジトリの PR 本文に使う。

     方針:
     - lean に保つ。設計根拠・棄却案・全変更ファイル列挙は載せない（それらは plan.md 側に閉じる）。
     - 末尾に "🤖 Generated with..." 等の署名は付けない。
     - <...> を埋め、不要な行（単一リポジトリ時の「関連 PR」等）は削除する。
     - このガイドコメント自体は最終本文から削除する。
     - 書き方の指針: ../references/phase-3-construction.md の「Step 7」を参照。
-->

## 概要

<!-- 何を / なぜ。1〜3 行の箇条書き。
     必ず触れる（該当する場合）:
     - 既定値（デフォルト）の変更と、その切替手段（Volume パラメータ等）
     - 追加アセット（.asset / シーン等）・lockfile（packages-lock.json）差分の意図
     - 最低 Unity バージョンを上げた場合は version メジャー bump 済みである旨
-->
- <変更の要点>

## 設計意図

<!-- レビュー補助。なぜこのアプローチを採ったかを 1〜2 行で簡潔に。
     棄却案・詳細な比較は plan.md に委ね、ここでは要点だけ書いて lean を保つ。 -->
- <採用したアプローチと理由。例: 既存 GBuffer を流用し追加パス・ステンシル増設なし／OFF 時ゼロコスト。詳細は plan.md 参照>

## テスト

<!-- 品質ゲート（MUST）: macOS（iOS）と Windows（Android）の両方で全 AverageTest が成功していること。
     対象は PlayMode ビジュアルリグレッション + EditMode 全件。失敗は最大 3 試行（初回+リトライ2回）で 1 回でも成功なら可。3 試行連続失敗は確定失敗として人間の判断に引き渡す。
     各 OS で実行・確認してから [x] にする。 -->
- [ ] macOS で iOS の全 AverageTest 成功
- [ ] Windows で Android の全 AverageTest 成功
- <その他に行った確認があれば追記。なければ削除>

## 関連

<!-- Intent / Plan へのリンクは常に残す（AI-DLC トレーサビリティ）。
     「関連 PR」行は関連する PR がある場合のみ。なければ削除。 -->
- Intent / Plan: docs/ai-dlc/<date>-<topic-slug>/
- 関連 PR: <例: #YYY, #XXX>
