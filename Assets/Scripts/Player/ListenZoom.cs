// Assets/Scripts/Player/ListenZoom.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarFPS
{
    public class ListenZoom : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] Camera cam;
        [SerializeField] float normalFOV = 60f;
        [SerializeField] float zoomFOV = 42f;
        [SerializeField] float zoomInTime = 0.12f;
        [SerializeField] float zoomOutTime = 0.10f;

        [Header("Post FX (URP Global Volume)")]
        [SerializeField] Volume globalVolume;           // drag your Global Volume here
        [SerializeField] bool driveVignette = true;
        [SerializeField, Range(0f, 1f)] float normalVignette = 0.25f;
        [SerializeField, Range(0f, 1f)] float zoomVignette = 0.45f;

        [Header("Overlay")]
        [SerializeField] CanvasGroup overlayGroup;      // e.g., a dim/scope UI image
        [SerializeField, Range(0f, 1f)] float overlayAlpha = 0.35f;
        [SerializeField] RectTransform overlayRect;    // usually same GO as overlayGroup
        [SerializeField] float overlayScaleNormal = 1.0f;
        [SerializeField] float overlayScaleZoom = 1.15f; // grows slightly when listening

        Vignette vignette;
        Coroutine co;

        // Targets for the instant set (adjust if you already have these as fields)
        [SerializeField] float normalFov = 60f;
        [SerializeField] float listenFov = 52f;
        //[SerializeField] float normalVig = 0f;
        [SerializeField] float listenVig = 0.25f;
        [SerializeField] float normalAlpha = 0f;
        [SerializeField] float listenAlpha = 1f;
        [SerializeField] float normalScale = 1f;
        [SerializeField] float listenScale = 1f;

        void Awake()
        {
            if (!cam) cam = GetComponent<Camera>();
            if (globalVolume != null && globalVolume.profile != null)
                globalVolume.profile.TryGet(out vignette);
            if (!overlayRect && overlayGroup)
                overlayRect = overlayGroup.GetComponent<RectTransform>();
        }

        public void SetListening(bool listening)
        {
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(Tween(listening));
        }

        IEnumerator Tween(bool listening)
        {
            float dur = listening ? zoomInTime : zoomOutTime;
            float t = 0f;

            if (!cam) yield break;

            float f0 = cam.fieldOfView;
            float f1 = listening ? zoomFOV : normalFOV;

            float v0 = (vignette != null && globalVolume != null) ? vignette.intensity.value : 0f;
            float v1 = listening ? zoomVignette : normalVignette;

            float a0 = overlayGroup ? overlayGroup.alpha : 0f;
            float a1 = listening ? overlayAlpha : 0f;

            Vector3 s0 = overlayRect ? overlayRect.localScale : Vector3.one;
            Vector3 s1 = Vector3.one * (listening ? overlayScaleZoom : overlayScaleNormal);

            while (t < dur)
            {
                // Check if objects are still valid (scene might be unloading)
                if (!cam || (globalVolume == null && driveVignette)) break;

                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);

                cam.fieldOfView = Mathf.Lerp(f0, f1, u);
                if (vignette != null && globalVolume != null && driveVignette) 
                    vignette.intensity.value = Mathf.Lerp(v0, v1, u);
                if (overlayGroup) overlayGroup.alpha = Mathf.Lerp(a0, a1, u);
                if (overlayRect) overlayRect.localScale = Vector3.Lerp(s0, s1, u);

                yield return null;
            }

            // Final set (only if objects still exist)
            if (cam) cam.fieldOfView = f1;
            if (vignette != null && globalVolume != null && driveVignette) 
                vignette.intensity.value = v1;
            if (overlayGroup) overlayGroup.alpha = a1;
            if (overlayRect) overlayRect.localScale = s1;
            co = null;
        }

        public void SetImmediate(bool on)
        {
            if (!cam) return;

            cam.fieldOfView = on ? listenFov : normalFov;

#if USING_URP
            if (vignette != null && globalVolume != null && driveVignette)
                vignette.intensity.value = on ? listenVig : normalVig;
#endif

            if (overlayGroup)
                overlayGroup.alpha = on ? listenAlpha : normalAlpha;

            if (overlayRect)
                overlayRect.localScale = Vector3.one * (on ? listenScale : normalScale);
        }
        
        public void Begin(bool on)
        {
            // If this GO is disabled (e.g., during scene reload), don't try to start a coroutine.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                SetImmediate(on);
                return;
            }

            if (co != null) StopCoroutine(co);
            co = StartCoroutine(Tween(on));   // <-- was Animate(on)
        }

        void OnDestroy()
        {
            // Stop any running coroutines when component is destroyed
            if (co != null)
            {
                StopCoroutine(co);
                co = null;
            }
        }
    }
}
