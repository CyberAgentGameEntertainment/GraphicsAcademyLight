# DirectionalBlur 高品質化 プラン（第一稿）

## 変更方針

既存の `DirectionalBlur.shader` を以下のように改修して「高品質モード」を実現する。

## 学生の判断

- **サンプリング数**: [6 / 8]（Phase 1 の選択）

## 具体的な変更

### 1. Properties のデフォルト値変更

`_DirectionalBlurSamplingCount` のデフォルト値を `3` から学生の選択値に変更:

```hlsl
_DirectionalBlurSamplingCount("Sampling Count", Int) = [6 or 8]
```

### 2. シェーダー内のサンプリングループ変更

uniform の `_DirectionalBlurSamplingCount` を使う既存ループを、
高品質固定サンプリング数を使うループに変更する:

```hlsl
// 高品質固定サンプリング
int hq_count = [6 or 8];
for (int n = 0; n < hq_count; n++)
{
    const [float/half] displacement = DIRECTIONAL_BLUR_SAMPLING_OFFSET(n);
    // 既存のサンプリング処理をそのまま使用
}
```

### 3. 変更しないもの

- Inverse Sampling のロジックは既存コードをそのまま維持
- Shader 名・Property 名・テクスチャ参照はそのまま
- アスペクト比サンプリングのロジックはそのまま
- Composite ロジックはそのまま
