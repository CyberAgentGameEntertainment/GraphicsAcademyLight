# Sirius.DevSupport

Sirius 向けの開発支援パッケージ。各ツールは相互に独立しており、他の Sirius パッケージへの依存なく単体で利用できる。

| ツール | 概要 | 外部依存 |
|---|---|---|
| EasyGPUProfiler | GPU プロファイリング（CTEasyGPUProfiler / SiriusDevSupportFeature / URPExtensions） | URP / RP Core / InputSystem |
| GraphicsRegressionTest | グラフィックスのリグレッションテスト | URP / Timeline / Cinemachine（任意） |
| ShaderPerformanceAnalysis | Shader のパフォーマンス解析 | Newtonsoft.Json |

## アセンブリ構成

各ツールは「機能フォルダ直下に Runtime / Editor + 各自の asmdef」形式で構成される。利用側は **使うツールの asmdef のみ** を自分の asmdef に参照追加する。

| ツール | Runtime asmdef | Editor asmdef |
|---|---|---|
| EasyGPUProfiler | `Sirius.DevSupport.EasyGPUProfiler` | `Sirius.DevSupport.EasyGPUProfiler.Editor` |
| GraphicsRegressionTest | — | `Sirius.DevSupport.GraphicsRegressionTest.Editor` |
| ShaderPerformanceAnalysis | — | `Sirius.DevSupport.ShaderPerformanceAnalysis.Editor` |

## 利用方法

- **独自 asmdef を持たないプロジェクト**（既定の Assembly-CSharp）: 全モジュールが `autoReferenced` のため、パッケージを導入するだけで参照追加なしに各ツールの public 型へアクセスできる。
- **独自 asmdef を持つプロジェクト**: Unity の asmdef 参照は**非推移的**なため、使うツールの asmdef を個別に参照追加する。1 つの asmdef を参照すれば全モジュールが入る、という集約はできない。

### 注意点

- **EasyGPUProfiler は URP 必須**: asmdef の `defineConstraints: ["HAS_URP"]` により、URP の無いプロジェクト（カスタム SRP 等）ではアセンブリごとコンパイル対象から除外され、エラーなく無効化される。「導入したのに使えない」場合は URP の有無を確認すること。
