using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class NormalVisualizer : MonoBehaviour
{
    // 设置法线长度
    public float normalLength = 0.1f;
    // 设置法线颜色
    public Color normalColor = Color.red;

    private void OnDrawGizmos()
    {
        // 获取MeshFilter组件
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter)
        {
            // 获取Mesh
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh)
            {
                // 获取顶点和法线
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;

                // 检查顶点和法线数量是否一致
                if (vertices.Length == normals.Length)
                {
                    // 对每个顶点，绘制法线
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        // 将顶点位置从本地空间转换为世界空间
                        Vector3 worldVertexPosition = transform.TransformPoint(vertices[i]);
                        // 将法线方向从本地空间转换为世界空间
                        Vector3 worldNormalDirection = transform.TransformDirection(normals[i]);

                        // 绘制法线
                        Debug.DrawRay(worldVertexPosition, worldNormalDirection * normalLength, normalColor);
                    }
                }
            }
        }
    }
}