using UnityEngine;
using System.Collections;

public class useCameraMaterial : MonoBehaviour
{
    public Material material;
    ComputeBuffer buffer;

    const int count = 1024;
    const float size = 5.0f;

    void Start()
    {

        buffer = new ComputeBuffer(count, sizeof(float) * 3, ComputeBufferType.Default);

        float[] points = new float[count * 3];

        Random.seed = 0;
        for (int i = 0; i < count; i++)
        {
            points[i * 3 + 0] = Random.Range(-size, size);
            points[i * 3 + 1] = Random.Range(-size, size);
            points[i * 3 + 2] = 0.0f;
        }

        buffer.SetData(points);
    }

    void OnPostRender()
    {
        material.SetPass(0);
        material.SetBuffer("buffer", buffer);
        Graphics.DrawProcedural(MeshTopology.Lines, count, 1);
    }

    void OnDestroy()
    {
        buffer.Release();
    }
}