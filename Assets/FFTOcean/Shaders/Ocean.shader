Shader "Ocean/OceanURP"
{
     Properties
    {
        // compute calculate data 可以隐藏但为了方便debug,暂时不隐藏
        _Displace ("Displace", 2D) = "black" { }
        _Normal_Bubbles ("Normal_Bubbles", 2D) = "black" { }
        
        _OceanColorShallow ("Ocean Color Shallow", Color) = (1, 1, 1, 1)
        _OceanColorDeep ("Ocean Color Deep", Color) = (1, 1, 1, 1)
        _BubblesColor ("Bubbles Color", Color) = (1, 1, 1, 1)
        
        [HDR]_Specular ("Specular", Color) = (1, 1, 1, 1)
        _Gloss ("Gloss", Range(8.0, 256)) = 20
        
        // 水体颜色
        _Ramptex ("_Ramptex", 2D) = "black" { }
        
        // Fresnel
        _AirRefractive ("空气折射率",float) = 1
        _WaterRefractive ("水折射率",float) = 1.33

        _SSSMask ("SSSMask",2D) ="white"{}
        _SSSColor ("SSS Color", Color) = (1, 1, 1, 1)
        _SSSParameters ("distortion_power_scale_waveMaxHeight",Vector)=(0.2,1,1,20)
        
        // 折射
        _MaxDepth ("MaxDepth", Float ) = 1
        _WaterSurfaceDepth ("_WaterSurfaceDepth", Float ) = 1
    }
    SubShader
    {
        Tags { 
                "Queue"="Transparent"
                "RenderType"="Transparent"
                "RenderPipeline" = "UniversalPipeline"
                "LightMode" = "UniversalForward"
        }
        LOD 100
        
        Pass
        {
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On 
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            
            struct  Attributes
            {
                float4 vertex : POSITION;
                float2 uv: TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS: SV_POSITION;
                float2 uv: TEXCOORD0;
                float3 positionWS: TEXCOORD1;
                float4 positionNDC: TEXCOORD2;
                float4 projPos :TEXCOORD3;
            };

            // compute data
            sampler2D _Displace;    float4 _Displace_ST;
            sampler2D _Normal_Bubbles;
            
            float4 _OceanColorShallow;
            float4 _OceanColorDeep;
            float4 _BubblesColor;
            float4 _Specular;
            float _Gloss;

            // 水体颜色
            sampler2D _Ramptex ;

            //fresnel
            float _AirRefractive;
            float _WaterRefractive;

            // sss
            sampler2D _SSSMask;
            float4 _SSSParameters;
            float4 _SSSColor;

            // 折射
            float _MaxDepth;
            float _WaterSurfaceDepth;

            //------------[[函数]]--------------
            float4 SSSColor(float3 lightDir, float3 viewDir, float3 normal, float waveHeight, float SSSMask)
            {
                float3 H = normalize(-lightDir + normal * _SSSParameters.x);
                float I = pow(saturate(dot(viewDir, -H)), _SSSParameters.y)* _SSSParameters.z * waveHeight * SSSMask;
                // float I = pow(saturate(dot(viewDir, -H)), _SSSParameters.y)* _SSSParameters.z * waveHeight * 1;
                return _SSSColor*I;
            }
            

            Varyings vert(Attributes input){
                Varyings output;
                output.uv = TRANSFORM_TEX(input.uv,_Displace);
                float4 displace = tex2Dlod(_Displace,float4(output.uv,0,0));
                input.vertex += float4(displace.xyz,0);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.vertex.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionNDC = positionInputs.positionNDC;
                output.projPos = ComputeScreenPos(output.positionCS);
                output.projPos.z = -mul(UNITY_MATRIX_MV,input.vertex).z;
                return output;
            }

            float Fresnel(float3 normal, half3 viewDir)
            {
                float R_0 =pow((_AirRefractive - _WaterRefractive) / (_AirRefractive + _WaterRefractive),2) ;
                return saturate(R_0 + (1 - R_0) * pow(1 - dot(normal, viewDir), 5));
            }

            //----------------------------------------------------
            half4 frag(Varyings i) : SV_Target{

                float alpha = 0.8f;

                float4 normal_bubbles = tex2D(_Normal_Bubbles,i.uv);
                float3 normal = TransformObjectToWorldNormal(normal_bubbles.xyz);
                float bubbles = normal_bubbles.w;
                float sssMask = tex2D(_SSSMask,i.uv).w;

                Light Mainlight = GetMainLight();
                float3 lightDirWS = normalize(Mainlight.direction);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 reflectDir = reflect(-viewDirWS,normal);

                float4 screenPos = ComputeScreenPos(i.positionCS);
                
                float2 screenUV = i.positionNDC.xy / i.positionNDC.w;
                float2 distUV = screenUV + normal.xy * 0.05f;
                // 获取深度
                real rawDepth = SampleSceneDepth(distUV);
                
                // 转换成linear01深度
                float linear01Depth = Linear01Depth(rawDepth, _ZBufferParams);
                // 转换成linearEye深度
                float linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                // 获取深度图对应的世界空间坐标
                float3 depthWSPos  = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);

                float fakeRefract =step(i.positionWS.y,depthWSPos.y);
                float2 refractedUV = lerp(distUV,screenUV,fakeRefract);

                // 重采样,去除伪影
                rawDepth = SampleSceneDepth(refractedUV);
                depthWSPos  = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                
                float waterDepth = length(i.positionWS- depthWSPos);
                float waterDepth01 =clamp(waterDepth / _MaxDepth, 0, 1);
                
                // 折射采样
                float3 refractCol = SampleSceneColor(refractedUV);

                // 添加深度影响
                float3 reCollege = lerp(refractCol,float3(1,1,1),waterDepth01);
                
                // 通过反射探针获得环境的反射数据
                half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0,samplerunity_SpecCube0,reflectDir,0);
                half3 environment = DecodeHDREnvironment(encodedIrradiance,unity_SpecCube0_HDR);
                
                half fresnel = Fresnel(normal, viewDirWS);

                // 着色
                half facing = saturate(dot(viewDirWS, normal));                
                float3 oceanColor = lerp(_OceanColorShallow, _OceanColorDeep, facing);

                float3 rampCol = tex2D(_Ramptex,float2(waterDepth01,0.5));
                
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
                // 泡沫颜色
                // TODO: 这个模型很草率,泡沫的着色物理上应该没那么简单
                float3 bubblesDiffuse = _BubblesColor.rbg * Mainlight.color* saturate(dot(lightDirWS, normal));
                //海洋颜色
                float3 oceanDiffuse =oceanColor* LightingLambert(Mainlight.color,lightDirWS ,normal);
                float3 specular = LightingSpecular(Mainlight.color,lightDirWS,normal,viewDirWS,_Specular,_Gloss);

                // return  float4(specular,1);
                
                // 假sss
                float waveHeight = saturate(i.positionWS.y/_SSSParameters.w);
                float4 sssColor = SSSColor(lightDirWS,viewDirWS,normal,waveHeight,sssMask*3);

                alpha  =1;
                float3 diffuse = lerp(oceanDiffuse, bubblesDiffuse, bubbles);
                // 这里0.2可以给个参数
                float3 col = ambient + lerp(diffuse, environment, fresnel)*(reCollege-0.2) +specular+sssColor+rampCol*0.5;
                return float4(col,alpha);
            }

            ENDHLSL
            
        }
    }
}
