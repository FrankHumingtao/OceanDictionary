Shader "Post/DualBlur"
{

    Properties
    {
        [HideInInspector]_MainTex("MainTex",2D)="white"{}
    }
    
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    ENDHLSL 

    SubShader
    {
        Tags{"RenderPipeline"="UniversalRenderPipeline"}
        Cull Off ZWrite Off ZTest Always
        Pass //Dual Blur -- Down【pass 0】
        {
            Name "DownSample"
            HLSLPROGRAM 
            #pragma vertex DualBlurDownVert
            #pragma fragment DualBlurDownFrag

            struct appdata
            {
                float4 vertex:POSITION;
                float2 uv:TEXCOORD0;
            };
            
            struct v2f_DualBlurDown
            {
                float4 vertex:POSITION;
                float2 uv[5]:TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            float _BlurRange;
            float4 _MainTex_TexelSize;  

            v2f_DualBlurDown DualBlurDownVert (appdata v)
            {
                //降采样
                v2f_DualBlurDown o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv[0] = v.uv;

                #if UNITY_UV_STARTS_TOP
                    o.uv[0].y = 1 - o.uv[0].y;
                #endif
	            //
                o.uv[1] = v.uv + float2(-1, -1)  * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5; //↖
	            o.uv[2] = v.uv + float2(-1,  1)  * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5; //↙
	            o.uv[3] = v.uv + float2(1,  -1)  * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5; //↗
	            o.uv[4] = v.uv + float2(1,   1)  * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5; //↘
	            //
                return o;
                //5 samples，组成一个五筒
            }

            float4 DualBlurDownFrag (v2f_DualBlurDown i):SV_TARGET
            {
                //降采样
                float4 col = tex2D(_MainTex, i.uv[0]) * 4;

                col += tex2D(_MainTex, i.uv[1]) ;
                col += tex2D(_MainTex, i.uv[2]) ;
                col += tex2D(_MainTex, i.uv[3]) ;
                col += tex2D(_MainTex, i.uv[4]) ;
                
                return col * 0.125; //sum / 8.0f
            }
            ENDHLSL
        }

        Pass //Dual Blur -- Up【pass 1】
        {
            Name "UpSample"
            HLSLPROGRAM 
            #pragma vertex DualBlurUpVert
            #pragma fragment DualBlurUpFrag

            struct appdata
            {
                float4 vertex:POSITION;
                float2 uv:TEXCOORD0;
            };
            
            struct v2f_DualBlurUp
            {
                float2 uv[8]:TEXCOORD0;
                float4 vertex:SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            float _BlurRange;
            float4 _MainTex_TexelSize;

            v2f_DualBlurUp DualBlurUpVert (appdata v)
            {
                //升采样
                v2f_DualBlurUp o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv[0] = v.uv;

            #if UNITY_UV_STARTS_TOP
                o.uv[0].y = 1 - o.uv[0].y;
            #endif
	            //
	            o.uv[0] = v.uv + float2(-1,-1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
	            o.uv[1] = v.uv + float2(-1, 1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
	            o.uv[2] = v.uv + float2(1, -1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
	            o.uv[3] = v.uv + float2(1,  1) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
	            o.uv[4] = v.uv + float2(-2, 0) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
	            o.uv[5] = v.uv + float2(0, -2) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
	            o.uv[6] = v.uv + float2(2,  0) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
	            o.uv[7] = v.uv + float2(0,  2) * (1 + _BlurRange) * _MainTex_TexelSize.xy * 0.5;
                //
                return o;
            }

            float4 DualBlurUpFrag (v2f_DualBlurUp i):SV_TARGET
            {
                //升采样
                float4 col = 0;

                col += tex2D(_MainTex, i.uv[0]) * 2;
                col += tex2D(_MainTex, i.uv[1]) * 2;
                col += tex2D(_MainTex, i.uv[2]) * 2;
                col += tex2D(_MainTex, i.uv[3]) * 2;
                col += tex2D(_MainTex, i.uv[4]) ;
                col += tex2D(_MainTex, i.uv[5]) ;
                col += tex2D(_MainTex, i.uv[6]) ;
                col += tex2D(_MainTex, i.uv[7]) ;

                return col * 0.0833; //sum / 12.0f
            }

            
            ENDHLSL
        }


    }

}