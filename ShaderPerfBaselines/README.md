# Shader Performance Baselines

シェーダーパフォーマンス回帰検出ツール（`Sirius.DevSupport` の `ShaderPerformanceAnalysis`）が
回帰比較に使う**ベースライン JSON の保管場所**。

`Assets/` の外に置くことで、Unity による不要な `.meta` 生成（`.meta` 汚染）を避けている。

## 運用

1. malioc を導入した環境で Editor バッチ UI（**Window > Sirius > Shader Performance Analyzer**）または
   headless 入口（`ShaderPerfBatchEntry`）を実行する。
2. 「ベースライン更新」または request JSON の `UpdateBaseline: true` で、ここに JSON を生成・コミットする。
3. 以降の解析は、生成済みベースラインと比較して悪化（回帰）を検出する。

ベースラインはヘッダ（**GPU コア / malioc バージョン / Unity バージョン**）が一致するときのみ比較に使われる。
環境が変わったら再生成すること。

## ファイル命名（推奨）

```
<gpu-core>_<unity-version>.json   例: Mali-G715_6000.3.19f1.json
```

> 初期ベースラインは malioc 実行環境（対象 GPU コア + Android Build Support）で生成する必要があるため、
> 本リポジトリには空（プレースホルダ）の状態で導入し、実データは生成時にコミットする。

ツールの詳細は [Shader Performance Analysis ドキュメント](../Documentation/ShaderPerformanceAnalysis/README.md) を参照。
