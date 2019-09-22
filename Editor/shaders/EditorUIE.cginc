#ifndef UNITY_EDITOR_UIE_INCLUDED
#define UNITY_EDITOR_UIE_INCLUDED

fixed _EditorColorSpace; // 1 for Linear, 0 for Gamma

fixed4 uie_editor_frag(v2f IN)
{
    // Postpone the application of the tint after the linear-to-gamma conversion.
    fixed4 tint = fixed4(IN.color.xyz, 1);
    IN.color = (fixed4)1;
    fixed4 stdColor = fixed4(uie_std_frag(IN).xyz, 1);

    // Only use the gamma conversion for an atlas or custom texture with an editor in linear space.
    fixed4 gammaColor = fixed4(LinearToGammaSpace(stdColor.rgb), stdColor.a);
    fixed convertToGamma = _EditorColorSpace * (abs(IN.flags.y) /* isTextured */ + IN.flags.z /* isCustom */);
    fixed4 result = lerp(stdColor, gammaColor, convertToGamma);

    // Apply the tint.
    return result * tint;
}

#endif // UNITY_EDITOR_UIE_INCLUDED
