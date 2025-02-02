using UnityEngine;
using System.Collections.Generic;

public class AudioVisualizer : MonoBehaviour
{
    #region Enums & Structs
    private enum Shape { Wall }

    [System.Serializable]
    private struct Settings
    {
        public Shape shape;
        public float radius;
        public int amount;
        public float sensitivity;
        public float speed;
        public float minHeight;
        public float maxHeight;
        public Color pillarColor;
        public float pillarWidth;
        public float pillarInterval;
        public int spectrumOffset;
        public int spectrumRange;
    }
    #endregion

    #region Variables
    [SerializeField] private Settings settings;
    private float[] spectrum;
    private List<GameObject> pillars;
    private AudioSource audioSource;
    private Transform pillarsParent;
    private readonly int spectrumSize = 512; // 2�� �������� ���� -> Fast Fourier Transform 
    #endregion

    #region Unity Methods
    private void Start()
    {
        InitSettings();
        SetupAudio();
        CreateVisualizer();
        spectrum = new float[spectrumSize];
    }

    private void Update()
    {
        if (pillars == null || pillars.Count == 0) return;

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        for (int i = 0; i < pillars.Count; i++)
        {
            if (pillars[i] == null) continue;

            float normalizedIndex = i / (float)pillars.Count;
            int idx = settings.spectrumOffset + (int)(normalizedIndex * settings.spectrumRange);
            idx = Mathf.Clamp(idx, settings.spectrumOffset,
                             settings.spectrumOffset + settings.spectrumRange - 1);
            idx = Mathf.Min(idx, spectrumSize - 1); // ��ü ����Ʈ�� ũ�� ����

            float targetScale = Mathf.Clamp(spectrum[idx] * settings.sensitivity,
                                          settings.minHeight, settings.maxHeight);
            Vector3 newScale = pillars[i].transform.localScale;
            newScale.y = Mathf.Lerp(newScale.y, targetScale, Time.deltaTime * settings.speed);
            pillars[i].transform.localScale = newScale;

            Vector3 pos = pillars[i].transform.position;
            pos.y = 0f;
            pillars[i].transform.position = pos;
        }
    }
    #endregion

    #region Helper Methods
    private void InitSettings()
    {
        if (settings.amount <= 0) settings.amount = 64;
        if (settings.sensitivity <= 0) settings.sensitivity = 100f;
        if (settings.speed <= 0) settings.speed = 5f;
        if (settings.maxHeight <= 0) settings.maxHeight = 10f;
        if (settings.minHeight <= 0) settings.minHeight = 0.1f;
        if (settings.pillarColor == Color.clear) settings.pillarColor = Color.cyan;
        if (settings.pillarWidth <= 0) settings.pillarWidth = 1f;
        if (settings.pillarInterval <= 0) settings.pillarInterval = 0.1f;
        if (settings.spectrumRange <= 0) settings.spectrumRange = 128;

        settings.spectrumRange = Mathf.Min(settings.spectrumRange, spectrumSize - settings.spectrumOffset);
    }

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void CreateVisualizer()
    {
        if (pillarsParent) Destroy(pillarsParent.gameObject);

        pillarsParent = new GameObject("Pillars").transform;
        pillarsParent.SetParent(transform);
        pillarsParent.localPosition = Vector3.zero;

        pillars = new List<GameObject>();

        for (int i = 0; i < settings.amount; i++)
        {
            Vector3 position = Vector3.zero;
            float spacing = settings.pillarWidth * (1f + settings.pillarInterval);
            position.x = ((float)i - settings.amount / 2f) * spacing;

            var pillar = CreatePillar(position);
            pillar.transform.SetParent(pillarsParent);
            pillars.Add(pillar);
        }
    }

    private GameObject CreatePillar(Vector3 position)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.transform.position = position;
        pillar.transform.localScale = new Vector3(settings.pillarWidth, settings.minHeight, settings.pillarWidth);

        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        material.SetFloat("_Surface", 0.5f);
        material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        material.SetFloat("_Metallic", 0.6f);
        material.SetFloat("_Smoothness", 0.8f);

        var color = settings.pillarColor;
        color.a = 0.5f;
        material.SetColor("_BaseColor", color);
        material.SetColor("_EmissionColor", color * 0.5f);
        material.renderQueue = 3000;

        pillar.GetComponent<MeshRenderer>().material = material;

        return pillar;
    }
    #endregion
}