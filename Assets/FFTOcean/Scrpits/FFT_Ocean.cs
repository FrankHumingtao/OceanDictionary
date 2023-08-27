using UnityEngine;
using Random = UnityEngine.Random;

namespace Scrpits.FFT_Ocean
{
    public class FFT_Ocean : MonoBehaviour
    {
        public Material OceanMaterial;
        public ComputeShader OceanCS;   //计算海洋的cs
        public Material DualBlur;
    
        private RenderTexture GaussianRandomRT;
        private RenderTexture HeightSpectrumRT;
        private RenderTexture DisplaceXSpectrumRT;
        private RenderTexture DisplaceZSpectrumRT;
        private RenderTexture DisplaceRT;
        private RenderTexture OutputRT;
        private RenderTexture NormalBubblesRT;
        private RenderTexture SSSMaskRT;
    
        private RenderTexture[] _blurBuffer1;
        private RenderTexture[] _blurBuffer2;

        private int kernelComputeGaussianRandom;            //计算高斯随机数
        private int kernelCreateHeightSpectrum;             //创建高度频谱
        private int kernelCreateDisplaceSpectrum;           //创建偏移频谱
        private int kernelFFTHorizontal;                    //FFT横向
        private int kernelFFTHorizontalEnd;                 //FFT横向，最后阶段
        private int kernelFFTVertical;                      //FFT纵向
        private int kernelFFTVerticalEnd;                   //FFT纵向,最后阶段
        private int kernelTextureGenerationDisplace;        //生成偏移纹理
        private int kernelTextureGenerationNormalBubbles;   //生成法线和泡沫纹理

        // CreateMesh
        public int MeshSize = 250;		//网格长宽数量
        public float MeshLength = 10;	//网格长度
        private int[] vertIndexs;       //网格三角形索引
        private Vector3[] positions;
        private Vector2[] uvs; 

        // InitializeCSvalue
        private int fftSize;
        public int FFTPow = 10;
    
        // Update
        private float time;
        public float TimeScale;
    
        // computeOceanValue
        public float A = 10;	
        public Vector4 WindAndSeed = new Vector4(0.1f, 0.2f, 0, 0);//风向和随机种子 xy为风, zw为两个随机种子
        public float WindScale = 2;     //风强
        public float Lambda = -1;       //用来控制偏移大小
        public float HeightScale = 1;   //高度影响
        public float BubblesScale = 1;  //泡沫强度
        public float BubblesThreshold = 1;//泡沫阈值
    
        public int ControlM = 12;       //控制m,控制FFT变换阶段
        public bool isControlH = true;  //是否控制横向FFT，否则控制纵向FFT
        // InitializeCSvalue
        // Awake
        private MeshFilter filetr;
        private MeshRenderer render;
        private Mesh mesh;
    
        private static readonly int Displace = Shader.PropertyToID("_Displace");
        private static readonly int NormalBubbles = Shader.PropertyToID("_Normal_Bubbles");
        private static readonly int SSSMask = Shader.PropertyToID("_SSSMask");

        private void Awake()
        {
            //添加网格及渲染组件
            filetr = gameObject.GetComponent<MeshFilter>();
            if (filetr == null)
            {
                filetr = gameObject.AddComponent<MeshFilter>();
            }
            render = gameObject.GetComponent<MeshRenderer>();
            if (render == null)
            {
                render = gameObject.AddComponent<MeshRenderer>();
            }
            mesh = new Mesh();
            filetr.mesh = mesh;
            render.material = OceanMaterial;
        
            DualBlur = new Material(Shader.Find("Post/DualBlur"));
        }
        private void Start()
        {
            CreateMesh();
            InitializeCSvalue();
            InitBlurValue();
        }
        private void Update()
        {
            time += Time.deltaTime * TimeScale;
            computeOceanValue();
            BlurValue();
            SetMaterialTex();
        }

        // 保存图片到本地,修改path和想要保存的图片即可
        // private void OnDestroy()
        // {
        //     Debug.Log("ok");
        //     string path = Application.dataPath + "/SavedTexture.png";
        //     TextureSaver.SaveRenderTextureToPNG(NormalBubblesRT,path);
        // }

        private void InitBlurValue()
        {
            _blurBuffer1 = new RenderTexture[3];
            _blurBuffer2 = new RenderTexture[3];
        }
        private void BlurValue()
        {
            var prefilterRend = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.Default);
            Graphics.Blit(NormalBubblesRT, prefilterRend, DualBlur, 0);
            var last = prefilterRend;
            for (int level = 0; level < 3; level++)
            {
                _blurBuffer1[level] = RenderTexture.GetTemporary(
                    last.width / 2, last.height / 2, 0, RenderTextureFormat.Default
                );
                Graphics.Blit(last, _blurBuffer1[level], DualBlur, 0);

                last = _blurBuffer1[level];
            }

            for (int level = 2; level >= 0; level--)
            {
                _blurBuffer2[level] = RenderTexture.GetTemporary(
                    last.width * 2, last.height * 2, 0, RenderTextureFormat.Default
                );
        
                Graphics.Blit(last, _blurBuffer2[level], DualBlur, 1);
        
                last = _blurBuffer2[level];
            }
        
            Graphics.Blit(last, SSSMaskRT, DualBlur, 1);
        
            for (var i = 0; i < 3; i++)
            {
                if (_blurBuffer1[i] != null)
                {
                    RenderTexture.ReleaseTemporary(_blurBuffer1[i]);
                    _blurBuffer1[i] = null;
                }

                if (_blurBuffer2[i] != null)
                {
                    RenderTexture.ReleaseTemporary(_blurBuffer2[i]);
                    _blurBuffer2[i] = null;
                }
            }
            RenderTexture.ReleaseTemporary(prefilterRend);

        }
        private void computeOceanValue()
        {
            OceanCS.SetFloat("A", A);
            WindAndSeed.z = Random.Range(1, 10f);
            WindAndSeed.w = Random.Range(1, 10f);
            Vector2 wind = new Vector2(WindAndSeed.x, WindAndSeed.y);
            wind.Normalize();
            wind *= WindScale;
            OceanCS.SetVector("WindAndSeed", new Vector4(wind.x, wind.y, WindAndSeed.z, WindAndSeed.w));
            OceanCS.SetFloat("Time", time);
            OceanCS.SetFloat("Lambda", Lambda);
            OceanCS.SetFloat("HeightScale", HeightScale);
            OceanCS.SetFloat("BubblesScale", BubblesScale);
            OceanCS.SetFloat("BubblesThreshold",BubblesThreshold);

            //生成高度频谱
            OceanCS.SetTexture(kernelCreateHeightSpectrum, "GaussianRandomRT", GaussianRandomRT);
            OceanCS.SetTexture(kernelCreateHeightSpectrum, "HeightSpectrumRT", HeightSpectrumRT);
            OceanCS.Dispatch(kernelCreateHeightSpectrum, fftSize / 8, fftSize / 8, 1);

            //生成偏移频谱
            OceanCS.SetTexture(kernelCreateDisplaceSpectrum, "HeightSpectrumRT", HeightSpectrumRT);
            OceanCS.SetTexture(kernelCreateDisplaceSpectrum, "DisplaceXSpectrumRT", DisplaceXSpectrumRT);
            OceanCS.SetTexture(kernelCreateDisplaceSpectrum, "DisplaceZSpectrumRT", DisplaceZSpectrumRT);
            OceanCS.Dispatch(kernelCreateDisplaceSpectrum, fftSize / 8, fftSize / 8, 1);


            if (ControlM == 0)
            {
                SetMaterialTex();
                return;
            }

            //进行横向IFFT
            for (int m = 1; m <= FFTPow; m++)
            {
                int ns = (int)Mathf.Pow(2, m - 1);
                OceanCS.SetInt("Ns", ns);
                //最后一次进行特殊处理
                if (m != FFTPow)
                {
                    ComputeFFT(kernelFFTHorizontal, ref HeightSpectrumRT);
                    ComputeFFT(kernelFFTHorizontal, ref DisplaceXSpectrumRT);
                    ComputeFFT(kernelFFTHorizontal, ref DisplaceZSpectrumRT);
                }
                else
                {
                    ComputeFFT(kernelFFTHorizontalEnd, ref HeightSpectrumRT);
                    ComputeFFT(kernelFFTHorizontalEnd, ref DisplaceXSpectrumRT);
                    ComputeFFT(kernelFFTHorizontalEnd, ref DisplaceZSpectrumRT);
                }
                if (isControlH && ControlM == m)
                {
                    SetMaterialTex();
                    return;
                }
            }
            //进行纵向IFFT
            for (int m = 1; m <= FFTPow; m++)
            {
                int ns = (int)Mathf.Pow(2, m - 1);
                OceanCS.SetInt("Ns", ns);
                //最后一次进行特殊处理
                if (m != FFTPow)
                {
                    ComputeFFT(kernelFFTVertical, ref HeightSpectrumRT);
                    ComputeFFT(kernelFFTVertical, ref DisplaceXSpectrumRT);
                    ComputeFFT(kernelFFTVertical, ref DisplaceZSpectrumRT);
                }
                else
                {
                    ComputeFFT(kernelFFTVerticalEnd, ref HeightSpectrumRT);
                    ComputeFFT(kernelFFTVerticalEnd, ref DisplaceXSpectrumRT);
                    ComputeFFT(kernelFFTVerticalEnd, ref DisplaceZSpectrumRT);
                }
                if (!isControlH && ControlM == m)
                {
                    SetMaterialTex();
                    return;
                }
            }

            //计算纹理偏移
            OceanCS.SetTexture(kernelTextureGenerationDisplace, "HeightSpectrumRT", HeightSpectrumRT);
            OceanCS.SetTexture(kernelTextureGenerationDisplace, "DisplaceXSpectrumRT", DisplaceXSpectrumRT);
            OceanCS.SetTexture(kernelTextureGenerationDisplace, "DisplaceZSpectrumRT", DisplaceZSpectrumRT);
            OceanCS.SetTexture(kernelTextureGenerationDisplace, "DisplaceRT", DisplaceRT);
            OceanCS.Dispatch(kernelTextureGenerationDisplace, fftSize / 8, fftSize / 8, 1);

            //生成法线和泡沫纹理
            OceanCS.SetTexture(kernelTextureGenerationNormalBubbles, "DisplaceRT", DisplaceRT);
            OceanCS.SetTexture(kernelTextureGenerationNormalBubbles, "NormalBubblesRT", NormalBubblesRT);
            OceanCS.Dispatch(kernelTextureGenerationNormalBubbles, fftSize / 8, fftSize / 8, 1);
        
        }
        private void ComputeFFT(int kernel, ref RenderTexture input)
        {
            OceanCS.SetTexture(kernel, "InputRT", input);
            OceanCS.SetTexture(kernel, "OutputRT", OutputRT);
            OceanCS.Dispatch(kernel, fftSize / 8, fftSize / 8, 1);

            //交换输入输出纹理
            (input, OutputRT) = (OutputRT, input);
        }
        private void SetMaterialTex()
        {
            //设置海洋材质纹理
            OceanMaterial.SetTexture(Displace, DisplaceRT);
            OceanMaterial.SetTexture(NormalBubbles, NormalBubblesRT);
            OceanMaterial.SetTexture(SSSMask,SSSMaskRT);
        }

        /// <summary>
        /// 初始化 Compute shader 
        /// </summary>
        private void InitializeCSvalue()
        {
            fftSize = (int)Mathf.Pow(2, FFTPow);
        
            //创建渲染纹理
            if (GaussianRandomRT != null && GaussianRandomRT.IsCreated())
            {
                GaussianRandomRT.Release();
                HeightSpectrumRT.Release();
                DisplaceXSpectrumRT.Release();
                DisplaceZSpectrumRT.Release();
                DisplaceRT.Release();
                OutputRT.Release();
                NormalBubblesRT.Release();
            
                // SSSMask也在着顺便弄了
                SSSMaskRT.Release();
            }
            GaussianRandomRT = CreateRT(fftSize);
            HeightSpectrumRT = CreateRT(fftSize);
            DisplaceXSpectrumRT = CreateRT(fftSize);
            DisplaceZSpectrumRT = CreateRT(fftSize);
            DisplaceRT = CreateRT(fftSize);
            OutputRT = CreateRT(fftSize);
            NormalBubblesRT = CreateRT(fftSize);
            SSSMaskRT = CreateRT(fftSize/2);

            //获取所有kernelID
            kernelComputeGaussianRandom = OceanCS.FindKernel("ComputeGaussianRandom");
            kernelCreateHeightSpectrum = OceanCS.FindKernel("CreateHeightSpectrum");
            kernelCreateDisplaceSpectrum = OceanCS.FindKernel("CreateDisplaceSpectrum");
            kernelFFTHorizontal = OceanCS.FindKernel("FFTHorizontal");
            kernelFFTHorizontalEnd = OceanCS.FindKernel("FFTHorizontalEnd");
            kernelFFTVertical = OceanCS.FindKernel("FFTVertical");
            kernelFFTVerticalEnd = OceanCS.FindKernel("FFTVerticalEnd");
            kernelTextureGenerationDisplace = OceanCS.FindKernel("TextureGenerationDisplace");
            kernelTextureGenerationNormalBubbles = OceanCS.FindKernel("TextureGenerationNormalBubbles");

            //设置ComputerShader数据
            OceanCS.SetInt("N", fftSize);
            OceanCS.SetFloat("OceanLength", MeshLength); 
        
            //生成高斯随机数
            OceanCS.SetTexture(kernelComputeGaussianRandom, "GaussianRandomRT", GaussianRandomRT);
            OceanCS.Dispatch(kernelComputeGaussianRandom, fftSize / 8, fftSize / 8, 1);

        }

        /// <summary>
        /// 创建一个正方形的纹理
        /// </summary>
        /// <param name="size">纹理的边长</param>
        /// <returns></returns>
        private RenderTexture CreateRT(int size)
        {
            RenderTexture rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
            rt.enableRandomWrite = true;
            rt.Create();
            return rt;
        }
        private void CreateMesh(){

            int numVertices = MeshSize * MeshSize;
            int numTriangles = (MeshSize-1) * (MeshSize-1) * 2;
  
            Vector3[] vertices = new Vector3[numVertices];
            Vector2[] uvs = new Vector2[numVertices];

            int index0 = 0;
            for (int i = 0; i < MeshSize; i++) {
                for (int j = 0; j < MeshSize; j++) {
                    // 计算顶点坐标
                    float x = (j - MeshSize/2f) * MeshLength / MeshSize; 
                    float z = (i - MeshSize/2f) * MeshLength / MeshSize;
                    vertices[index0] = new Vector3(x, 0, z);

                    // 计算UV坐标
                    float u = j / (float)(MeshSize - 1);
                    float v = i / (float)(MeshSize - 1);
                    uvs[index0] = new Vector2(u, v);

                    index0++;
                }
            }

            int[] triangles = new int[numTriangles*3];

            int triIndex = 0;
            for (int i = 0; i < MeshSize-1; i++) {
                for (int j = 0; j < MeshSize-1; j++) {
      
                    int index = i * MeshSize + j;

                    triangles[triIndex++] = index;
                    triangles[triIndex++] = index + MeshSize;
                    triangles[triIndex++] = index + MeshSize + 1;

                    triangles[triIndex++] = index; 
                    triangles[triIndex++] = index + MeshSize + 1;
                    triangles[triIndex++] = index + 1;
                } 
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

        }
    }
}

