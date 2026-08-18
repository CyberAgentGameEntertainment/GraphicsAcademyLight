using UnityEngine;

namespace Sirius.PostProcessing
{
    public static class GodRayHelper
    {
        public static Vector2 CalculateRadialBlurCenter(Light light, Camera camera)
        {
            var lightDir = -light.transform.forward;

            var viewMatrix = camera.worldToCameraMatrix;
            var projMatrix = camera.projectionMatrix;

            var lightViewDir = viewMatrix.MultiplyVector(lightDir);
            var lightClipDir = projMatrix.MultiplyVector(lightViewDir);

            // (-1, 1) -> (0, 1)
            var radialBlurCenter = new Vector2(
                lightClipDir.x * 0.5f + 0.5f,
                lightClipDir.y * 0.5f + 0.5f);

            return radialBlurCenter;
        }
    }
}
