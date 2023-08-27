using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class GerstnerWaves : MonoBehaviour
{
    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] displacedVertices;

    public float waveLength = 10f;
    public float amplitude = 1f;
    public float speed = 1f;
    public float direction = 1f;
    public float steepness = 0.2f;

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        originalVertices = mesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
    }

    void Update()
    {
        float k = 2 * Mathf.PI / waveLength;
        float omega = speed * k;
        float Q = steepness / (k * amplitude);

        for (int i = 0; i < originalVertices.Length; i++)
        {
            float x = originalVertices[i].x;
            float y = originalVertices[i].y;
            float phase = k * (direction * x + y) - omega * Time.time;

            float cosPhase = Mathf.Cos(phase);
            float sinPhase = Mathf.Sin(phase);

            displacedVertices[i].x = x + Q * amplitude * cosPhase;
            displacedVertices[i].y = y + Q * amplitude * sinPhase;
            displacedVertices[i].z = amplitude * sinPhase;
        }

        mesh.vertices = displacedVertices;
        mesh.RecalculateNormals();
    }
}
