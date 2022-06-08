#ifndef __SHADER_HELPER__
#define __SHADER_HELPER__

float3 TriplanarNormal(float3 position, float3 surfaceNormal, float scale, sampler2D normalMap, float sharpness) {
    float3 normalX = UnpackNormal(tex2D(normalMap, position.zy * scale));
    float3 normalY = UnpackNormal(tex2D(normalMap, position.xz * scale));
    float3 normalZ = UnpackNormal(tex2D(normalMap, position.xy * scale));

    normalX = float3(normalX.xy + surfaceNormal.zy, normalX.z * surfaceNormal.x);
    normalY = float3(normalY.xy + surfaceNormal.xz, normalY.z * surfaceNormal.y);
    normalZ = float3(normalZ.xy + surfaceNormal.xy, normalZ.z * surfaceNormal.z);

    float3 weight = pow(abs(surfaceNormal), sharpness);
    weight /= dot(weight, 1);

    return normalize(normalX.zyx * weight.x + normalY.xzy * weight.y + normalZ.xyz * weight.z);
}

float4 TriplanarColor(float3 position, float3 normal, float scale, sampler2D tex, float sharpness) {
    // sample the texture
    fixed4 colX = tex2D(tex, position.yz * scale);
    fixed4 colY = tex2D(tex, position.xz * scale);
    fixed4 colZ = tex2D(tex, position.xy * scale);

    float3 blend = pow(abs(normal), sharpness);
    blend /= dot(blend, 1);
    return colX * blend.x + colY * blend.y + colZ * blend.z;
}

float2 SphereDepth(float3 center, float radius, float3 viewOrigin, float3 view, float missDistance) {
    float3 offset = viewOrigin - center;
    const float a = 1;
    float b = 2 * dot(offset, view);
    float c = dot(offset, offset) - radius * radius;

    float discriminant = b * b - 4 * a * c;

    if (discriminant > 0) {
        float s = sqrt(discriminant);
        float dstToSphere1 = max(0, (-b - s) / (2 * a));
        float dstToSphere2 = (-b + s) / (2 * a);

        if (dstToSphere2 >= 0) {
            return float2(dstToSphere1, dstToSphere2 - dstToSphere1);
        }
    }

    return float2(missDistance, 0);
}

#endif