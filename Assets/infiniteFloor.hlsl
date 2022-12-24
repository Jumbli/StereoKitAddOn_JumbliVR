#include <stereokit.hlsli>

//--name = app/infiniteFloor

//--color:color = 1,1,1,.1
float4 color;
//--radius      = 1,2,0,0
float4 radius;
//--stage:vector3 = 0,0,0
float3 stage;
//--inverse = 0
float inverse;

struct vsIn {
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
};
struct psIn {
	float4 pos   : SV_POSITION;
	float4 world : TEXCOORD0;
	uint view_id : SV_RenderTargetArrayIndex;
	float2 uv : TEXCOORD1;
};

psIn vs(vsIn input, uint id : SV_InstanceID) {
	psIn o;
	o.view_id = id % sk_view_count;
	id        = id / sk_view_count;
	o.uv = input.uv;
	o.world = mul(input.pos, sk_inst[id].world);
	o.pos   = mul(o.world,   sk_viewproj[o.view_id]);
	

	return o;
}
float4 ps(psIn input) : SV_TARGET{
	// This line algorithm is inspired by :
	// http://madebyevan.com/shaders/grid/

	// Make center solid
	

	//float3 pos = input.world.xyz - stage;
	float2 pos = input.uv - stage.xz - .5f;
	float center = distance(stage.xz, input.uv -.5f) < .111;

	float pi = 3.141592653589793;
	float scale = .5; // How many radial segments
	float2 coord = float2(length(pos), atan2(pos.x, pos.y) * scale / pi);
	float2 wrapped = float2(coord.x, frac(coord.y / (2.0 * scale)) * (2.0 * scale));
	float2 coordWidth = fwidth(coord);
	float2 wrappedWidth = fwidth(wrapped);
	float2 width = coord.y < -scale * 0.5 || coord.y > scale * 0.5 ? wrappedWidth : coordWidth;

	// Compute anti-aliased world-space grid lines
	float2 grid = abs(frac(coord * 9 - 0.5) - 0.5) / width - .01 / width;
	float alpha = min(grid.x, grid.y) + center * 1000;

	if (inverse == 1)
		alpha = 1 - alpha;

	// Fade out by 1 meter away from player
	float fade = max(0, 1 - distance(sk_camera_pos[0].xz, input.world.xz));

	float strength = max(0,min(alpha, 1.0) * color.a * fade);


	return float4(color * min(1, strength));

}