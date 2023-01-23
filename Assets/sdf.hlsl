#include <stereokit.hlsli>

//--name = app/sdf

//--color:color = 0,0,0,1
float4 color;

float slot; // Don't delete this, it's used in C#
//--slot: = 0

float row;
//--row: = 1
float col;
//--col: = 1
float rowHeight;
//--rowHeight: = 1
float colWidth;
//--colWidth: = 1


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
	float2 uv    : TEXCOORD0;
	float4 color : COLOR0;
	uint view_id : SV_RenderTargetArrayIndex;
};

psIn vs(vsIn input, uint id : SV_InstanceID) {
	psIn o;
	o.view_id = id % sk_view_count;
	id = id / sk_view_count;

	
	float3 world = mul(float4(input.pos.xyz, 1), sk_inst[id].world).xyz;
	o.pos = mul(float4(world, 1), sk_viewproj[o.view_id]);

	o.uv.x = input.uv.x * colWidth + col * colWidth;
	o.uv.y = input.uv.y * rowHeight + row * rowHeight;

	o.color = color;
	
	return o;
}
float4 ps(psIn input) : SV_TARGET{
	float4 sdf = sdfTexture.Sample(sdfTexture_s, input.uv);

	float4 col = lerp(float4(0, 0, 0, 0), input.color, smoothstep(.45, .55, sdf.x));

	 return col;
	
}