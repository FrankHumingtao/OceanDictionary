using System.IO;
using UnityEngine;

namespace Scrpits.FFT_Ocean
{
    public static class TextureSaver
    {
        public static void SaveRenderTextureToPNG(RenderTexture rt, string filePath)
        {
            RenderTexture currentRT = RenderTexture.active;

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);

            Object.Destroy(tex);
            RenderTexture.active = currentRT;
        }
    }
}