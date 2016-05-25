using UnityEngine;
using System.Collections;

public class VoxelEditor : MonoBehaviour {
	
	public float brushRadius = 10.0f;
	public float brushStr = 0.1f;
	
	private float timeStamp;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
		float Wh = Input.GetAxis("Mouse ScrollWheel");
		if (Wh != 0.0f)
		{
			brushRadius+= Wh;
		}
		
		
		
		bool lmb = Input.GetMouseButton(0);
		bool lmbup = Input.GetMouseButtonUp(0);
		bool rmb = Input.GetMouseButton(1);
		bool rmbup = Input.GetMouseButtonUp(1);
		
		if (lmb || rmb /*|| lmbup || rmbup*/){ 
	
		    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			
			RaycastHit hit;
			
			if (Physics.Raycast(ray, out hit))
			{
				//Debug.DrawLine(ray.origin, hit.point,Color.red,1.0f);
				//print("Hit "+hit.collider.gameObject.name+" at "+hit.point);
				
				//VoxelChunk chunk = hit.collider.gameObject.GetComponent<VoxelMesh>().voxelChunk;
				VoxelChunk chunk = hit.collider.gameObject.GetComponent<VoxelChunk>();
				if (chunk != null && !chunk.QDPlanned){
					//Vector3 localPoint = hit.point - chunk.transform.position;
					//Debug.Log ("Point ="+hit.point);
					
					//if (Time.time > timeStamp+0.05f){
						if (chunk.transform.parent != null)
							VoxelCalculator.Instance.PaintBrushAll(chunk, hit.point,brushRadius,lmb?brushStr:-brushStr);
						else
							VoxelCalculator.Instance.PaintBrush(chunk, hit.point, brushRadius,lmb?brushStr:-brushStr);
						timeStamp = Time.time;
					//}
					//chunk.ResetCollider();
					
					/*if (rmbup || lmbup || Time.time > timeStamp+0.5f){
						chunk.ResetCollider();
						timeStamp = Time.time;
					}*/

					
					
				}
			}
		}
	}
}
