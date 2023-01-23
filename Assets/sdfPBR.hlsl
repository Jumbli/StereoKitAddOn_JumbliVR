#include <stereokit.hlsli>

//--name = app/sdfPBR

//--color:color = 0,0,0,1
float4 color;

float slot; // Don't delete this, it's used in C#
//--slot: = 0

float row;
//--row: = 0
float col;
//--col: = 3
float rowHeight;
//--rowHeight: = 1
float colWidth;
//--colWidth: = 1

float  roughness;
//--roughness             = .5

Texture2D    sdfTexture   : register(t0);
//--sdfTexture     = white
SamplerState sdfTexture_s : register(s0);

//--radius      = 5,10,0,0
float4 radius;

struct vsIn {
	float4 pos  : SV_Position;
	float3 norm : NORMAL0;
	float2 uv   : TEXCOORD0;
	float4 col  : COLOR0;
};
struct psIn {
	float4 pos   : SV_POSITION;
	float3 normal  : NORMAL0;
	float2 uv    : TEXCOORD0;
	float4 color : COLOR0;
	float3 irradiance: COLOR1;
	float3 world   : TEXCOORD1;
	float3 view_dir: TEXCOORD2;
	uint view_id : SV_RenderTargetArrayIndex;
};

psIn vs(vsIn input, uint id : SV_InstanceID) {
	psIn o;
	o.view_id = id % sk_view_count;
	id = id / sk_view_count;

	o.world = mul(float4(input.pos.xyz, 1), sk_inst[id].world).xyz;
	o.pos = mul(float4(o.world, 1), sk_viewproj[o.view_id]);

	o.normal = normalize(mul(float4(input.norm, 0), sk_inst[id].world).xyz);

	o.uv.x = input.uv.x * colWidth + col * colWidth;
	o.uv.y = input.uv.y * rowHeight + row * rowHeight;

	o.color = color;

	o.irradiance = Lighting(o.normal);
	o.view_dir = sk_camera_pos[o.view_id].xyz - o.world;
	
	return o;
}

float3 FresnelSchlickRoughness(float cosTheta, float3 F0, float roughness) {
	return F0 + (max(1 - roughness, F0) - F0) * pow(1.0 - cosTheta, 5.0);
}

// See: https://www.unrealengine.com/en-US/blog/physically-based-shading-on-mobile
float2 brdf_appx(half Roughness, half NoV) {
	const half4 c0 = { -1, -0.0275, -0.572, 0.022 };
	const half4 c1 = { 1, 0.0425, 1.04, -0.04 };
	half4 r = Roughness * c0 + c1;
	half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
	half2 AB = half2(-1.04, 1.04) * a004 + r.zw;
	return AB;
}


float4 ps(psIn input) : SV_TARGET{
	float4 sdf = sdfTexture.Sample(sdfTexture_s, input.uv);

	float4 albedo = lerp(float4(0, 0, 0, 0), input.color, smoothstep(.45, .55, sdf.x));

	float3 view = normalize(input.view_dir);
	float3 reflection = reflect(-view, input.normal);
	float  ndotv = max(0, dot(input.normal, view));

	float3 F0 = 0.04;
	float3 F = FresnelSchlickRoughness(ndotv, F0, roughness);
	float3 kS = F;

	float3 prefilteredColor = sk_cubemap.SampleLevel(sk_cubemap_s, reflection, .5).rgb; // miplevel = .5
	float2 envBRDF = brdf_appx(roughness, ndotv);
	float3 specular = prefilteredColor * (F * envBRDF.x + envBRDF.y);

	float3 kD = 1 - kS;
	float3 diffuse = albedo.rgb * input.irradiance;
	float3 color = (kD * diffuse + specular);

	return float4(color, albedo.a * input.color.a);
}