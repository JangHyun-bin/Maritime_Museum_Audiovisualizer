using UnityEngine;

public class AudioVisualizerLineFill : MonoBehaviour
{
    // 고정 설정 값
    [SerializeField] private int amount = 64;                   // 그래프에 사용할 샘플 포인트 개수
    [SerializeField] private float sensitivity = 120f;          // 스펙트럼 값 배율
    [SerializeField] private float minHeight = 0.1f;            // 최소 높이
    [SerializeField] private float maxHeight = 20f;             // 최대 높이
    [SerializeField] private int spectrumOffset = 0;            // 스펙트럼 시작 인덱스
    [SerializeField] private int spectrumRange = 128;           // 스펙트럼에 사용할 범위
    private readonly int spectrumSize = 512;   // FFT 스펙트럼 데이터 크기

    // 추가: 면의 두께 (z방향 깊이)를 Inspector에서 조정할 수 있도록 함.
    [SerializeField] private float fillDepth = 1.0f;

    private AudioSource audioSource;
    private float[] spectrum;

    private LineRenderer lineRenderer;
    private GameObject fillArea;    // 채움 메쉬가 붙을 GameObject
    private Mesh fillMesh;

    // Inspector에서 지정할 공용 Material과 Color (라인과 면에 동일하게 적용됨)
    [SerializeField] private Material inspectorMat;
    [SerializeField] private Color inspectorColor = Color.cyan;

    private void Start()
    {
        // AudioSource 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        spectrum = new float[spectrumSize];

        // Inspector에서 Material이 지정되지 않았다면 기본 Material 생성
        if (inspectorMat == null)
            inspectorMat = new Material(Shader.Find("Sprites/Default"));

        // LineRenderer 설정
        GameObject lineObj = new GameObject("LineGraph");
        lineObj.transform.SetParent(transform);
        lineObj.transform.localPosition = Vector3.zero;
        lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.positionCount = amount;
        lineRenderer.material = inspectorMat;
        lineRenderer.widthMultiplier = 0.1f;
        lineRenderer.startColor = inspectorColor;
        lineRenderer.endColor = inspectorColor;
        lineRenderer.useWorldSpace = false; // 로컬 좌표 사용

        // 채움 영역(면적) 메쉬 설정
        fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(transform);
        fillArea.transform.localPosition = Vector3.zero;
        MeshFilter mf = fillArea.AddComponent<MeshFilter>();
        MeshRenderer mr = fillArea.AddComponent<MeshRenderer>();
        fillMesh = new Mesh();
        mf.mesh = fillMesh;
        mr.material = inspectorMat;
    }

    private void Update()
    {
        // Inspector에서 지정한 색상을 매 프레임 Material에 적용 (라인과 면 모두)
        inspectorMat.color = inspectorColor;

        // 스펙트럼 데이터 업데이트
        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        Vector3[] linePositions = new Vector3[amount];

        // 그래프의 가로 길이 (좌우로 amount 단위 분포)
        float graphWidth = amount;
        float startX = -graphWidth / 2f;
        float spacing = graphWidth / (amount - 1);

        // 각 포인트의 위치 계산 (스펙트럼 데이터 사용)
        for (int i = 0; i < amount; i++)
        {
            float normalizedIndex = i / (float)amount;
            int idx = spectrumOffset + (int)(normalizedIndex * spectrumRange);
            idx = Mathf.Clamp(idx, spectrumOffset, spectrumOffset + spectrumRange - 1);
            idx = Mathf.Min(idx, spectrumSize - 1);

            float height = Mathf.Clamp(spectrum[idx] * sensitivity, minHeight, maxHeight);
            // 좌표: (x, height, 0)
            linePositions[i] = new Vector3(startX + i * spacing, height, 0f);
        }

        // LineRenderer의 정점 업데이트
        lineRenderer.positionCount = amount;
        lineRenderer.SetPositions(linePositions);

        // ───────────────────────────────────────────────
        // 기존의 채움 메쉬(2D 평면)를 extrude하여 두께가 있는 3D 메쉬 생성
        // ───────────────────────────────────────────────
        // 먼저, 앞면(Front Face)용 메쉬를 위해 quad strip 모양의 정점을 생성합니다.
        int n = amount;              // 점의 개수
        int N = n * 2;               // 앞면 정점 개수 (baseline + 라인)
        Vector3[] stripVertices = new Vector3[N];
        // 각 점에 대해 아래(기저선)와 위(라인)를 생성
        for (int i = 0; i < n; i++)
        {
            // baseline 정점 (y=0)
            stripVertices[i * 2] = new Vector3(startX + i * spacing, 0f, 0f);
            // 라인 정점 (스펙트럼에 따른 높이)
            stripVertices[i * 2 + 1] = linePositions[i];
        }
        // 앞면 삼각형 (quad 1개당 2개의 삼각형)
        int[] frontTriangles = new int[(n - 1) * 6];
        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int idx0 = i * 2;         // 현재 baseline
            int idx1 = i * 2 + 1;       // 현재 라인 정점
            int idx2 = (i + 1) * 2;     // 다음 baseline
            int idx3 = (i + 1) * 2 + 1; // 다음 라인 정점

            // 첫 번째 삼각형: baseline[i] - line[i] - line[i+1]
            frontTriangles[t++] = idx0;
            frontTriangles[t++] = idx1;
            frontTriangles[t++] = idx3;

            // 두 번째 삼각형: baseline[i] - line[i+1] - baseline[i+1]
            frontTriangles[t++] = idx0;
            frontTriangles[t++] = idx3;
            frontTriangles[t++] = idx2;
        }

        // ─ Front & Back 면 구성 ─
        // 앞면은 위에서 생성한 stripVertices와 frontTriangles를 그대로 사용합니다.
        // 뒷면은 앞면 정점을 fillDepth만큼 z방향으로 오프셋한 복사본이며, 삼각형의 winding(회전 순서)을 반대로 합니다.
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
            // 뒷면은 반대 순서로, 그리고 인덱스에 N(앞면 정점 개수)만큼 오프셋
            backTriangles[i] = c + N;
            backTriangles[i + 1] = b + N;
            backTriangles[i + 2] = a + N;
        }

        // ─ 측면(Side) 구성 ─
        // 채움 영역의 외곽 경계는 아래쪽은 baseline, 위쪽은 라인(하지만 순서는 반대로)입니다.
        int boundaryCount = n * 2; // 경계 정점 수
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
        // 측면 삼각형: 경계의 각 변(edge)마다 쿼드를 만들어 두 개의 삼각형으로 채웁니다.
        // 경계는 닫힌 폴리곤이므로 마지막 정점과 첫 정점도 연결됩니다.
        int[] sideTriangles = new int[boundaryCount * 6]; // 각 edge 당 2개의 삼각형(6 인덱스)
        // 측면 전용 정점은 나중에 전체 메쉬 정점 배열의 뒤쪽에 배치할 예정입니다.
        // 전체 정점 배열 구성:
        // [0 ~ N-1]               : 앞면 정점 (N = 2*n)
        // [N ~ 2*N-1]             : 뒷면 정점
        // [2*N ~ 2*N+boundaryCount-1]   : 경계 앞면 정점
        // [2*N+boundaryCount ~ 2*N+2*boundaryCount-1] : 경계 뒷면 정점
        int offsetBoundaryFront = 2 * N;              // 2*N = 4*n
        int offsetBoundaryBack = offsetBoundaryFront + boundaryCount; // 4*n + 2*n = 6*n

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

        // ─ 전체 메쉬 정점 배열 구성 ─
        int totalVertices = N     // 앞면
                          + N     // 뒷면
                          + boundaryCount  // 경계 앞면
                          + boundaryCount; // 경계 뒷면
        Vector3[] vertices = new Vector3[totalVertices];
        // 앞면 정점
        for (int i = 0; i < N; i++)
        {
            vertices[i] = stripVertices[i];
        }
        // 뒷면 정점
        for (int i = 0; i < N; i++)
        {
            vertices[i + N] = backVerts[i];
        }
        // 경계 앞면 정점
        for (int i = 0; i < boundaryCount; i++)
        {
            vertices[i + 2 * N] = boundaryFront[i];
        }
        // 경계 뒷면 정점
        for (int i = 0; i < boundaryCount; i++)
        {
            vertices[i + 2 * N + boundaryCount] = boundaryBack[i];
        }

        // ─ 전체 삼각형 배열 구성 ─
        int totalTrianglesLength = frontTriangles.Length + backTriangles.Length + sideTriangles.Length;
        int[] allTriangles = new int[totalTrianglesLength];
        int offset = 0;
        System.Array.Copy(frontTriangles, 0, allTriangles, offset, frontTriangles.Length);
        offset += frontTriangles.Length;
        System.Array.Copy(backTriangles, 0, allTriangles, offset, backTriangles.Length);
        offset += backTriangles.Length;
        System.Array.Copy(sideTriangles, 0, allTriangles, offset, sideTriangles.Length);

        // 메쉬 업데이트
        fillMesh.Clear();
        fillMesh.vertices = vertices;
        fillMesh.triangles = allTriangles;
        fillMesh.RecalculateNormals();
    }
}
