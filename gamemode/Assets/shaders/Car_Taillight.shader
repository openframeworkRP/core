
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#ifndef S_ALPHA_TEST
	#define S_ALPHA_TEST 0
	#endif
	#ifndef S_TRANSLUCENT
	#define S_TRANSLUCENT 1
	#endif
	
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define S_UV2 1
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 vPositionOs : TEXCOORD14;
	float3 vNormalOs : TEXCOORD15;
	float4 vTangentUOs_flTangentVSign : TANGENT	< Semantic( TangentU_SignV ); >;
	float4 vColor : COLOR0;
	float4 vTintColor : COLOR1;
	#if ( PROGRAM == VFX_PROGRAM_PS )
		bool vFrontFacing : SV_IsFrontFace;
	#endif
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		
		PixelInput i = ProcessVertex( v );
		i.vPositionOs = v.vPositionOs.xyz;
		i.vColor = v.vColor;
		
		ExtraShaderData_t extraShaderData = GetExtraPerInstanceShaderData( v.nInstanceTransformID );
		i.vTintColor = extraShaderData.vTint;
		
		VS_DecodeObjectSpaceNormalAndTangent( v, i.vNormalOs, i.vTangentUOs_flTangentVSign );
		return FinalizeVertex( i );
		
	}
}

PS
{
	#include "common/pixel.hlsl"
	RenderState( CullMode, F_RENDER_BACKFACES ? NONE : DEFAULT );
		
	SamplerState g_sSampler0 < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;
	CreateInputTexture2D( BaseTexture, Srgb, 8, "None", "_color", ",0/,0/0", DefaultFile( "materials/dev/white_color.tga" ) );
	Texture2D g_tBaseTexture < Channel( RGBA, Box( BaseTexture ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	TextureAttribute( LightSim_DiffuseAlbedoTexture, g_tBaseTexture )
	TextureAttribute( RepresentativeTexture, g_tBaseTexture )
	float4 g_vTailTint < UiType( Color ); UiGroup( ",0/,0/0" ); Default4( 0.59, 0.00, 0.00, 1.00 ); >;
	float g_flTailIntensity < Attribute( "TailIntensity" ); Default1( 5 ); >;
	float g_flTailOn < Attribute( "TailOn" ); Default1( 0 ); >;
	float g_flBrakeIntensity < Attribute( "BrakeIntensity" ); Default1( 5 ); >;
	float g_flBrakeOn < Attribute( "BrakeOn" ); Default1( 0 ); >;
	float g_flOpacity < Attribute( "Opacity" ); Default1( 0.9799999 ); >;
	
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		
		Material m = Material::Init( i );
		m.Albedo = float3( 1, 1, 1 );
		m.Normal = float3( 0, 0, 1 );
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		m.TintMask = 1;
		m.Opacity = 1;
		m.Emission = float3( 0, 0, 0 );
		m.Transmission = 0;
		
		float4 l_0 = Tex2DS( g_tBaseTexture, g_sSampler0, i.vTextureCoords.xy );
		float4 l_1 = g_vTailTint;
		float4 l_2 = l_0 * l_1;
		float l_3 = g_flTailIntensity;
		float l_4 = g_flTailOn;
		float l_5 = l_3 * l_4;
		float l_6 = g_flBrakeIntensity;
		float l_7 = g_flBrakeOn;
		float l_8 = l_6 * l_7;
		float l_9 = l_5 + l_8;
		float4 l_10 = l_2 * float4( l_9, l_9, l_9, l_9 );
		float l_11 = g_flOpacity;
		
		m.Albedo = l_2.xyz;
		m.Emission = l_10.xyz;
		m.Opacity = l_11;
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		
		
		m.AmbientOcclusion = saturate( m.AmbientOcclusion );
		m.Roughness = saturate( m.Roughness );
		m.Metalness = saturate( m.Metalness );
		m.Opacity = saturate( m.Opacity );
		
		// Result node takes normal as tangent space, convert it to world space now
		m.Normal = TransformNormal( m.Normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		
		// for some toolvis shit
		m.WorldTangentU = i.vTangentUWs;
		m.WorldTangentV = i.vTangentVWs;
		m.TextureCoords = i.vTextureCoords.xy;
				
		return ShadingModelStandard::Shade( m );
	}
}
