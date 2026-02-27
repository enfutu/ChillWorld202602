// RARA_CloudLinkSync.cs
// 雲（TowelCloud）のマテリアル値を、星（RARA/StarfieldUnlit_CloudLinked）へ同期する。
// Animator不要。連続同期/手動同期の両対応。
// 依存：UdonSharp / VRCSDK3-Worlds
// 注意：このスクリプトは Editor でも実機（PC/Quest）でも動作するが、
//       変更はローカルのみ（ネットワークで他人に反映はしない）。

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class RARA_CloudLinkSync : UdonSharpBehaviour
{
    [Header("Source (Cloud)")]
    [Tooltip("TowelCloud を割り当てた Renderer")]
    public Renderer cloudRenderer;
    [Tooltip("雲のマテリアル Index（通常 0）")]
    public int cloudMaterialIndex = 0;

    [Header("Targets (Stars)")]
    [Tooltip("CloudLinked 星ドームの Renderer（複数可）")]
    public Renderer[] starRenderers;
    [Tooltip("星のマテリアル Index（通常 0）")]
    public int starMaterialIndex = 0;

    [Header("Runtime")]
    [Tooltip("継続的に同期する（false なら Start/SyncNow 時のみコピー）")]
    public bool continuousSync = false;
    [Tooltip("continuousSync=true のときの同期間隔（秒）")]
    public float syncInterval = 0.25f;
    private float _timer;

    [Header("Options")]
    [Tooltip("ノイズテクスチャも毎回コピー（通常ON）")]
    public bool syncNoiseTexture = true;

    // --- ライフサイクル ---
    void Start()
    {
        DoSync();
    }

    void Update()
    {
        if (!continuousSync) return;
        _timer += Time.deltaTime;
        if (_timer >= syncInterval)
        {
            _timer = 0f;
            DoSync();
        }
    }

    // --- 外部から呼べるイベント（トリガー/ボタンなどで使用） ---
    public void SyncNow() { DoSync(); }
    public void _SyncNow() { DoSync(); }                 // VRC_Trigger から string で呼びやすい別名
    public void EnableContinuousSync() { continuousSync = true; _timer = 0f; }
    public void DisableContinuousSync() { continuousSync = false; }

    // --- 同期本体 ---
    private void DoSync()
    {
        Material cm = GetMaterial(cloudRenderer, cloudMaterialIndex);
        if (cm == null) { Debug.LogWarning("[CloudLinkSync] Cloud material not found."); return; }

        // すべての星Rendererに対してコピー
        for (int i = 0; i < (starRenderers == null ? 0 : starRenderers.Length); i++)
        {
            Renderer sr = starRenderers[i];
            if (sr == null) continue;

            Material sm = GetMaterial(sr, starMaterialIndex);
            if (sm == null) continue;

            // 対応表：雲 → 星(CloudLink)
            // Texture
            if (syncNoiseTexture) CopyTexture(cm, "_noiseMap", sm, "_CL_NoiseMap");

            // Float / Toggle
            CopyFloat(cm, "_scale",                 sm, "_CL_Scale");
            CopyFloat(cm, "_cloudy",                sm, "_CL_Cloudy");
            CopyFloat(cm, "_soft",                  sm, "_CL_Soft");

            CopyFloat(cm, "_yMirror",               sm, "_CL_YMirror");
            CopyFloat(cm, "_underFade",             sm, "_CL_UnderFade");
            CopyFloat(cm, "_underFadeStart",        sm, "_CL_UnderFadeStart");
            CopyFloat(cm, "_underFadeWidth",        sm, "_CL_UnderFadeWidth");

            CopyFloat(cm, "_moveRotation",          sm, "_CL_MoveRotation");
            CopyFloat(cm, "_speed_parameter",       sm, "_CL_Speed");
            CopyFloat(cm, "_shapeSpeed_parameter",  sm, "_CL_ShapeSpeed");
            CopyFloat(cm, "_speedOffset",           sm, "_CL_SpeedOffset");
            CopyFloat(cm, "_speedSlide",            sm, "_CL_SpeedSlide");
            CopyFloat(cm, "_fbmScaleUnder",         sm, "_CL_FbmScaleUnder");
        }
    }

    // --- Utils ---
    private Material GetMaterial(Renderer r, int index)
    {
        if (r == null) return null;
        // Renderer.materials はインスタンス化された Material を返す（アセットを汚さない）
        Material[] mats = r.materials;
        if (mats == null) return null;
        if (index < 0 || index >= mats.Length) return null;
        return mats[index];
    }

    private void CopyFloat(Material from, string fromProp, Material to, string toProp)
    {
        // HasProperty は UdonSharp で使用可能
        if (from.HasProperty(fromProp) && to.HasProperty(toProp))
        {
            to.SetFloat(toProp, from.GetFloat(fromProp));
        }
    }

    private void CopyTexture(Material from, string fromProp, Material to, string toProp)
    {
        if (from.HasProperty(fromProp) && to.HasProperty(toProp))
        {
            Texture tex = from.GetTexture(fromProp);
            to.SetTexture(toProp, tex);
        }
    }
}
