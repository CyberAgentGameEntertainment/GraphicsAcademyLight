# GraphicsRegressionTest の構成

Editor専用の別asmdef（`Sirius.DevSupport.GraphicsRegressionTest.Editor`）。

2つのテストプロバイダがある:
- **CameraTestProvider**: `CameraTestConfig` (ScriptableObject) に定義されたカメラ設定でテスト。`CameraPrefabTestContext` がPrefab配置→撮影→比較を管理
- **TestSceneProvider**: シーン単位テスト。`TestSceneParameter` で対象シーンとパラメータを定義

画像比較は **NVIDIA FLIP** アルゴリズム（`NvidiaFlip`）を使用。知覚的な差異を検出し、`FlipAssert` が閾値ベースで合否判定。
