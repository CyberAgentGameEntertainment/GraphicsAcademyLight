## Unity開発
- **MUST** `.meta`ファイルはAIが生成・編集してはならない。`.meta`ファイルの生成はUnity Editorに任せること。AIが生成したGUIDはプロジェクト内の既存GUIDと衝突するリスクがある

## Git運用
- **MUST** サブモジュールポインタの変更をコミットしてはならない。サブモジュールの更新はユーザーが手動で管理する

## コードレビュー対応
- **MUST** レビュー指摘を盲目的に受け入れない。修正前に、指摘の主張が事実かどうかを実際のソースコード・コマンド実行・ファイル存在確認等で検証すること
- 検証の結果、指摘が正しくない場合はその根拠を示して報告する

## Claude Code スキル
- `.claude/skills/` にスキルを新規追加・更新した場合は、`README_DEVELOPERS.md` の「Claude Code スキル」セクションにもセットアップ手順と使い方を追記すること

## uloop コンパイル運用
SIRIUS本体での実測（uloop 2.1.10）に基づく。コンパイル完了までの所要時間は uloop のバージョンではなく**使い方**で決まる。手動より大幅に遅くなる主因は、重いフラグの常用・コマンド分割・ロック解除直後の早撃ちにある。
- **MUST** エラー確認は素の `uloop compile` 一発で行うこと。応答JSONの `Errors[]`（message/file/line 付き）を直接読む。`clear-console`→`compile`→`get-logs` のように複数コマンドへ分割しない（各回コールドNodeプロセス＋接続が積み上がるうえ、3番目の `get-logs` が compile の誘発した非同期リロードに衝突して `Domain Reload in progress` を返し**ログ取得自体に失敗する**。エラー情報は素のcompile応答の `Errors[]` に含まれている。実測：分割サイクル中央値 約5.5秒 vs 素のcompile 約3.6秒）
- **MUST** `--force-recompile` を常用しないこと。全アセンブリのクリーンビルド（実測 約36〜56秒、差分コンパイルの約10倍）になる。asmdef変更・codegen・ビルドキャッシュ破損が疑われる場合のみ使う。特に `--wait-for-domain-reload true` と併用すると CLI 待機が 90秒タイムアウト（`Compile wait timed out after 90000ms`）に達することがある（実測 3回中1回）
- **MUST** `--wait-for-domain-reload true` をルーチンのエラー確認に付けないこと（実測 約18秒、素のcompile+reloadの約2倍。リロード後の prewarm 子プロセス等が上乗せされる）。次手が `execute-dynamic-code` / `run-tests` / PlayMode などウォームな再ロード後ドメインを要するときのみ付ける
- **SHOULD** 素の `compile` は応答を返した後、**ドメインリロードを最大 ~14秒遅延で非同期開始する**。そのため `Temp/compiling.lock`・`Temp/domainreload.lock` が「消えた＋小バッファ」だけでは、リロード開始前の見かけ上クリアな窓を通過して衝突しうる。2.1.10 はこの衝突を `Compilation is already in progress`（コンパイル中）/ `Unity is reloading (Domain Reload in progress). Please wait a moment and try again.`（リロード中）として**グレースフルに即時返却する（~140ms、クラッシュしない）**。よってエラー確認ループでは、リロード完了待ち（~10〜18秒）より**「撃ってガードが返ったら数百ms待って撃ち直す」（~140ms/回）方が速い**。ただし `run-tests` / `execute-dynamic-code` / PlayMode などリロード完了が前提の次手の前は、ロックが消えるまで待つこと
- **SHOULD** Unity は再起動せずウォーム維持すること（`uloop launch -r` のコールド起動は実測 約60秒/回）
- **INFO** 変更が無い状態での `uloop compile` も ~3秒で `Success:true`（空の `Errors[]`）を返す（再計測では5秒タイムアウト等の abort は再現せず）。ただしC#変更が無ければコンパイルの実体は無いので、シェーダ/アセットのみ変更時の無駄撃ちには注意
