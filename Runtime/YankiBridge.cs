using UnityEngine;
using ODAXR.YankiXR.Data;

namespace ODAXR.YankiXR.Runtime
{
    public class YankiBridge : MonoBehaviour
    {
        [Header("Data & Target")]
        [SerializeField] private YankiGridData gridData;
        [SerializeField] private AudioSource targetAudioSource;
        [SerializeField] private Transform listenerTransform;

        [Header("Acoustic Limits")]
        [SerializeField] private float minCutoffFreq = 300f;
        [SerializeField] private float maxCutoffFreq = 22000f;
        [SerializeField] private float minVolumeRatio = 0.05f;

        [Header("Settings")]
        [SerializeField] private float smoothSpeed = 6f;
        [SerializeField] private bool showDebugLogs = false; // Performans içinvarsayılan false

        private AudioLowPassFilter lowPassFilter;
        private AudioReverbFilter reverbFilter;
        
        private int lastSourceIndex = -2;
        private int lastListenerIndex = -2;
        private float currentOcclusion = -1f;
        private bool isSetupValid = false;

#if UNITY_EDITOR
        internal YankiGridData PreviewGrid => gridData;
        internal Transform PreviewSource
        {
            get
            {
                AudioSource source = targetAudioSource != null ? targetAudioSource : GetComponent<AudioSource>();
                return source != null ? source.transform : null;
            }
        }
#endif

        private void Start()
        {
            ValidateSetup();
        }

        private void ValidateSetup()
        {
            isSetupValid = false;

            if (targetAudioSource == null)
                targetAudioSource = GetComponent<AudioSource>();

            if (targetAudioSource == null || gridData == null) return;

            if (listenerTransform == null && Camera.main != null)
                listenerTransform = Camera.main.transform;

            if (listenerTransform == null) return;

            if (!gridData.IsBaked)
            {
                Debug.LogWarning("[YankiBridge] Grid Data fırınlanmamış! 'Yanki XR > Acoustic Baker' menüsünden Bake yapın.");
                return;
            }

            if (!targetAudioSource.TryGetComponent(out lowPassFilter))
                lowPassFilter = targetAudioSource.gameObject.AddComponent<AudioLowPassFilter>();

            if (!targetAudioSource.TryGetComponent(out reverbFilter))
                reverbFilter = targetAudioSource.gameObject.AddComponent<AudioReverbFilter>();

            reverbFilter.reverbPreset = AudioReverbPreset.User;
            lowPassFilter.lowpassResonanceQ = 1.2f;

            isSetupValid = true;
        }

        private void Update()
        {
            if (!isSetupValid) return;

            // 1. SAF O(1) VOKSEL ERİŞİMİ (DÖNGÜSÜZ / RAYCAST'SİZ)
            int sourceIndex = gridData.GetCellIndex(targetAudioSource.transform.position);
            int listenerIndex = gridData.GetCellIndex(listenerTransform.position);

            if (sourceIndex == -1 || listenerIndex == -1) return;

            // 2. SAF O(1) MATRİS ERİŞİMİ
            float totalOcclusion = gridData.GetOcclusion(sourceIndex, listenerIndex);

            // 3. LOGLAMA (Sadece Değişiklik Olduğunda)
            if (showDebugLogs && (sourceIndex != lastSourceIndex || listenerIndex != lastListenerIndex))
            {
                lastSourceIndex = sourceIndex;
                lastListenerIndex = listenerIndex;
                currentOcclusion = totalOcclusion;

                Debug.Log($"[YankiBridge] Source Index: {sourceIndex} | Listener Index: {listenerIndex} | Occlusion: {totalOcclusion:F2}");
            }

            // 4. AKUSTİK FİLTRE INTERPOLATION
            float targetVolume = Mathf.Lerp(1f, minVolumeRatio, totalOcclusion);
            float targetCutoff = CalculateLogarithmicCutoff(totalOcclusion);

            targetAudioSource.volume = Mathf.Lerp(targetAudioSource.volume, targetVolume, Time.deltaTime * smoothSpeed);
            lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetCutoff, Time.deltaTime * smoothSpeed);
        }

        private float CalculateLogarithmicCutoff(float occlusionFactor)
        {
            float minLog = Mathf.Log10(minCutoffFreq);
            float maxLog = Mathf.Log10(maxCutoffFreq);
            float logCutoff = Mathf.Lerp(maxLog, minLog, Mathf.Pow(occlusionFactor, 0.6f));
            return Mathf.Pow(10f, logCutoff);
        }
    }
}