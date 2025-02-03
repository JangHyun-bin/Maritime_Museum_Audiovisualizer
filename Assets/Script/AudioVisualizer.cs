using UnityEngine;

public class AudioVisualizerLineFill : MonoBehaviour
{
    // 기본 설정값들
    [SerializeField] private int amount = 64;
    [SerializeField] private float sensitivity = 120f;
    [SerializeField] private float minHeight = 0.1f;
    [SerializeField] private float maxHeight = 20f;
    [SerializeField] private int spectrumOffset = 0;
    [SerializeField] private int spectrumRange = 128;
    private readonly int spectrumSize = 512;

    [SerializeField] private float fillDepth = 1.0f;
    [SerializeField] private Material inspectorMat;
    [SerializeField] private Color inspectorColor = Color.cyan;

    private AudioSource audioSource;
    private float[] spectrum;
    private LineRenderer lineRenderer;
    private Mesh fillMesh;

    private void Start()
    {
        // AudioSource 및 스펙트럼 데이터 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        spectrum = new float[spectrumSize];

        if (inspectorMat == null)
            inspectorMat = new Material(Shader.Find("Sprites/Default"));

        // 라인 그래프 생성 (원본)
        GameObject lineObj = new GameObject("LineGraph");
        lineObj.transform.SetParent(transform);
        lineObj.transform.localPosition = Vector3.zero;
        lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.positionCount = amount;
        lineRenderer.material = inspectorMat;
        lineRenderer.widthMultiplier = 0.1f;
        lineRenderer.startColor = inspectorColor;
        lineRenderer.endColor = inspectorColor;
        lineRenderer.useWorldSpace = false;

        // 채움 영역용 GameObject와 Mesh 생성 (미러 내용도 이 메쉬에 합침)
        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(transform);
        fillArea.transform.localPosition = Vector3.zero;
        MeshFilter mf = fillArea.AddComponent<MeshFilter>();
        MeshRenderer mr = fillArea.AddComponent<MeshRenderer>();
        mr.material = inspectorMat;
        fillMesh = new Mesh();
        mf.mesh = fillMesh;
    }

    private void Update()
    {
        // 매 프레임 Inspector에 설정한 색상을 적용
        inspectorMat.color = inspectorColor;

        // 스펙트럼 데이터 업데이트
        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        // ───────────────────────────────
        // 1. 라인 그래프 (상단 부분) 계산
        // ───────────────────────────────
        Vector3[] linePositions = new Vector3[amount];
        float graphWidth = amount;
        float startX = -graphWidth / 2f;
        float spacing = graphWidth / (amount - 1);

        for (int i = 0; i < amount; i++)
        {
            float normalizedIndex = i / (float)amount;
            int idx = spectrumOffset + (int)(normalizedIndex * spectrumRange);
            idx = Mathf.Clamp(idx, spectrumOffset, spectrumOffset + spectrumRange - 1);
            idx = Mathf.Min(idx, spectrumSize - 1);

            float height = Mathf.Clamp(spectrum[idx] * sensitivity, minHeight, maxHeight);
            linePositions[i] = new Vector3(startX + i * spacing, height, 0f);
        }

        // 라인 렌더러 업데이트
        lineRenderer.positionCount = amount;
        lineRenderer.SetPositions(linePositions);

        // ───────────────────────────────
        // 2. 채움 메쉬 (원본, 상단 부분) 생성
        // ───────────────────────────────
        int n = amount;
        int N = n * 2; // stripVertices는 상단 라인과 기준선(바닥)을 번갈아 저장
        Vector3[] stripVertices = new Vector3[N];
        for (int i = 0; i < n; i++)
        {
            // 기준선 (y = 0)
            stripVertices[i * 2] = new Vector3(startX + i * spacing, 0f, 0f);
            // 스펙트럼에 따른 높이 (상단 라인)
            stripVertices[i * 2 + 1] = linePositions[i];
        }

        // ─ 앞면 (Front Face) 삼각형 구성 ─
        int[] frontTriangles = new int[(n - 1) * 6];
        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int idx0 = i * 2;
            int idx1 = i * 2 + 1;
            int idx2 = (i + 1) * 2;
            int idx3 = (i + 1) * 2 + 1;

            // 첫 번째 삼각형
            frontTriangles[t++] = idx0;
            frontTriangles[t++] = idx1;
            frontTriangles[t++] = idx3;
            // 두 번째 삼각형
            frontTriangles[t++] = idx0;
            frontTriangles[t++] = idx3;
            frontTriangles[t++] = idx2;
        }

        // ─ 뒷면 (Back Face) 생성 ─
        Vector3[] backVerts = new Vector3[N];
        for (int i = 0; i < N; i++)
        {
            backVerts[i] = stripVertices[i] + new Vector3(0, 0, fillDepth);
        }
        int[] backTriangles = new int[frontTriangles.Length];
        for (int i = 0; i < frontTriangles.Length; i += 3)
        {
            int a = frontTriangles[i];
            int b = frontTriangles[i + 1];
            int c = frontTriangles[i + 2];
            // 뒷면은 삼각형 winding 순서를 반전하고, 인덱스에 N만큼 오프셋
            backTriangles[i] = c + N;
            backTriangles[i + 1] = b + N;
            backTriangles[i + 2] = a + N;
        }

        // ─ 측면 (Side) 구성 ─
        int boundaryCount = n * 2;
        Vector3[] boundaryFront = new Vector3[boundaryCount];
        // 아래쪽 경계 (왼쪽에서 오른쪽)
        for (int i = 0; i < n; i++)
        {
            boundaryFront[i] = new Vector3(startX + i * spacing, 0f, 0f);
        }
        // 위쪽 경계 (오른쪽에서 왼쪽)
        for (int i = 0; i < n; i++)
        {
            boundaryFront[n + i] = linePositions[n - 1 - i];
        }
        // 뒷면 경계는 fillDepth만큼 z 오프셋
        Vector3[] boundaryBack = new Vector3[boundaryCount];
        for (int i = 0; i < boundaryCount; i++)
        {
            boundaryBack[i] = boundaryFront[i] + new Vector3(0, 0, fillDepth);
        }
        int[] sideTriangles = new int[boundaryCount * 6]; // 각 edge마다 2개의 삼각형 (6개 인덱스)
        int offsetBoundaryFront = 2 * N;
        int offsetBoundaryBack = offsetBoundaryFront + boundaryCount;
        for (int i = 0; i < boundaryCount; i++)
        {
            int next = (i + 1) % boundaryCount;
            int idx0 = offsetBoundaryFront + i;
            int idx1 = offsetBoundaryFront + next;
            int idx2 = offsetBoundaryBack + next;
            int idx3 = offsetBoundaryBack + i;

            int si = i * 6;
            sideTriangles[si + 0] = idx0;
            sideTriangles[si + 1] = idx1;
            sideTriangles[si + 2] = idx2;
            sideTriangles[si + 3] = idx0;
            sideTriangles[si + 4] = idx2;
            sideTriangles[si + 5] = idx3;
        }

        // 원본 채움 메쉬의 전체 정점 및 삼각형 배열 구성
        int totalVertices = N + N + boundaryCount + boundaryCount;
        Vector3[] vertices = new Vector3[totalVertices];
        for (int i = 0; i < N; i++)
            vertices[i] = stripVertices[i];
        for (int i = 0; i < N; i++)
            vertices[i + N] = backVerts[i];
        for (int i = 0; i < boundaryCount; i++)
            vertices[i + 2 * N] = boundaryFront[i];
        for (int i = 0; i < boundaryCount; i++)
            vertices[i + 2 * N + boundaryCount] = boundaryBack[i];

        int totalTrianglesLength = frontTriangles.Length + backTriangles.Length + sideTriangles.Length;
        int[] allTriangles = new int[totalTrianglesLength];
        int offset = 0;
        System.Array.Copy(frontTriangles, 0, allTriangles, offset, frontTriangles.Length);
        offset += frontTriangles.Length;
        System.Array.Copy(backTriangles, 0, allTriangles, offset, backTriangles.Length);
        offset += backTriangles.Length;
        System.Array.Copy(sideTriangles, 0, allTriangles, offset, sideTriangles.Length);

        // ───────────────────────────────
        // 3. 미러(하단) 부분 생성 및 원본과 합치기
        // ───────────────────────────────
        int origVertexCount = vertices.Length;

        // (1) 미러 정점: Y값을 반전
        Vector3[] mirrorVertices = new Vector3[origVertexCount];
        for (int i = 0; i < origVertexCount; i++)
        {
            mirrorVertices[i] = new Vector3(vertices[i].x, -vertices[i].y, vertices[i].z);
        }

        // (2) 미러 삼각형: 원본 삼각형 인덱스를 미러 정점 배열에 맞게 오프셋하고, winding 순서를 반전
        int[] mirrorTriangles = new int[allTriangles.Length];
        for (int i = 0; i < allTriangles.Length; i += 3)
        {
            mirrorTriangles[i]     = allTriangles[i + 2] + origVertexCount;
            mirrorTriangles[i + 1] = allTriangles[i + 1] + origVertexCount;
            mirrorTriangles[i + 2] = allTriangles[i]     + origVertexCount;
        }

        // (3) 원본과 미러 데이터를 하나의 배열로 합치기
        Vector3[] combinedVertices = new Vector3[origVertexCount * 2];
        System.Array.Copy(vertices, combinedVertices, origVertexCount);
        System.Array.Copy(mirrorVertices, 0, combinedVertices, origVertexCount, origVertexCount);

        int[] combinedTriangles = new int[allTriangles.Length + mirrorTriangles.Length];
        System.Array.Copy(allTriangles, combinedTriangles, allTriangles.Length);
        System.Array.Copy(mirrorTriangles, 0, combinedTriangles, allTriangles.Length, mirrorTriangles.Length);

        // ─ 최종 메쉬 업데이트 ─
        fillMesh.Clear();
        fillMesh.vertices = combinedVertices;
        fillMesh.triangles = combinedTriangles;
        fillMesh.RecalculateNormals();
    }
}
