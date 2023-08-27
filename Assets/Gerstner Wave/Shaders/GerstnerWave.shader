Shader "Ocean/GerstnerWave"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        
		_WaveA ("Wave A (dir, steepness, wavelength)", Vector) = (1,0,0.5,3)
    	_WaveB ("Wave B", Vector) = (0,1,0.25,3)
    	_WaveC ("Wave C", Vector) = (1,1,0.15,2)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
            };
            
            fixed4 _Color;
            float4 _WaveA,_WaveB,_WaveC;
            
            
		float3 GerstnerWave (
			float4 wave, float3 p, inout float3 tangent, inout float3 binormal
		) {
		    float steepness = wave.z;
		    float wavelength = wave.w;
		    float k = 2 * UNITY_PI / wavelength;
			float c = sqrt(9.8 / k);
			float2 d = normalize(wave.xy);
			float f = k * (dot(d, p.xz) - c * _Time.y);
			float a = steepness / k;
			
			// p.x += d.x * (a * cos(f));
			// p.y = a * sin(f);
			// p.z += d.y * (a * cos(f));

			tangent += float3(
				-d.x * d.x * (steepness * sin(f)),
				d.x * (steepness * cos(f)),
				-d.x * d.y * (steepness * sin(f))
			);
			binormal += float3(
				-d.x * d.y * (steepness * sin(f)),
				d.y * (steepness * cos(f)),
				-d.y * d.y * (steepness * sin(f))
			);
			return float3(
				d.x * (a * cos(f)),
				a * sin(f),
				d.y * (a * cos(f))
			);
		}
            v2f vert(appdata v)
            {
                v2f o;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				float3 gridPoint = worldPos.xyz;
				float3 tangent = float3(1, 0, 0);
				float3 binormal = float3(0, 0, 1);
				float3 p = gridPoint;
				p += GerstnerWave(_WaveA, gridPoint, tangent, binormal);
				p += GerstnerWave(_WaveB, gridPoint, tangent, binormal);
				p += GerstnerWave(_WaveC, gridPoint, tangent, binormal);
				float3 normal = normalize(cross(binormal,tangent));
                o.vertex = mul(UNITY_MATRIX_VP, float4(p,v.vertex.w));
                o.uv = v.uv;
				o.normal = normal;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = _Color;
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz); // 光照方向
                fixed3 normal = normalize(i.normal); // 法线方向
                fixed3 viewDir = normalize(_WorldSpaceCameraPos - i.vertex.xyz); // 视线方向
                fixed3 halfDir = normalize(lightDir + viewDir); // 半向量
                fixed3 diffuse = max(dot(i.normal, lightDir), 0); // Lambert漫反射
                fixed3 specular = pow(max(dot(normal, halfDir), 0), 32); // Blinn-Phong高光
                c.rgb *= diffuse*0.5f+0.5f;
                c.rgb += specular;
                return float4(i.normal,1);

            }
            ENDCG
        }
    }
}
