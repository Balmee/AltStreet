
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class HorrorTransformationController : MonoBehaviour
{
    [Serializable]
    private class TransformationPlan
    {
        public int schema_version;
        public TransformationSettings transformation;
    }

    [Serializable]
    private class TransformationSettings
    {
        public float horror_intensity;
        public float fog_density;
        public float exposure_adjustment;
        public float saturation_multiplier;
        public float vegetation_overgrowth;
        public float streetlight_flicker_probability;
        public string ambient_audio_profile;
        public string material_profile;
        public string lighting_profile;
    }

    [Header("Scene References")]
    [SerializeField] private Light mainDirectionalLight;

    [Header("Controls")]
    [SerializeField] private KeyCode toggleKey = KeyCode.H;

    private TransformationPlan plan;

    private bool horrorActive;

    private bool originalFogEnabled;
    private float originalFogDensity;
    private Color originalFogColor;
    private Color originalAmbientLight;
    private float originalLightIntensity;
    private Color originalLightColor;

    private readonly Color horrorFogColor =
        new Color(0.10f, 0.12f, 0.16f);

    private readonly Color horrorAmbientColor =
        new Color(0.16f, 0.17f, 0.21f);

    private readonly Color horrorLightColor =
        new Color(0.68f, 0.74f, 0.90f);

    private void Start()
    {
        CaptureOriginalLighting();

        if (!LoadTransformationPlan())
        {
            Debug.LogError(
                "STREETVISION could not load its transformation plan."
            );
            return;
        }

        ApplyOriginalEnvironment();

        Debug.Log(
            "STREETVISION ready. Press H to toggle the horror transformation."
        );
    }

    
    private void Update()
    {
        if (plan == null || Keyboard.current == null)
            return;

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            if (horrorActive)
                ApplyOriginalEnvironment();
            else
                ApplyHorrorEnvironment();
        }
    }


    private void CaptureOriginalLighting()
    {
        originalFogEnabled = RenderSettings.fog;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogColor = RenderSettings.fogColor;
        originalAmbientLight = RenderSettings.ambientLight;

        if (mainDirectionalLight != null)
        {
            originalLightIntensity = mainDirectionalLight.intensity;
            originalLightColor = mainDirectionalLight.color;
        }
    }

    private bool LoadTransformationPlan()
    {
        string path = Path.Combine(
            Application.streamingAssetsPath,
            "StreetVision",
            "transformation_plan.json"
        );

        if (!File.Exists(path))
        {
            Debug.LogError($"Transformation plan not found: {path}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            plan = JsonUtility.FromJson<TransformationPlan>(json);

            if (plan == null || plan.transformation == null)
            {
                Debug.LogError(
                    "Transformation JSON is missing required fields."
                );
                return false;
            }

            Debug.Log(
                $"Loaded STREETVISION plan version {plan.schema_version}."
            );

            Debug.Log(
                $"Source method: {plan.transformation.lighting_profile}"
            );

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Failed to read transformation plan: {exception.Message}"
            );
            return false;
        }
    }

    private void ApplyOriginalEnvironment()
    {
        horrorActive = false;

        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogDensity = originalFogDensity;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.ambientLight = originalAmbientLight;

        if (mainDirectionalLight != null)
        {
            mainDirectionalLight.intensity = originalLightIntensity;
            mainDirectionalLight.color = originalLightColor;
        }

        Debug.Log("STREETVISION: Original environment restored.");
    }

    private void ApplyHorrorEnvironment()
    {
        horrorActive = true;

        TransformationSettings settings = plan.transformation;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Clamp(
            settings.fog_density, 0.001f, 0.08f
        );
        RenderSettings.fogColor = horrorFogColor;
        RenderSettings.ambientLight = horrorAmbientColor;

        if (mainDirectionalLight != null)
        {
            mainDirectionalLight.color = horrorLightColor;

            // Map the negative exposure adjustment to a bounded
            // directional-light multiplier for this prototype.
            float intensityMultiplier = Mathf.Clamp(
                1f + settings.exposure_adjustment,
                0.25f,
                1f
            );

            mainDirectionalLight.intensity =
                originalLightIntensity * intensityMultiplier;
        }

        Debug.Log(
            $"STREETVISION: Horror environment applied. " +
            $"Intensity={settings.horror_intensity:F2}, " +
            $"Fog={settings.fog_density:F4}, " +
            $"Saturation={settings.saturation_multiplier:F2}"
        );
    }
}
