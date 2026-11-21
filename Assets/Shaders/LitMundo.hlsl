#ifndef OWL_LIT_MUNDO_INCLUDED
#define OWL_LIT_MUNDO_INCLUDED

// Proyeccion "de caja" en coordenadas de mundo: cada cara usa el plano al que
// mas se parece su normal. Como los modelos son low-poly (caras planas con
// vertices separados), basta con decidirlo por vertice.
float2 OwlUVMundo(float3 posicionWS, float3 normalWS)
{
    float3 n = abs(normalWS);
    float2 p;
    if (n.y >= n.x && n.y >= n.z)
        p = posicionWS.xz;          // piso
    else if (n.x >= n.z)
        p = posicionWS.zy;          // paredes que miran a X
    else
        p = posicionWS.xy;          // paredes que miran a Z

    // Tiling/Offset del material = repeticiones por metro
    return p * _BaseMap_ST.xy + _BaseMap_ST.zw;
}

Varyings OwlSueloVertex(Attributes input)
{
    Varyings output = LitPassVertex(input);

    float3 posicionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = OwlUVMundo(posicionWS, normalWS);

    return output;
}

#endif
