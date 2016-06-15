using UnityEngine;
using System.Collections;

public class useBasicComputeShader : MonoBehaviour {

    public ComputeShader shader;

	// Use this for initialization
	void Start () {

        ComputeBuffer buffer = new ComputeBuffer(4 * 4 * 2 * 2, sizeof(int));

        int kernel = shader.FindKernel("CSMain2");

        shader.SetBuffer(kernel, "buffer2", buffer);

        shader.Dispatch(kernel, 2, 2, 1);

        int[] data = new int[4 * 4 * 2 * 2];

        buffer.GetData(data);

        for (int i = 0; i < 8; i++)
        {
            string line = "";
            for (int j = 0; j < 8; j++)
            {
                line += " " + data[j + i * 8];
            }
            Debug.Log(line);
        }

        buffer.Release();

	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
