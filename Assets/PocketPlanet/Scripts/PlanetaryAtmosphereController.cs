using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPlanet
{
    public enum PlanetaryZone
    {
        VerdantHaven = 0,
        DeepSpaceCorridor = 1,
        ObsidianDeadwood = 2
    }

    public class PlanetaryAtmosphereController : MonoBehaviour
    {
        public static PlanetaryAtmosphereController Instance { get; private set; }

        public Light mainDirectionalLight;
        public Light secondaryFillLight;
        public Material skyboxMaterial;
        public Camera mainCamera;

        public PlanetaryZone CurrentZone { get; private set; } = PlanetaryZone.VerdantHaven;
        public bool ShrineIgnited { get; private set; }

        private Color _curFogColor = new Color(0.11f, 0.14f, 0.28f);
        private float _curFogDensity = 0.0026f;
        private Color _curSunColor = new Color(1.0f, 0.94f, 0.84f);
        private float _curSunIntensity = 1.65f;
        private Color _curSkyAmb = new Color(0.36f, 0.44f, 0.68f);
        private Color _curEqAmb = new Color(0.32f, 0.50f, 0.46f);
        private Color _curGndAmb = new Color(0.20f, 0.16f, 0.28f);
        private Color _curCamBg = new Color(0.07f, 0.09f, 0.20f);

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            ApplyImmediate(PlanetaryZone.VerdantHaven);
        }

        public void SetShrineIgnited(bool ignited)
        {
            ShrineIgnited = ignited;
        }

        public void ApplyImmediate(PlanetaryZone zone)
        {
            CurrentZone = zone;
            GetTargetValues(zone, out _curFogColor, out _curFogDensity, out _curSunColor, out _curSunIntensity,
                out _curSkyAmb, out _curEqAmb, out _curGndAmb, out _curCamBg);
            PushToRenderSettings();
        }

        private void Update()
        {
            Vector3 refPos = Vector3.zero;
            if (SpaceshipFlightController.Instance != null && SpaceshipFlightController.Instance.IsInFlight)
            {
                refPos = SpaceshipFlightController.Instance.transform.position;
            }
            else if (PocketPlanetGameManager.Instance != null && PocketPlanetGameManager.Instance.player != null)
            {
                refPos = PocketPlanetGameManager.Instance.player.transform.position;
            }

            PlanetaryZone desiredZone;
            if (refPos.z < 180f)
                desiredZone = PlanetaryZone.VerdantHaven;
            else if (refPos.z < 720f)
                desiredZone = PlanetaryZone.DeepSpaceCorridor;
            else
                desiredZone = PlanetaryZone.ObsidianDeadwood;

            if (desiredZone != CurrentZone)
            {
                CurrentZone = desiredZone;
                if (CurrentZone == PlanetaryZone.VerdantHaven)
                    PocketPlanetAudio.SetZoneMusic("bgm_pocket_planet");
                else
                    PocketPlanetAudio.SetZoneMusic("bgm_obsidian_deadwood");
            }

            GetTargetValues(CurrentZone, out Color tFogCol, out float tFogDens, out Color tSunCol, out float tSunInt,
                out Color tSkyAmb, out Color tEqAmb, out Color tGndAmb, out Color tCamBg);

            float k = Time.deltaTime * 3.2f;
            _curFogColor = Color.Lerp(_curFogColor, tFogCol, k);
            _curFogDensity = Mathf.Lerp(_curFogDensity, tFogDens, k);
            _curSunColor = Color.Lerp(_curSunColor, tSunCol, k);
            _curSunIntensity = Mathf.Lerp(_curSunIntensity, tSunInt, k);
            _curSkyAmb = Color.Lerp(_curSkyAmb, tSkyAmb, k);
            _curEqAmb = Color.Lerp(_curEqAmb, tEqAmb, k);
            _curGndAmb = Color.Lerp(_curGndAmb, tGndAmb, k);
            _curCamBg = Color.Lerp(_curCamBg, tCamBg, k);

            PushToRenderSettings();
        }

        private void GetTargetValues(
            PlanetaryZone zone,
            out Color fogCol,
            out float fogDens,
            out Color sunCol,
            out float sunInt,
            out Color skyAmb,
            out Color eqAmb,
            out Color gndAmb,
            out Color camBg)
        {
            switch (zone)
            {
                case PlanetaryZone.VerdantHaven:
                    fogCol = new Color(0.14f, 0.19f, 0.34f);
                    fogDens = 0.0024f;
                    sunCol = new Color(1.0f, 0.93f, 0.82f);
                    sunInt = 0.88f;
                    skyAmb = new Color(0.36f, 0.42f, 0.66f);
                    eqAmb = new Color(0.32f, 0.44f, 0.48f);
                    gndAmb = new Color(0.20f, 0.18f, 0.28f);
                    camBg = new Color(0.08f, 0.11f, 0.24f);
                    break;

                case PlanetaryZone.DeepSpaceCorridor:
                    fogCol = new Color(0.05f, 0.04f, 0.12f);
                    fogDens = 0.0006f;
                    sunCol = new Color(0.86f, 0.92f, 1.0f);
                    sunInt = 0.95f;
                    skyAmb = new Color(0.24f, 0.28f, 0.48f);
                    eqAmb = new Color(0.20f, 0.22f, 0.38f);
                    gndAmb = new Color(0.12f, 0.10f, 0.22f);
                    camBg = new Color(0.03f, 0.03f, 0.09f);
                    break;

                default: // ObsidianDeadwood
                    if (ShrineIgnited)
                    {
                        fogCol = new Color(0.16f, 0.11f, 0.26f);
                        fogDens = 0.0058f;
                        sunCol = new Color(0.95f, 0.85f, 1.0f);
                        sunInt = 0.82f;
                        skyAmb = new Color(0.28f, 0.22f, 0.42f);
                        eqAmb = new Color(0.24f, 0.18f, 0.34f);
                        gndAmb = new Color(0.14f, 0.11f, 0.20f);
                        camBg = new Color(0.07f, 0.05f, 0.14f);
                    }
                    else
                    {
                        fogCol = new Color(0.08f, 0.05f, 0.15f);
                        fogDens = 0.0110f;
                        sunCol = new Color(0.58f, 0.50f, 0.78f);
                        sunInt = 0.58f;
                        skyAmb = new Color(0.17f, 0.12f, 0.26f);
                        eqAmb = new Color(0.13f, 0.09f, 0.20f);
                        gndAmb = new Color(0.08f, 0.06f, 0.12f);
                        camBg = new Color(0.04f, 0.02f, 0.08f);
                    }
                    break;
            }
        }

        private void PushToRenderSettings()
        {
            RenderSettings.fogColor = _curFogColor;
            RenderSettings.fogDensity = _curFogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _curSkyAmb;
            RenderSettings.ambientEquatorColor = _curEqAmb;
            RenderSettings.ambientGroundColor = _curGndAmb;

            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = _curSunColor;
                mainDirectionalLight.intensity = _curSunIntensity;
            }

            if (mainCamera != null)
            {
                mainCamera.backgroundColor = _curCamBg;
            }

            if (skyboxMaterial != null)
            {
                if (skyboxMaterial.HasProperty("_SkyTint"))
                    skyboxMaterial.SetColor("_SkyTint", _curSkyAmb);
                if (skyboxMaterial.HasProperty("_GroundColor"))
                    skyboxMaterial.SetColor("_GroundColor", _curFogColor);
                if (skyboxMaterial.HasProperty("_Exposure"))
                {
                    float exp = CurrentZone == PlanetaryZone.ObsidianDeadwood
                        ? (ShrineIgnited ? 0.85f : 0.48f)
                        : (CurrentZone == PlanetaryZone.DeepSpaceCorridor ? 0.72f : 1.18f);
                    skyboxMaterial.SetFloat("_Exposure", exp);
                }
            }
        }
    }
}
