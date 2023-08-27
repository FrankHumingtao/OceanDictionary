using Unity.VisualScripting;
using UnityEngine;

namespace Scrpits
{
    public class SetGerstnerWave :MonoBehaviour
    {
        public Material waterMaterial;

        void Start()
        {
            if (waterMaterial)
            {
                waterMaterial.SetVector("_GerstnerWave[0]", new Vector4(1.0f, 0.0f, 2.0f, 0.5f));
                waterMaterial.SetVector("_GerstnerWave[1]", new Vector4(0.0f, 1.0f, 2.5f, 0.4f));
                waterMaterial.SetVector("_GerstnerWave[2]", new Vector4(0.7f, 0.7f, 1.5f, 0.3f));
                waterMaterial.SetVector("_GerstnerWave[3]", new Vector4(-0.5f, 0.5f, 3.0f, 0.6f));
            }
        }
    }
}