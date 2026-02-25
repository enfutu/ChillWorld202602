
using GPUInfiniteGrass;
using TMPro;
#if UDONSHARP
using UdonSharp;
#endif
using UnityEngine;
using UnityEngine.UI;

#if UDONSHARP
public class GrassController : UdonSharpBehaviour {
#else
public class GrassController : MonoBehaviour {
#endif
    
    public ParticleSurfaceManager ParticleSurfaceManager;
    private const int GrassParticlesPerBatch = 16383;
    private const int TrianglesPerParticle = 2;
    
    public Slider TrisSlider;
    public TMP_Text TrisValue;
    
    public Slider RadiusSlider;
    public TMP_Text RadiusValue;
    
    
    public string FormatKMB(int value, int decimals = 2) {
        int abs = Mathf.Abs(value);
        string sign = value < 0 ? "-" : "";
        if (abs < 1_000) return sign + abs.ToString();
        if (abs < 1_000_000) return sign + Format(abs, 1_000d, "K", decimals);
        if (abs < 1_000_000_000) return sign + Format(abs, 1_000_000d, "M", decimals);
        return sign + Format(abs, 1_000_000_000d, "B", decimals);
    }

    private string Format(long value, double divisor, string suffix, int decimals) {
        double v = value / divisor;
        string format = v >= 100 ? "0" : v >= 10 ? "0.#" : "0." + new string('#', Mathf.Clamp(decimals, 0, 4));
        return v.ToString(format) + suffix;
    }
    
    private void Update() {
        TrisValue.text = FormatKMB((int)TrisSlider.value * GrassParticlesPerBatch * TrianglesPerParticle);
        ParticleSurfaceManager.DrawAmount = (int)TrisSlider.value;
        RadiusValue.text = RadiusSlider.value + "m";
        ParticleSurfaceManager.DrawDistance = RadiusSlider.value;
    }

    public void Refresh() {
        ParticleSurfaceManager.TrailCRT.Initialize();
    }

}
